using System.Collections;
using System.Collections.Generic;
using DYP;
using UnityEngine;

/// <summary>
/// Special attack: the boss stops and roars. A shockwave spreads from the roar's center out to roarRadius, damaging
/// and pushing away every target it reaches (this throws a player out of the boss's body). With Show Debug Circle on,
/// the full area is outlined during the windup and the wave is drawn as it grows. It then conjures dark energy projectiles that hover above it and fire at the
/// player one by one, each aimed at where the player is at that moment, in a straight line.
/// Only the roar keeps the boss in place: after the recovery it moves (and may attack) again while the projectiles
/// follow it and keep firing. The roar can't be used again until its volley is over. Projectiles that haven't been
/// fired when the attack is cancelled (e.g. the boss dies) are removed.
/// No hitbox setup needed; the roar is an overlap check and the projectiles come from BlazingRoarData.
/// </summary>
public class BlazingRoarAttack : BossAttack<BlazingRoarData>
{
    // How quickly hovering projectiles catch up with their point above the moving boss (higher = stiffer).
    private const float HoverFollowSharpness = 12f;
    // Points used to draw the debug circles.
    private const int CircleSegments = 64;

    private static Material s_CircleMaterial;

    [Tooltip("Layers the roar can damage and push. Defaults to \"Player\" and \"Triggers\" (the Adventurer's layer) if left empty.")]
    [SerializeField] private LayerMask targetLayers;

    [Header("Debug Circle")]
    [Tooltip("Shows the roar's area in game: an outline of the full radius during the windup, and the shockwave growing from the center while it hits. Placeholder until there is a real effect.")]
    [SerializeField] private bool showDebugCircle = true;
    [SerializeField] private Color areaColor = new Color(0.6f, 0f, 1f, 0.4f);
    [SerializeField] private Color waveColor = new Color(0.85f, 0.35f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float areaLineWidth = 0.06f;
    [SerializeField, Min(0.01f)] private float waveLineWidth = 0.18f;
    [Tooltip("Sorting order of the circles, so they draw in front of the level and the boss.")]
    [SerializeField] private int circleSortingOrder = 100;

    private LineRenderer areaCircle;
    private LineRenderer waveCircle;
    private readonly Vector3[] circlePoints = new Vector3[CircleSegments];

    private readonly List<ArrowProjectile> hovering = new List<ArrowProjectile>();
    private readonly List<Vector2> hoverOffsetsInUse = new List<Vector2>();
    private readonly List<Vector3> hoverScales = new List<Vector3>();
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private readonly HashSet<Transform> hitCharacters = new HashSet<Transform>();
    private ContactFilter2D targetFilter;
    private Coroutine volley;

    /// <summary>True while projectiles are hovering or still being fired.</summary>
    public bool IsVolleyActive => volley != null;

    protected override void Awake()
    {
        base.Awake();

        if (targetLayers.value == 0)
            targetLayers = LayerMask.GetMask("Player", "Triggers");

        targetFilter = new ContactFilter2D { useTriggers = true };
        targetFilter.SetLayerMask(targetLayers);

        if (data != null && data.projectilePrefab != null && !data.projectilePrefab.TryGetComponent(out ArrowProjectile _))
            Debug.LogError($"{name}: The Blazing Roar projectile prefab has no ArrowProjectile component.", this);

        if (showDebugCircle) CreateDebugCircles();
        PhaseChanged += OnPhaseChanged;
    }

    private void OnDestroy()
    {
        PhaseChanged -= OnPhaseChanged;
    }

    // Don't roar again while the last volley is still in the air.
    protected override bool CanUse(in BossTargetInfo target) => volley == null;

    // Telegraph: outline the whole area while the boss winds up.
    private void OnPhaseChanged(BossAttack attack, BossAttackPhase phase)
    {
        if (phase == BossAttackPhase.Windup && data != null)
            DrawCircle(areaCircle, FromFeet(data.roarOriginOffset), data.roarRadius);
    }

    protected override IEnumerator Perform(BossTargetInfo target)
    {
        // Fixed for the whole roar; the boss stands still until its recovery is over.
        Vector2 center = FromFeet(data.roarOriginOffset);
        SpawnRoarEffect(center);
        hitCharacters.Clear();
        DrawCircle(areaCircle, center, data.roarRadius);

        // The shockwave spreads out (fast at first, then slowing down), hitting targets as it reaches them.
        float elapsed = 0f;
        while (elapsed < data.roarExpandTime)
        {
            float t = Mathf.Clamp01(elapsed / data.roarExpandTime);
            float radius = data.roarRadius * (1f - (1f - t) * (1f - t));
            HitInRadius(center, radius);
            DrawCircle(waveCircle, center, radius);

            yield return NextFixedUpdate;
            elapsed += Time.fixedDeltaTime;
        }

        HitInRadius(center, data.roarRadius);
        DrawCircle(waveCircle, center, data.roarRadius);
        yield return NextFixedUpdate;
        HideDebugCircles();

        if (data.projectilePrefab != null && data.hoverOffsets != null && data.hoverOffsets.Length > 0)
            volley = StartCoroutine(Volley(target.PlayerCenter));
    }

    protected override void Cleanup()
    {
        HideDebugCircles();
    }

    /// <summary>
    /// Conjures the projectiles, grows them in and fires them one by one. Runs on its own, so the boss is free to
    /// move once the roar's recovery is over.
    /// </summary>
    private IEnumerator Volley(Vector2 fallbackTarget)
    {
        yield return WaitFixed(data.conjureDelay);
        Conjure();

        float elapsed = 0f;
        while (elapsed < data.conjureTime)
        {
            yield return NextFixedUpdate;
            elapsed += Time.fixedDeltaTime;
            SetHoverScale(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / data.conjureTime)));
            FollowBoss();
        }
        SetHoverScale(1f);

        // Fire one by one, each at where the player is now.
        for (int i = 0; i < hovering.Count; i++)
        {
            ArrowProjectile projectile = hovering[i];
            if (projectile != null)
            {
                Vector2 aimAt = Boss.PlayerCollider != null ? (Vector2)Boss.PlayerCollider.bounds.center : fallbackTarget;
                Vector2 direction = aimAt - (Vector2)projectile.transform.position;
                projectile.LaunchInDirection(direction, data.projectileSpeed, data.projectileDamage, data.projectileLifetime, Boss.gameObject);
                hovering[i] = null;
            }

            if (i == hovering.Count - 1) break;

            // The rest keep hovering above the boss while it moves.
            elapsed = 0f;
            while (elapsed < data.fireInterval)
            {
                yield return NextFixedUpdate;
                elapsed += Time.fixedDeltaTime;
                FollowBoss();
            }
        }

        ClearHovering(false);
        volley = null;
    }

    private void SpawnRoarEffect(Vector2 center)
    {
        if (data.roarEffectPrefab == null) return;

        GameObject effect = Instantiate(data.roarEffectPrefab, new Vector3(center.x, center.y, Boss.transform.position.z), Quaternion.identity);
        if (data.roarEffectDuration > 0f) Destroy(effect, data.roarEffectDuration);
    }

    /// <summary>
    /// Damages and pushes away every target within radius that this roar hasn't hit yet. Called every step while the
    /// shockwave grows; hitCharacters is cleared when the roar starts, so each target is hit once per roar.
    /// </summary>
    private void HitInRadius(Vector2 center, float radius)
    {
        if (radius <= 0f) return;

        overlaps.Clear();
        Physics2D.OverlapCircle(center, radius, targetFilter, overlaps);

        foreach (Collider2D other in overlaps)
        {
            if (other.transform.IsChildOf(Boss.transform)) continue;

            // A character can have several colliders; hit each one once.
            Transform character = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
            if (!hitCharacters.Add(character)) continue;

            // Health can sit on a sibling child of the character. (No ?? here: Unity objects don't null-check correctly with it.)
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null) damageable = character.GetComponentInChildren<IDamageable>();
            if (damageable != null) damageable.TakeDamage(data.damage);

            BasicMovementController2D targetMovement = other.GetComponentInParent<BasicMovementController2D>();
            if (targetMovement != null)
            {
                // Away from the boss; a player standing right in the middle is thrown the way the boss faces.
                float deltaX = other.bounds.center.x - center.x;
                float side = Mathf.Abs(deltaX) < 0.05f ? Boss.FacingDirection : Mathf.Sign(deltaX);
                targetMovement.ApplyKnockback(new Vector2(side * data.pushForce, data.pushUpForce));
            }
        }
    }

    /// <summary>Spawns the projectiles at their hover points, not launched yet (they stay put until fired).</summary>
    private void Conjure()
    {
        ClearHovering(true);

        foreach (Vector2 offset in data.hoverOffsets)
        {
            Vector2 point = HoverPoint(offset);
            GameObject instance = Instantiate(data.projectilePrefab, new Vector3(point.x, point.y, Boss.transform.position.z), Quaternion.identity);

            if (!instance.TryGetComponent(out ArrowProjectile projectile))
            {
                Destroy(instance);
                continue;
            }

            hovering.Add(projectile);
            hoverOffsetsInUse.Add(offset);
            hoverScales.Add(instance.transform.localScale);
        }

        SetHoverScale(0f);
    }

    private Vector2 HoverPoint(Vector2 offset) =>
        Boss.TopCenter + new Vector2(offset.x * Boss.FacingDirection, offset.y);

    /// <summary>Moves the unfired projectiles smoothly toward their points above the boss, which may be walking.</summary>
    private void FollowBoss()
    {
        float blend = 1f - Mathf.Exp(-HoverFollowSharpness * Time.fixedDeltaTime);

        for (int i = 0; i < hovering.Count; i++)
        {
            if (hovering[i] == null) continue;

            Transform t = hovering[i].transform;
            Vector2 next = Vector2.Lerp(t.position, HoverPoint(hoverOffsetsInUse[i]), blend);
            t.position = new Vector3(next.x, next.y, t.position.z);
        }
    }

    private void SetHoverScale(float amount)
    {
        for (int i = 0; i < hovering.Count; i++)
            if (hovering[i] != null)
                hovering[i].transform.localScale = hoverScales[i] * Mathf.Max(amount, 0.01f);
    }

    /// <summary>Forgets the hovering projectiles; with destroyUnfired, also removes those not fired yet.</summary>
    private void ClearHovering(bool destroyUnfired)
    {
        if (destroyUnfired)
            foreach (ArrowProjectile projectile in hovering)
                if (projectile != null) Destroy(projectile.gameObject);

        hovering.Clear();
        hoverOffsetsInUse.Clear();
        hoverScales.Clear();
    }

    // Unfired projectiles would hang in the air forever, since their lifetime only starts at launch.
    protected override void OnCancelled()
    {
        volley = null;
        ClearHovering(true);
        HideDebugCircles();
    }

    /// <summary>Two world-space line circles under this object: the full area outline and the growing shockwave.</summary>
    private void CreateDebugCircles()
    {
        if (s_CircleMaterial == null)
        {
            // Sprites/Default is in the Always Included Shaders list, so it exists in builds too.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning($"{name}: Shader 'Sprites/Default' not found, so the roar's debug circle is off.", this);
                return;
            }
            s_CircleMaterial = new Material(shader);
        }

        areaCircle = CreateCircle("RoarAreaDebugCircle", areaColor, areaLineWidth);
        waveCircle = CreateCircle("RoarWaveDebugCircle", waveColor, waveLineWidth);
    }

    private LineRenderer CreateCircle(string objectName, Color color, float width)
    {
        var circleObject = new GameObject(objectName);
        circleObject.transform.SetParent(transform, false);

        var line = circleObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = CircleSegments;
        line.widthMultiplier = width;
        line.sharedMaterial = s_CircleMaterial;
        line.startColor = color;
        line.endColor = color;
        line.sortingOrder = circleSortingOrder;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    private void DrawCircle(LineRenderer line, Vector2 center, float radius)
    {
        if (line == null || Boss == null) return;

        float z = Boss.transform.position.z;
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / CircleSegments;
            circlePoints[i] = new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, z);
        }

        line.SetPositions(circlePoints);
        line.enabled = radius > 0f;
    }

    private void HideDebugCircles()
    {
        if (areaCircle != null) areaCircle.enabled = false;
        if (waveCircle != null) waveCircle.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null || !TryGetGizmoFrame(out Vector2 feet, out int facing)) return;

        Gizmos.color = new Color(0.6f, 0f, 1f);
        Gizmos.DrawWireSphere(FromFeet(data.roarOriginOffset, feet, facing), data.roarRadius);

        BossBrain brain = GetComponentInParent<BossBrain>();
        if (brain == null || data.hoverOffsets == null) return;

        Vector2 top = brain.TopCenter;
        foreach (Vector2 offset in data.hoverOffsets)
            Gizmos.DrawWireSphere(top + new Vector2(offset.x * facing, offset.y), 0.2f);
    }
}
