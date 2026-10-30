
using System;
using DYP;
using UnityEngine;
using UnityEngine.InputSystem;

public class BowCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform bowPivot;
    [SerializeField] private Transform arrowSpawn;
    [SerializeField] private SpriteRenderer bowRenderer;
    [Tooltip("Kinematic 2D controller. Recoil is applied through it when assigned.")]
    [SerializeField] private BasicMovementController2D movementController;

    [Header("Arrows")]
    [Tooltip("The arrow fired until another one is picked at an altar. Each prefab carries its own ArrowData.")]
    [SerializeField] private GameObject basicArrowPrefab;

    [Header("Bow Animation")]
    [Tooltip("Frame 0 = idle, frames 1-5 = drawing.")]
    [SerializeField] private Sprite[] bowFrames = new Sprite[6];

    [Header("Charge")]
    [Tooltip("Seconds of holding needed to reach full charge.")]
    [SerializeField] private float maxChargeTime = 1.2f;
    [Tooltip("Seconds the bow must be drawn before releasing fires an arrow. Releasing earlier cancels the shot, so spam-clicking doesn't fire.")]
    [SerializeField, Min(0f)] private float minDrawTime = 0.25f;

    [Header("Arrow Spawn")]
    [SerializeField] private float spawnOffset = 0.15f;


    [Header("Firing")]
    [SerializeField] private float fireCooldown = 0.15f;
    [Tooltip("Fraction of the arrow's recoil applied while standing on the ground. 1 = same as in the air.")]
    [SerializeField, Range(0f, 1f)] private float groundedRecoilMultiplier = 0.8f;

    private float nextShotTime;


    private float chargeTimer;
    private bool isCharging;
    private GameObject currentArrowPrefab;
    private readonly BowUpgrades upgrades = new BowUpgrades();

    /// <summary>Raised with the new arrow prefab whenever the fired arrow changes.</summary>
    public event Action<GameObject> ArrowChanged;

    public bool IsCharging => isCharging;
    public GameObject CurrentArrowPrefab => currentArrowPrefab;
    /// <summary>Permanent upgrades picked at altars. Recreated with the bow, so they reset on death.</summary>
    public BowUpgrades Upgrades => upgrades;
    /// <summary>Max Charge Time with upgrades applied.</summary>
    public float MaxChargeTime => Mathf.Max(0f, upgrades.Apply(BowStat.MaxChargeTime, maxChargeTime));
    public float Charge01 =>
        MaxChargeTime <= 0f
            ? 1f
            : Mathf.Clamp01(chargeTimer / MaxChargeTime);

    private void Awake()
    {
        if (movementController == null)
            movementController = GetComponentInParent<BasicMovementController2D>();

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (bowRenderer == null && bowPivot != null)
            bowRenderer = bowPivot.GetComponentInChildren<SpriteRenderer>();

        if (aimCamera == null)
            Debug.LogError("BowCombat needs an aim camera.", this);

        if (basicArrowPrefab == null)
            Debug.LogError("Assign the basic arrow prefab to BowCombat.", this);

        // A scene reload on death recreates the bow, so the pick resets to the basic arrow.
        currentArrowPrefab = basicArrowPrefab;
        SetBowFrame(0);
    }

    /// <summary>Makes the bow fire the given arrow prefab, e.g. when the player picks one at an altar.</summary>
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
        ArrowChanged?.Invoke(prefab);
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
        {
            isCharging = false;
            chargeTimer = 0f;
            SetBowFrame(0);
            return;
        }

        if (Mouse.current == null || aimCamera == null)
            return;

        AimAtCursor();

        if (Mouse.current.leftButton.wasPressedThisFrame)
            StartCharging();

        if (isCharging && Mouse.current.leftButton.isPressed)
        {
            chargeTimer += Time.deltaTime;
            chargeTimer = Mathf.Min(chargeTimer, MaxChargeTime);
            UpdateBowAnimation();
        }

        if (isCharging && Mouse.current.leftButton.wasReleasedThisFrame)
            ReleaseShot();
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

        Vector3 cursorWorld = ray.GetPoint(distance);
        Vector3 aimVector = cursorWorld - bowPivot.position;
        aimVector.z = 0f;

        if (aimVector.sqrMagnitude < 0.0001f)
            return;

        Vector2 aimDirection = aimVector.normalized;

        // Rotate the bow around the Z axis.
        float angle = Mathf.Atan2(
            aimDirection.y,
            aimDirection.x
        ) * Mathf.Rad2Deg;

        bowPivot.rotation = Quaternion.Euler(0f, 0f, angle);
    }


    private void StartCharging()
    {
        if (isCharging) return;
        if (Time.time < nextShotTime) return;

        isCharging = true;
        chargeTimer = 0f;

        UpdateBowAnimation();
    }


    private void ReleaseShot()
    {
        if (!isCharging)
            return;

        float charge = Charge01;
        isCharging = false;

        // Released too early: cancel the shot instead of firing a weak arrow.
        if (chargeTimer < minDrawTime)
        {
            chargeTimer = 0f;
            SetBowFrame(0);
            return;
        }

        if (currentArrowPrefab == null)
        {
            SetBowFrame(0);
            return;
        }

        nextShotTime = Time.time + fireCooldown;

        SetBowFrame(0);

        Vector3 direction = arrowSpawn.right.normalized;

        Vector3 spawnPosition =
            arrowSpawn.position + direction * spawnOffset;

        GameObject arrowObject = Instantiate(
            currentArrowPrefab,
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
            upgrades.Apply(BowStat.FullChargeArrowSpeed, 1f)
        );

        // Push the player opposite the shot direction; weaker while grounded, none while sliding.
        if (movementController != null && !movementController.IsSliding)
        {
            if (movementController.IsOnGround())
                recoil *= groundedRecoilMultiplier;

            movementController.AddImpulse(-(Vector2)direction * recoil);
        }
    }

    private void UpdateBowAnimation()
    {
        if (bowFrames == null || bowFrames.Length == 0)
            return;

        // Frame 0 is idle; frames 1-5 show the draw.
        int frame = Mathf.Clamp(
            1 + Mathf.FloorToInt(Charge01 * 5f),
            1,
            5
        );

        SetBowFrame(frame);
    }

    private void SetBowFrame(int index)
    {
        if (bowRenderer == null ||
            bowFrames == null ||
            bowFrames.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, bowFrames.Length - 1);

        if (bowFrames[index] != null)
            bowRenderer.sprite = bowFrames[index];
    }
}
