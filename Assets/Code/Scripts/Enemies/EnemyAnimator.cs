using DYP;
using UnityEngine;

/// <summary>
/// Feeds a ground enemy's movement into its Animator, for idle/walk blend trees. MoveX is -1 (walking left),
/// 0 (standing) or 1 (walking right), eased by the damp time. It is not the fraction of top speed: sprite clips
/// can't be mixed, so a blend tree shows whichever clip weighs more, and a slow patrol (e.g. 35% speed) would show idle.
/// With a ChargeAttack (Humbaba), the Charging bool is true during the charge's windup only.
/// The basic attack animation is driven separately by the enemy's EnemyAttack (its Animator field and IsAttacking bool).
/// </summary>
public class EnemyAnimator : MonoBehaviour
{
    [Tooltip("Animator with the movement blend tree. Empty = the first Animator in the children.")]
    [SerializeField] private Animator animator;
    [Tooltip("Ground controller whose velocity is shown. Empty = the one on this object or a parent.")]
    [SerializeField] private BasicMovementController2D movement;
    [Tooltip("Float parameter set to -1 while walking left, 1 while walking right and 0 while standing.")]
    [SerializeField] private string moveXParameter = "MoveX";
    [Tooltip("Seconds MoveX takes to follow the velocity. Smooths the blend between idle and walk. 0 = instant.")]
    [SerializeField, Min(0f)] private float dampTime = 0.1f;
    [Tooltip("Speed, as a fraction of the walk speed, below which the enemy counts as standing still (MoveX = exactly 0, idle). " +
             "Keep it below EntityData's Patrol Speed Multiplier, or patrolling shows idle.")]
    [SerializeField, Range(0f, 0.5f)] private float idleThreshold = 0.1f;

    [Header("Charge (optional)")]
    [Tooltip("Charge attack whose state is shown. Empty = the one on this object or a parent; none = no charge animation.")]
    [SerializeField] private ChargeAttack chargeAttack;
    [Tooltip("Bool parameter that is true during the charge attack's windup. Leave the Charge state by exit time.")]
    [SerializeField] private string chargingParameter = "Charging";

    private int moveXHash;
    private int chargingHash;
    private bool driveCharging;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (movement == null) movement = GetComponentInParent<BasicMovementController2D>();
        if (chargeAttack == null) chargeAttack = GetComponentInParent<ChargeAttack>();

        if (animator == null || movement == null)
        {
            Debug.LogError($"{name}: EnemyAnimator needs an Animator and a BasicMovementController2D. Disabling.", this);
            enabled = false;
            return;
        }

        moveXHash = Animator.StringToHash(moveXParameter);
        if (!HasParameter(moveXHash, AnimatorControllerParameterType.Float))
        {
            Debug.LogError($"{name}: Animator has no float parameter \"{moveXParameter}\". Disabling EnemyAnimator.", this);
            enabled = false;
            return;
        }

        if (chargeAttack != null)
        {
            chargingHash = Animator.StringToHash(chargingParameter);
            driveCharging = HasParameter(chargingHash, AnimatorControllerParameterType.Bool);
            if (!driveCharging)
                Debug.LogWarning($"{name}: has a ChargeAttack but the Animator has no bool parameter \"{chargingParameter}\"; the charge plays the movement animation.", this);
        }
    }

    private void Update()
    {
        // MoveSpeed is 0 when EntityData sets no walk speed; avoid dividing by it.
        float speed = movement.MoveSpeed;
        float speedFraction = speed > 0f ? movement.InputVelocity.x / speed : 0f;
        // Any real movement counts as walking at full blend, whatever the speed (patrol, chase).
        float moveX = Mathf.Abs(speedFraction) < idleThreshold ? 0f : Mathf.Sign(speedFraction);

        // Damping only approaches the target, so once stopping has eased into the dead zone, snap to exactly 0.
        if (moveX == 0f && Mathf.Abs(animator.GetFloat(moveXHash)) < idleThreshold)
            animator.SetFloat(moveXHash, 0f);
        else
            animator.SetFloat(moveXHash, moveX, dampTime, Time.deltaTime);

        // Windup only: the Animator plays the charge clip once and returns to Movement on its own (exit time),
        // so the run itself shows the walk. Kept false afterwards so it doesn't re-enter the Charge state.
        if (driveCharging) animator.SetBool(chargingHash, chargeAttack.IsActive && !chargeAttack.WindupFinished);
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash && parameter.type == type)
                return true;
        return false;
    }
}
