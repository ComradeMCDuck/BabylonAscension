
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
    [Tooltip("Element 0 is selected with key 1, element 1 with key 2, element 2 with key 3, element 3 with key 4. Each prefab carries its own ArrowData.")]
    [SerializeField] private GameObject[] arrowPrefab;
    [SerializeField] private int selectedArrowIndex = 0;

    [Header("Bow Animation")]
    [Tooltip("Frame 0 = idle, frames 1-5 = drawing.")]
    [SerializeField] private Sprite[] bowFrames = new Sprite[6];

    [Header("Charge")]
    [Tooltip("Seconds of holding needed to reach full charge.")]
    [SerializeField] private float maxChargeTime = 1.2f;

    [Header("Arrow Spawn")]
    [SerializeField] private float spawnOffset = 0.15f;


    [Header("Firing")]
    [SerializeField] private float fireCooldown = 0.15f;

    private float nextShotTime;


    private float chargeTimer;
    private bool isCharging;

    public bool IsCharging => isCharging;
    public int SelectedArrowIndex => selectedArrowIndex;
    public float Charge01 =>
        maxChargeTime <= 0f
            ? 1f
            : Mathf.Clamp01(chargeTimer / maxChargeTime);

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

        if (arrowPrefab == null || arrowPrefab.Length == 0)
            Debug.LogError("Assign an arrow prefab to BowCombat.", this);

        SelectArrow(selectedArrowIndex);
        SetBowFrame(0);
    }

    private void OnEnable()
    {
        if (movementController != null)
            movementController.OnArrowSelected += SelectArrow;
    }

    private void OnDisable()
    {
        if (movementController != null)
            movementController.OnArrowSelected -= SelectArrow;
    }

    public void SelectArrow(int index)
    {
        if (arrowPrefab == null || arrowPrefab.Length == 0)
            return;

        if (index < 0 || index >= arrowPrefab.Length || arrowPrefab[index] == null)
        {
            Debug.LogWarning($"No arrow prefab assigned at index {index}.", this);
            return;
        }

        selectedArrowIndex = index;
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
            chargeTimer = Mathf.Min(chargeTimer, maxChargeTime);
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

        if (arrowPrefab == null ||
            selectedArrowIndex < 0 ||
            selectedArrowIndex >= arrowPrefab.Length ||
            arrowPrefab[selectedArrowIndex] == null)
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
            arrowPrefab[selectedArrowIndex],
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
            shooter
        );

        // Push the player opposite the shot direction only while airborne.
        if (movementController != null && !movementController.IsOnGround() && !movementController.IsSliding)
        {
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
