using System.Collections;
using System.Collections.Generic;
using DYP;
using UnityEngine;

/// <summary>
/// Special attack: the boss stops and roars, damaging and pushing away every target within roarRadius (this throws
/// a player out of the boss's body). It then conjures dark energy projectiles that hover above it and fire at the
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

    [Tooltip("Layers the roar can damage and push. Defaults to \"Player\" and \"Triggers\" (the Adventurer's layer) if left empty.")]
    [SerializeField] private LayerMask targetLayers;

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
    }

    // Don't roar again while the last volley is still in the air.
    protected override bool CanUse(in BossTargetInfo target) => volley == null;

    protected override IEnumerator Perform(BossTargetInfo target)
    {
        Roar(FromFeet(data.roarOriginOffset));

        if (data.projectilePrefab != null && data.hoverOffsets != null && data.hoverOffsets.Length > 0)
            volley = StartCoroutine(Volley(target.PlayerCenter));

        yield break;
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

    /// <summary>Damages and pushes away every target in the roar's radius, once each.</summary>
    private void Roar(Vector2 center)
    {
        if (data.roarEffectPrefab != null)
        {
            GameObject effect = Instantiate(data.roarEffectPrefab, new Vector3(center.x, center.y, Boss.transform.position.z), Quaternion.identity);
            if (data.roarEffectDuration > 0f) Destroy(effect, data.roarEffectDuration);
        }

        overlaps.Clear();
        hitCharacters.Clear();
        Physics2D.OverlapCircle(center, data.roarRadius, targetFilter, overlaps);

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
