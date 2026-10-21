using System;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityData", menuName = "Scriptable Objects/EntityData")]
public class EntityData : ScriptableObject
{
    /// <summary>
    /// Raised when values are changed from the Inspector or through NotifyChanged (e.g. the debug menu).
    /// </summary>
    public event Action Changed;

    public int entityID;

    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Movement")]
    public float detectionRange = 5f;
    public float moveSpeed = 4f;
    public float jumpSpeed = 1.0f;
    public float dashSpeed = 10f;
    public float dashDuration = 0.2f;
    public float nextDashCooldown = 1f;
    public bool lockZAxis = true;

    [Header("Attack")]
    public float attackDamage = 1f;
    public float attackRange = 2f;
    public float attackCooldown = 5f;
    public float attackSpeed = 1f;
    public GameObject attackEffectPrefab;

    [Header("Gravity")]
    public float fallMultiplier = 2f; // > 1 = faster fall, < 1 = slower fall
    public float riseMultiplier = 1f; // applies while moving upward
    public float maxFallSpeed = 20f; // terminal velocity

    // -- Settings used by BasicMovementController2D --

    [Header("Controller - Movement Settings")]
    [Min(0f)] public float speed = 8f;
    [Min(0f)] public float accelerationTimeAirborne = 0.1f;
    [Min(0f)] public float accelerationTimeGrounded = 0.1f;

    [Header("Controller - On Ladder Settings")]
    public bool snapToRestrictedArea = false;
    public bool exitLadderOnGround = true;
    [Min(0f)] public float onLadderSpeed = 4f;
    public bool lockFacingToRight = true;

    [Header("Controller - Jump")]
    public bool hasVariableJumpHeight = true;
    [Min(0)] public int airJumpAllowed = 1;
    [Min(0f)] public float maxJumpHeight = 3.5f;
    [Min(0f)] public float minJumpHeight = 1.0f;
    [Tooltip("Seconds to reach the highest point of a jump. Also defines gravity.")]
    [Min(0.01f)] public float timeToApex = 0.4f;
    [Min(0f)] public float onLadderJumpForce = 0.4f;
    [Min(0f)] public float fallingJumpPaddingTime = 0.09f;
    [Min(0f)] public float willJumpPaddingTime = 0.15f;

    [Header("Controller - Wall Interaction")]
    public bool canWallSlide = true;
    [Min(0f)] public float wallStickTime = 0.15f;
    [Min(0f)] public float wallSlideSpeedLoss = 0.05f;
    [Min(0f)] public float wallSlidingSpeedMax = 2f;
    public bool canWallClimb = false;
    [Min(0f)] public float wallClimbSpeed = 2f;
    public bool canWallJump = true;
    public Vector2 wallClimbForce = new Vector2(12f, 16f);
    public Vector2 wallOffForce = new Vector2(8f, 15f);
    public Vector2 wallLeapForce = new Vector2(18f, 17f);
    public bool canGrabLedge = false;
    [Min(0f)] public float ledgeDetectionOffset = 0.1f;

    [Header("Controller - Recoil")]
    [Tooltip("Acceleration toward the input speed while recoiling on the ground with movement input.")]
    [Min(0f)] public float recoilGroundAcceleration = 60f;
    [Tooltip("Deceleration while recoiling on the ground without movement input.")]
    [Min(0f)] public float recoilGroundDeceleration = 70f;
    [Tooltip("Acceleration toward the input speed while recoiling in the air with movement input.")]
    [Min(0f)] public float recoilAirAcceleration = 25f;
    [Tooltip("Deceleration while recoiling in the air without movement input. Low values preserve momentum.")]
    [Min(0f)] public float recoilAirDeceleration = 2f;
    [Tooltip("Maximum combined horizontal and vertical speed after an impulse is applied.")]
    [Min(0f)] public float recoilMaxPlanarSpeed = 18f;
    [Tooltip("Cancel falling speed before an upward impulse, so shooting downward always launches the character up.")]
    public bool cancelFallingOnUpwardImpulse = true;

    /// <summary>
    /// Tells listeners (e.g. the movement controller) that values were changed from code.
    /// </summary>
    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private void OnValidate()
    {
        NotifyChanged();
    }
}
