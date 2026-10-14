
using UnityEngine;
using UnityEngine.InputSystem;

public class BowCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform spriteVisual;
    [SerializeField] private Transform bowPivot;
    [SerializeField] private Transform arrowSpawn;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer bowRenderer;
    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private PlayerController controller;
    [SerializeField] private GameObject arrowPrefab;

    [Header("Bow Animation")]
    [Tooltip("Frame 0 = idle, frames 1-5 = drawing.")]
    [SerializeField] private Sprite[] bowFrames = new Sprite[6];

    [Header("Charge")]
    [SerializeField] private float maxChargeTime = 1.2f;
    [SerializeField] private float minDamage = 5f;
    [SerializeField] private float maxDamage = 30f;
    [SerializeField] private float minArrowSpeed = 12f;
    [SerializeField] private float maxArrowSpeed = 24f;
    [SerializeField] private float minKnockback = 2f;
    [SerializeField] private float maxKnockback = 12f;

    [Header("Player Recoil")]
    [SerializeField] private float minRecoil = 3f;
    [SerializeField] private float maxRecoil = 12f;

    [Header("Arrow Spawn")]
    [SerializeField] private float spawnOffset = 0.15f;


    [Header("Firing")]
    [SerializeField] private float fireCooldown = 0.15f;

    private float nextShotTime;


    private Rigidbody rb;
    private float chargeTimer;
    private bool isCharging;
    private bool facingLeft;

    public bool IsCharging => isCharging;
    public float Charge01 =>
        maxChargeTime <= 0f
            ? 1f
            : Mathf.Clamp01(chargeTimer / maxChargeTime);

    private void Awake()
    {
        rb = playerRb != null
            ? playerRb
            : GetComponent<Rigidbody>();

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (bodyRenderer == null && spriteVisual != null)
            bodyRenderer = spriteVisual.GetComponentInChildren<SpriteRenderer>();

        if (bowRenderer == null && bowPivot != null)
            bowRenderer = bowPivot.GetComponentInChildren<SpriteRenderer>();

        if (rb == null)
            Debug.LogError("BowCombat needs a player Rigidbody.", this);

        if (aimCamera == null)
            Debug.LogError("BowCombat needs an aim camera.", this);

        if (arrowPrefab == null)
            Debug.LogError("Assign an arrow prefab to BowCombat.", this);

        SetBowFrame(0);
    }

    private void Update()
    {
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

        // Flip only the body visual. BowPivot is a separate sibling.
        facingLeft = aimDirection.x < 0f;

        if (spriteVisual != null)
        {
            Vector3 scale = spriteVisual.localScale;
            scale.x = Mathf.Abs(scale.x) * (facingLeft ? -1f : 1f);
            spriteVisual.localScale = scale;
        }

        // Keep the body's SpriteRenderer in sync with the
        // existing PlayerController's facing state.
        if (bodyRenderer != null)
            bodyRenderer.flipX = false;
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

        nextShotTime = Time.time + fireCooldown;

        SetBowFrame(0);

        Vector3 direction = arrowSpawn.right.normalized;

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

            Destroy(arrowObject);
            return;
        }

        float damage = Mathf.Lerp(minDamage, maxDamage, charge);
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, charge);
        float knockback = Mathf.Lerp(minKnockback, maxKnockback, charge);
        float recoil = Mathf.Lerp(minRecoil, maxRecoil, charge);

        arrow.Launch(
            direction,
            speed,
            damage,
            knockback,
            gameObject
        );

        // Push the player opposite the shot direction.
        // Works both on the ground and in midair.

        if (controller != null)
        {
            controller.ApplyBowRecoil(-direction * recoil);
        }
        else if (rb != null)
        {
            rb.AddForce(-direction * recoil, ForceMode.Impulse);
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
