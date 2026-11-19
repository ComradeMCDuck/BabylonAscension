
using System;
using System.Collections.Generic;
using DYP;
using UnityEngine;
using UnityEngine.InputSystem;

public class BowCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform bowPivot;
    [SerializeField] private Transform arrowSpawn;
    [Tooltip("Where the aim line starts. With UpperBodyAimRig, use the bow arm's shoulder bone so shots follow the same line the arm does. Empty = BowPivot.")]
    [SerializeField] private Transform aimOrigin;
    [Tooltip("Kinematic 2D controller. Recoil is applied through it when assigned.")]
    [SerializeField] private BasicMovementController2D movementController;
    [SerializeField] private Animator animator;


    [Header("Arrows")]
    [Tooltip("The arrow every normal shot fires. Each prefab carries its own ArrowData.")]
    [SerializeField] private GameObject basicArrowPrefab;

    [Header("Special Arrow")]
    [Tooltip("Arms the special arrow (the one picked at an altar): the next shot fires it and uses up its charge. " +
             "Pressing it again disarms. Enemy kills recharge it (Kills To Charge in the arrow's ArrowData).")]
    [SerializeField] private Key specialArrowKey = Key.Q;

    [Header("Charge")]
    [Tooltip("Seconds of holding needed to reach full charge.")]
    [SerializeField] private float maxChargeTime = 1.2f;
    [Tooltip("Seconds the bow must be drawn before releasing fires an arrow. Releasing earlier cancels the shot, so spam-clicking doesn't fire.")]
    [SerializeField, Min(0f)] private float minDrawTime = 0.25f;

    [Header("Damage Charge")]
    [Tooltip("Damage multiplier at no charge. The arrow's Base Damage is multiplied by this.")]
    [SerializeField, Min(0f)] private float minDamageMultiplier = 0.2f;
    [Tooltip("Damage multiplier at full charge (Max Charge Time).")]
    [SerializeField, Min(0f)] private float maxDamageMultiplier = 1f;
    [Tooltip("Shape of the climb from the min to the max multiplier. 1 = straight line; higher = slow start that gets steeper toward full charge.")]
    [SerializeField, Min(0.01f)] private float damageCurveExponent = 2.5f;

    [Header("Full Draw Bonus")]
    [Tooltip("Added to the max damage multiplier when the shot is released within Bonus Window after reaching full charge.")]
    [SerializeField, Min(0f)] private float fullDrawBonusMultiplier = 0.5f;
    [Tooltip("Seconds after reaching full charge during which releasing gets the bonus. After that the bonus is gone and the bow starts to sway.")]
    [SerializeField, Min(0f)] private float fullDrawBonusWindow = 0.6f;
    [Tooltip("Optional effect shown on the bow during the bonus window (turned on and off with SetActive). Empty = a placeholder glow is created on BowPivot.")]
    [SerializeField] private GameObject fullDrawBonusEffect;

    [Header("Overdraw Sway")]
    [Tooltip("Sway size (degrees up and down from the aim) when the bonus window ends.")]
    [SerializeField, Min(0f)] private float swayStartAngle = 3f;
    [Tooltip("Degrees the sway size grows per second of holding past the bonus window. 0 = the sway stays the same size.")]
    [SerializeField, Min(0f)] private float swayAngleGrowth = 6f;
    [Tooltip("Largest sway size, in degrees.")]
    [SerializeField, Min(0f)] private float swayMaxAngle = 20f;
    [Tooltip("Full up-and-down swings per second.")]
    [SerializeField, Min(0f)] private float swayFrequency = 1.2f;

    [Header("Arrow Spawn")]
    [SerializeField] private float spawnOffset = 0.15f;


    [Header("Firing")]
    [SerializeField] private float fireCooldown = 0.15f;
    [Tooltip("Fraction of the arrow's recoil applied while standing on the ground. 1 = same as in the air.")]
    [SerializeField, Range(0f, 1f)] private float groundedRecoilMultiplier = 0.8f;

    private float nextShotTime;

    // Shorter aim vectors than this give no usable direction; the last one is kept.
    private const float MinAimSqrDistance = 0.0001f;
    // Kills needed while no special arrow (or one without ArrowData) is picked.
    private const int DefaultKillsToCharge = 3;

    // Cached by AimAtCursor. Shots use these instead of a transform's rotation, because the
    // spawn point may sit on a mirrored or IK-driven bone whose +X isn't the aim.
    private Vector3 cursorWorld;
    private Vector2 aimDirection = Vector2.right;
    private bool hasCursor;

    // chargeTimer stops at Max Charge Time; holdTime keeps counting for the bonus window and the sway.
    private float chargeTimer;
    private float holdTime;
    private float swayAngle;
    private bool isCharging;
    // The arrow picked at an altar: the special arrow, or the basic arrow while none is picked.
    private GameObject currentArrowPrefab;
    private readonly BowUpgrades upgrades = new BowUpgrades();
    // Arrows bought at altars (plus the basic arrow). Switching to an owned arrow is free.
    private readonly HashSet<GameObject> ownedArrows = new HashSet<GameObject>();

    // The special charge is shared by all special arrows, so switching arrows at an altar doesn't refill it.
    // A run starts charged.
    private bool specialReady = true;
    private int specialKills;
    private bool specialArmed;

    /// <summary>Raised with the new arrow prefab whenever the picked (special) arrow changes.</summary>
    public event Action<GameObject> ArrowChanged;
    /// <summary>Raised after an arrow was launched: the arrow prefab and the charge (0..1). Used by analytics.</summary>
    public event Action<GameObject, float> ArrowFired;

    public bool IsCharging => isCharging;
    /// <summary>The arrow picked at an altar. The basic arrow while no special arrow is picked.</summary>
    public GameObject CurrentArrowPrefab => currentArrowPrefab;
    public GameObject BasicArrowPrefab => basicArrowPrefab;
    /// <summary>True when an arrow other than the basic one is picked, so Q has something to arm.</summary>
    public bool HasSpecialArrow => currentArrowPrefab != null && currentArrowPrefab != basicArrowPrefab;
    /// <summary>The picked special arrow, or null while only the basic arrow is picked.</summary>
    public GameObject SpecialArrowPrefab => HasSpecialArrow ? currentArrowPrefab : null;
    /// <summary>True when the special arrow is charged and can be armed.</summary>
    public bool IsSpecialReady => specialReady;
    /// <summary>True when the next shot fires the special arrow.</summary>
    public bool IsSpecialArmed => specialArmed;
    /// <summary>Kills counted toward the next special charge (0 while charged).</summary>
    public int SpecialKills => specialKills;
    /// <summary>Kills the picked special arrow needs to recharge.</summary>
    public int SpecialKillsNeeded => GetKillsToCharge(SpecialArrowPrefab);
    /// <summary>Permanent upgrades picked at altars. PlayerProgress carries them across scene loads and death.</summary>
    public BowUpgrades Upgrades => upgrades;
    /// <summary>Arrows bought at altars, plus the basic arrow.</summary>
    public IReadOnlyCollection<GameObject> OwnedArrows => ownedArrows;
    /// <summary>Max Charge Time with upgrades applied.</summary>
    public float MaxChargeTime => Mathf.Max(0f, upgrades.Apply(BowStat.MaxChargeTime, maxChargeTime));
    public float Charge01 =>
        MaxChargeTime <= 0f
            ? 1f
            : Mathf.Clamp01(chargeTimer / MaxChargeTime);
    /// <summary>Seconds the current draw has been held, including past full charge. 0 when not drawing.</summary>
    public float HoldTime => isCharging ? holdTime : 0f;
    /// <summary>True while releasing would get the full-draw bonus.</summary>
    public bool IsFullDrawBonusActive =>
        isCharging && holdTime >= MaxChargeTime && holdTime < MaxChargeTime + fullDrawBonusWindow;
    /// <summary>True once the draw is held past the bonus window: the bonus is gone and the bow sways.</summary>
    public bool IsOverdrawn => isCharging && holdTime >= MaxChargeTime + fullDrawBonusWindow;
    /// <summary>Degrees the aim is currently turned by overdraw sway (0 when not swaying). UpperBodyAimRig turns the arms by it too.</summary>
    public float SwayAngle => swayAngle;
    /// <summary>Damage multiplier a shot released right now would get.</summary>
    public float CurrentDamageMultiplier => GetDamageMultiplier();

    private void Awake()
    {
        if (movementController == null)
            movementController = GetComponentInParent<BasicMovementController2D>();

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (aimCamera == null)
            Debug.LogError("BowCombat needs an aim camera.", this);

        if (basicArrowPrefab == null)
            Debug.LogError("Assign the basic arrow prefab to BowCombat.", this);

        // Start on the basic arrow; PlayerProgress restores bought arrows and the last pick after a scene load.
        currentArrowPrefab = basicArrowPrefab;
        if (basicArrowPrefab != null) ownedArrows.Add(basicArrowPrefab);

        if (fullDrawBonusEffect == null && bowPivot != null)
            fullDrawBonusEffect = BowBonusGlow.Create(bowPivot, FindTopSprite()).gameObject;

        if (fullDrawBonusEffect != null)
            fullDrawBonusEffect.SetActive(false);
    }

    /// <summary>The character's front-most sprite, so the placeholder glow can draw above it.</summary>
    private SpriteRenderer FindTopSprite()
    {
        Transform root = movementController != null ? movementController.transform : transform;
        SpriteRenderer top = null;
        foreach (SpriteRenderer sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (top == null || sprite.sortingOrder > top.sortingOrder)
                top = sprite;
        }
        return top;
    }

    public bool OwnsArrow(GameObject prefab) => prefab != null && ownedArrows.Contains(prefab);

    /// <summary>Marks an arrow as bought, so picking it later is free. Doesn't select it.</summary>
    public void UnlockArrow(GameObject prefab)
    {
        if (prefab != null) ownedArrows.Add(prefab);
    }

    /// <summary>
    /// Makes the given arrow the special arrow fired after pressing Q, e.g. when the player picks one at an altar.
    /// Picking the basic arrow leaves the bow without a special arrow.
    /// </summary>
    public void SetArrow(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Tried to give BowCombat an empty arrow prefab.", this);
            return;
        }

        if (prefab == currentArrowPrefab)
            return;

        currentArrowPrefab = prefab;
        if (!HasSpecialArrow) specialArmed = false;
        // The new arrow may need fewer kills than are already counted.
        CheckSpecialCharged();

        ArrowChanged?.Invoke(prefab);
        SpecialArrowChanged?.Invoke();
    }

    /// <summary>Restores the special charge, e.g. from PlayerProgress after a scene load.</summary>
    public void SetSpecialCharge(bool ready, int kills)
    {
        specialReady = ready;
        specialKills = ready ? 0 : Mathf.Max(0, kills);
        specialArmed = false;
        CheckSpecialCharged();
        SpecialArrowChanged?.Invoke();
    }

    /// <summary>Fully charges the special arrow without changing whether it is armed (DebugMenu uses it).</summary>
    public void ChargeSpecial()
    {
        if (specialReady) return;

        specialReady = true;
        specialKills = 0;
        SpecialArrowChanged?.Invoke();
    }

    private void OnEnable() => EnemyHealth.AnyKilled += OnEnemyKilled;

    private void OnDisable()
    {
        EnemyHealth.AnyKilled -= OnEnemyKilled;
        specialArmed = false;
    }

    private void OnEnemyKilled(EnemyHealth enemy)
    {
        if (specialReady) return;

        specialKills++;
        CheckSpecialCharged();
        SpecialArrowChanged?.Invoke();
    }

    private void CheckSpecialCharged()
    {
        if (specialReady || specialKills < GetKillsToCharge(SpecialArrowPrefab)) return;

        specialReady = true;
        specialKills = 0;
    }

    private static int GetKillsToCharge(GameObject arrowPrefab)
    {
        ArrowProjectile projectile = arrowPrefab != null ? arrowPrefab.GetComponent<ArrowProjectile>() : null;
        return projectile != null && projectile.Data != null
            ? Mathf.Max(1, projectile.Data.killsToCharge)
            : DefaultKillsToCharge;
    }

    /// <summary>Q: arms the special arrow if it is charged, or disarms it.</summary>
    private void ToggleSpecialArmed()
    {
        bool armed = !specialArmed && HasSpecialArrow && specialReady;
        if (armed == specialArmed) return;

        specialArmed = armed;
        SpecialArrowChanged?.Invoke();
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
        {
            isCharging = false;
            chargeTimer = 0f;
            holdTime = 0f;
            UpdateSway();
            UpdateBonusEffect();
            return;
        }

        if (Mouse.current == null || aimCamera == null)
            return;

        AimAtCursor();

        if (Keyboard.current != null && specialArrowKey != Key.None && Keyboard.current[specialArrowKey].wasPressedThisFrame)
            ToggleSpecialArmed();

        if (Mouse.current.leftButton.wasPressedThisFrame)
            StartCharging();

        if (isCharging && Mouse.current.leftButton.isPressed)
        {
            holdTime += Time.deltaTime;
            chargeTimer = Mathf.Min(holdTime, MaxChargeTime);
        }

        if (isCharging && Mouse.current.leftButton.wasReleasedThisFrame)
            ReleaseShot();

        // After the release, so the shot used the sway the player saw on the last frame.
        UpdateSway();
        UpdateBonusEffect();

        if (animator != null)
            animator.SetBool("isCharging", IsCharging);
    }

    private void AimAtCursor()
    {
        // Intersect the camera ray with the player's XY movement plane.
        Plane aimPlane = new Plane(
            Vector3.forward,
            new Vector3(0f, 0f, transform.position.z)
        );

        Ray ray = aimCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        if (!aimPlane.Raycast(ray, out float distance))
            return;

        cursorWorld = ray.GetPoint(distance);
        hasCursor = true;

        // Aiming from the shoulder keeps the direction right even when the cursor is
        // closer than the bow hand (which sits on the shoulder-to-cursor line).
        Vector3 origin = aimOrigin != null ? aimOrigin.position : bowPivot.position;
        Vector2 aimVector = cursorWorld - origin;

        if (aimVector.sqrMagnitude < MinAimSqrDistance)
            return;

        aimDirection = aimVector.normalized;

        // Rotate the bow around the Z axis.
        float angle = Mathf.Atan2(
            aimDirection.y,
            aimDirection.x
        ) * Mathf.Rad2Deg;

        bowPivot.rotation = Quaternion.Euler(0f, 0f, angle + swayAngle);
    }

    /// <summary>
    /// Damage multiplier for the current hold: climbs from min to max along a curve until full charge,
    /// gets the bonus during the bonus window, then drops back to max once the bow is overdrawn.
    /// </summary>
    private float GetDamageMultiplier()
    {
        if (holdTime < MaxChargeTime)
            return Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, Mathf.Pow(Charge01, damageCurveExponent));

        return holdTime < MaxChargeTime + fullDrawBonusWindow
            ? maxDamageMultiplier + fullDrawBonusMultiplier
            : maxDamageMultiplier;
    }

    /// <summary>
    /// Past the bonus window the bow swings up and down around the aim, growing until Sway Max Angle.
    /// Starts at the middle of a swing, so the bow doesn't jump when the sway begins.
    /// </summary>
    private void UpdateSway()
    {
        if (!IsOverdrawn)
        {
            swayAngle = 0f;
            return;
        }

        float swayTime = holdTime - (MaxChargeTime + fullDrawBonusWindow);
        float size = Mathf.Min(swayMaxAngle, swayStartAngle + swayAngleGrowth * swayTime);
        swayAngle = size * Mathf.Sin(swayTime * swayFrequency * 2f * Mathf.PI);
    }

    private void UpdateBonusEffect()
    {
        if (fullDrawBonusEffect == null) return;

        bool show = IsFullDrawBonusActive;
        if (fullDrawBonusEffect.activeSelf != show)
            fullDrawBonusEffect.SetActive(show);
    }


    /// <summary>
    /// Straight from the spawn point to the cursor, so the shot starts on the line through the
    /// cursor even when the spawn point (the bow hand) isn't exactly on the shoulder's aim line.
    /// When the cursor is between the shoulder and the hand, that line would point backward,
    /// so the shoulder's aim direction is used instead.
    /// Overdraw sway turns the result, so the arrow flies where the swaying bow points.
    /// </summary>
    private Vector2 GetShotDirection()
    {
        Vector2 shot = aimDirection;

        if (hasCursor)
        {
            Vector2 fromSpawn = cursorWorld - arrowSpawn.position;
            if (fromSpawn.sqrMagnitude >= MinAimSqrDistance && Vector2.Dot(fromSpawn, aimDirection) > 0f)
                shot = fromSpawn.normalized;
        }

        return Quaternion.Euler(0f, 0f, swayAngle) * shot;
    }

    private void StartCharging()
    {
        if (isCharging) return;
        if (Time.time < nextShotTime) return;

        isCharging = true;
        chargeTimer = 0f;
        holdTime = 0f;
    }


    private void ReleaseShot()
    {
        if (!isCharging)
            return;

        float charge = Charge01;
        float damageMultiplier = GetDamageMultiplier();
        bool releasedTooEarly = holdTime < minDrawTime;
        isCharging = false;
        holdTime = 0f;

        // Released too early: cancel the shot instead of firing a weak arrow.
        if (releasedTooEarly)
        {
            chargeTimer = 0f;
            return;
        }

        // Normal shots always fire the basic arrow; the special one only when armed with Q.
        bool firesSpecial = specialArmed && specialReady && HasSpecialArrow;
        GameObject arrowPrefab = firesSpecial ? currentArrowPrefab : basicArrowPrefab;

        if (arrowPrefab == null)
            return;

        nextShotTime = Time.time + fireCooldown;

        Vector3 direction = GetShotDirection();

        Vector3 spawnPosition =
            arrowSpawn.position + direction * spawnOffset;

        GameObject arrowObject = Instantiate(
            arrowPrefab,
            spawnPosition,
            Quaternion.LookRotation(Vector3.forward, Vector3.up)
        );

        ArrowProjectile arrow =
            arrowObject.GetComponent<ArrowProjectile>();

        if (arrow == null)
        {
            Debug.LogError(
                "Arrow prefab needs an ArrowProjectile component.",
                arrowObject
            );
            animator?.SetTrigger("Shoot");

            Destroy(arrowObject);
            return;
        }

        // Each arrow type sets its own recoil in its ArrowData.
        float recoil = arrow.Data != null
            ? Mathf.Lerp(arrow.Data.minRecoil, arrow.Data.maxRecoil, charge)
            : 0f;

        // Use the player root as the owner so the arrow ignores
        // every player collider.
        GameObject shooter = movementController != null
            ? movementController.gameObject
            : gameObject;

        arrow.Launch(
            direction,
            charge,
            shooter,
            upgrades.Apply(BowStat.FullChargeArrowSpeed, 1f),
            damageMultiplier
        );
        animator?.SetTrigger("Shoot");
        ArrowFired?.Invoke(currentArrowPrefab, charge);

        // Spent: kill enemies to charge it again.
        if (firesSpecial)
        {
            specialArmed = false;
            specialReady = false;
            specialKills = 0;
            SpecialArrowChanged?.Invoke();
        }

        // Push the player opposite the shot direction; weaker while grounded, none while sliding.
        if (movementController != null && !movementController.IsSliding)
        {
            if (movementController.IsOnGround())
                recoil *= groundedRecoilMultiplier;

            movementController.AddImpulse(-(Vector2)direction * recoil);
        }
    }
}


