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
    public float damageIndicationDuration = 0.1f;

    [Header("Movement")]
    [Tooltip("Enemies: radius of the line-of-sight circle.")]
    public float detectionRange = 5f;
    public float moveSpeed = 4f;
    public float jumpSpeed = 1.0f;
    public float dashSpeed = 10f;
    public float dashDuration = 0.2f;
    public float nextDashCooldown = 1f;
    public bool lockZAxis = true;

    [Header("Attack")]
    public float attackDamage = 1f;
    [Tooltip("Enemies: gap between the enemy's and the player's colliders at which the enemy attacks.")]
    public float attackRange = 2f;
    public float attackCooldown = 5f;
    [Tooltip("Enemies: projectile speed.")]
    public float attackSpeed = 1f;
    public GameObject attackEffectPrefab;

    [Header("Soul Drop")]
    [Tooltip("Enemies: soul orb dropped where the enemy dies. Must have a Soul component; its value is set on the prefab. Empty = drops nothing.")]
    public GameObject soulPrefab;

    [Header("Enemy AI")]
    [Tooltip("Patrol speed as a fraction of 'speed' (Controller - Movement Settings). Chasing uses the full speed.")]
    [Range(0f, 1f)] public float patrolSpeedMultiplier = 0.4f;
    [Tooltip("Seconds the enemy waits at a patrol point before turning around.")]
    [Min(0f)] public float patrolWaitTime = 1f;
    [Tooltip("Seconds the enemy keeps chasing the player's last known position after losing sight of them.")]
    [Min(0f)] public float loseSightTime = 2f;
    [Tooltip("If true, the enemy jumps onto platforms, over walls and across gaps while chasing. Jump height comes from 'Controller - Jump'.")]
    public bool canJump = true;
    [Tooltip("How close (horizontally) a wall, gap or platform edge must be before the enemy jumps.")]
    [Min(0f)] public float jumpCheckDistance = 1f;
    [Tooltip("Minimum seconds between two enemy jumps.")]
    [Min(0f)] public float jumpCooldown = 0.5f;
    [Tooltip("Delay between starting an attack and the projectile spawning (or the slash starting). Gives the player time to react.")]
    [Min(0f)] public float attackWindup = 0.2f;
    [Tooltip("Downward acceleration of the enemy's projectile. The speed (attackSpeed) is fixed, so the enemy aims higher to make up for it. 0 = flies straight.")]
    [Min(0f)] public float projectileGravity = 10f;
    [Min(0.1f)] public float projectileLifetime = 5f;

    [Header("Slash Attack (enemies with a SlashAttack component)")]
    [Tooltip("Point the hitbox swings around, relative to the enemy's position when facing right. Mirrored when facing left. Damage is attackDamage.")]
    public Vector2 slashOriginOffset = new Vector2(0.2f, 0.3f);
    [Tooltip("Total angle (degrees) the hitbox sweeps through, centered on the direction to the player. It swings from top to bottom.")]
    [Range(0f, 360f)] public float slashArcAngle = 140f;
    [Tooltip("Seconds the swing takes. The hitbox is active for this long.")]
    [Min(0.01f)] public float slashDuration = 0.25f;

    [Header("Jump Attack (enemies with a JumpAttack component)")]
    public float jumpAttackDamage = 30f;
    [Tooltip("Seconds after a jump attack before the next one. The basic attack is used in the meantime.")]
    [Min(0f)] public float jumpAttackCooldown = 6f;
    [Tooltip("Max horizontal distance to the player at which the enemy starts a jump attack.")]
    [Min(0f)] public float jumpAttackRange = 4f;
    [Tooltip("How far past the player's center the enemy lands (and stops).")]
    [Min(0f)] public float jumpAttackOvershoot = 1.5f;
    [Tooltip("Lowest peak height of the lunge, separate from the normal jump height. Lower = flatter and faster lunge. Raised automatically when the lunge would need more than Max Lunge Speed.")]
    [Min(0.05f)] public float jumpAttackLungeHeight = 0.8f;
    [Tooltip("Fastest horizontal lunge speed. Far lunges that would need more use a higher arc instead. Not limited by Recoil Max Planar Speed.")]
    [Min(0f)] public float jumpAttackMaxLungeSpeed = 16f;
    [Tooltip("Crouch time before the leap. Gives the player time to react.")]
    [Min(0f)] public float jumpAttackWindup = 0.3f;
    [Tooltip("The player can be at most this much higher or lower than the enemy for it to jump attack.")]
    [Min(0f)] public float jumpAttackMaxHeightDifference = 1.5f;

    [Header("Charge Attack (enemies with a ChargeAttack component)")]
    public float chargeDamage = 40f;
    [Tooltip("Seconds after a charge ends before the next one. The basic attack is used in the meantime.")]
    [Min(0f)] public float chargeCooldown = 8f;
    [Tooltip("Max horizontal distance to the player at which the enemy starts a charge.")]
    [Min(0f)] public float chargeRange = 7f;
    [Tooltip("The player can be at most this much higher or lower than the enemy for it to charge.")]
    [Min(0f)] public float chargeMaxHeightDifference = 1f;
    [Tooltip("Time the enemy stands still facing the player before running. Gives the player time to react.")]
    [Min(0f)] public float chargeWindup = 0.6f;
    [Tooltip("Running speed during the charge. Independent of the normal movement speed.")]
    [Min(0.01f)] public float chargeSpeed = 14f;
    [Tooltip("The charge stops (without a stun) after running this far, or earlier at a ledge.")]
    [Min(0f)] public float chargeDistance = 12f;
    [Tooltip("Seconds the enemy is stunned (can't move or attack) after charging into a wall.")]
    [Min(0f)] public float chargeStunDuration = 3f;
    [Tooltip("The charge also damages other enemies in its path (once each per charge, with chargeDamage).")]
    public bool chargeDamagesEnemies = true;

    [Header("Flight (enemies with an EnemyFlight component)")]
    [Tooltip("Top flying speed.")]
    [Min(0.01f)] public float flySpeed = 6f;
    [Tooltip("How quickly the flying speed changes. Lower = floatier, swings wider around corners.")]
    [Min(0.01f)] public float flyAcceleration = 20f;
    [Tooltip("Distance to the player the enemy tries to keep while flying around them. Keep it below attackRange.")]
    [Min(0f)] public float flyPreferredDistance = 6f;
    [Tooltip("Closest the enemy hovers to the player when walls leave no room at the preferred distance.")]
    [Min(0f)] public float flyMinDistance = 3f;
    [Tooltip("The enemy circles over the player between these angles (degrees, 0 = right of the player, 90 = straight above, 180 = left).")]
    [Range(-90f, 270f)] public float flyMinAngle = 30f;
    [Range(-90f, 270f)] public float flyMaxAngle = 150f;
    [Tooltip("How fast (degrees per second) the enemy circles over the player.")]
    [Min(0f)] public float flyOrbitSpeed = 20f;
    [Tooltip("Height of the up and down wing-flap motion.")]
    [Min(0f)] public float flyBobAmplitude = 0.25f;
    [Tooltip("Wing flaps per second.")]
    [Min(0f)] public float flyBobFrequency = 2f;

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

    [Header("Controller - Knockback (when hit)")]
    [Tooltip("Seconds movement and jump input are ignored after being knocked back, so the character can't instantly walk out of it.")]
    [Min(0f)] public float knockbackStunTime = 0.3f;
    [Tooltip("Upward speed added when knocked back while on the ground, so the hit pops the character up and it slides with air deceleration.")]
    [Min(0f)] public float knockbackLift = 4f;

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
