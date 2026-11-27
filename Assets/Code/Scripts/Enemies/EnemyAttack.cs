using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Base class for enemy attacks. Handles the windup and cooldown from EntityData;
/// subclasses only decide what actually happens (spawn a projectile, swing a hitbox, ...).
/// EnemyBehavior finds this component on the same GameObject and calls TryAttack.
/// With an Animator assigned, the attack waits for the animation: it sets the attacking bool and waits until the Animator
/// is playing the attack state (e.g. after a looping fly cycle with exit time ends). By default the windup comes first,
/// so the animation and the attack itself (e.g. the slash hitbox) start together after EntityData.attackWindup.
/// With windupBeforeAnimation off, the animation starts first and the windup is measured from its first frame,
/// for attacks that must land on a particular frame of the animation (e.g. releasing an arrow).
/// </summary>
public abstract class EnemyAttack : MonoBehaviour
{
    // Longest wait for the Animator to reach the attack state. Past this the attack goes ahead anyway,
    // so a wrong state name or a missing transition can't stop the enemy from ever attacking again.
    private const float MaxAnimationWait = 2f;

    [Header("Aiming")]
    [Tooltip("Aim at the player's last known position right before the attack lands (after the windup and the wait for the animation) " +
             "instead of when the attack starts. Use it when the attack waits a long time, so it doesn't fire at where the player used to be.")]
    [SerializeField] private bool aimAtWindupEnd;

    [Header("Animation (optional)")]
    [Tooltip("Animator that plays the attack. Leave empty to attack without waiting for an animation.")]
    [SerializeField] private Animator animator;
    [Tooltip("Bool parameter set while the enemy wants to attack. Cleared once the attack state starts, so the attack plays once.")]
    [SerializeField] private string attackingParameter = "IsAttacking";
    [Tooltip("Name of the Animator state that plays the attack. The attack (or, with Windup Before Animation off, the windup) starts when the Animator enters it.")]
    [SerializeField] private string attackStateName = "Attack";
    [Tooltip("On: the enemy waits out the windup first, then the attack animation and the attack itself start together. " +
             "Off: the animation starts first and the windup is counted from its first frame. Use it when the attack has to " +
             "happen on a particular frame of the animation (e.g. a bow release).")]
    [SerializeField] private bool windupBeforeAnimation = true;

    private float nextAttackTime;
    private bool animatorChecked;
    private int attackingParameterHash;
    private int attackStateHash;

    /// <summary>
    /// True from the start of the windup until the attack is over. EnemyBehavior does not leave the Attack state
    /// or turn the enemy around while this is set.
    /// </summary>
    public bool IsAttacking { get; private set; }

    /// <summary>Raised when the attack is decided, before waiting for the animation and the windup. Hook sounds here.</summary>
    public event Action AttackStarted;

    /// <summary>
    /// Starts an attack if the cooldown has passed. getTargetPosition is read once: when the attack starts, or at the
    /// end of the windup with aimAtWindupEnd. Either way the target is fixed before the attack lands, so the player can dodge it.
    /// </summary>
    public bool TryAttack(Func<Vector2> getTargetPosition, EntityData data)
    {
        if (IsAttacking || Time.time < nextAttackTime) return false;

        StartCoroutine(AttackRoutine(getTargetPosition, data));
        return true;
    }

    private IEnumerator AttackRoutine(Func<Vector2> getTargetPosition, EntityData data)
    {
        IsAttacking = true;
        Vector2 targetPosition = getTargetPosition();
        AttackStarted?.Invoke();

        if (windupBeforeAnimation) yield return Windup(data);

        if (HasValidAnimator())
        {
            animator.SetBool(attackingParameterHash, true);
            yield return WaitForAttackState();
            animator.SetBool(attackingParameterHash, false);
        }

        if (!windupBeforeAnimation) yield return Windup(data);
        if (aimAtWindupEnd) targetPosition = getTargetPosition();

        yield return StartCoroutine(PerformAttackRoutine(targetPosition, data));
        nextAttackTime = Time.time + data.attackCooldown;
        IsAttacking = false;
    }

    private static IEnumerator Windup(EntityData data)
    {
        if (data.attackWindup > 0f) yield return new WaitForSeconds(data.attackWindup);
    }

    /// <summary>
    /// Checks the Animator setup once (lazily, since subclasses have their own private Awake).
    /// A missing parameter is logged and the attack falls back to running without the animation.
    /// </summary>
    private bool HasValidAnimator()
    {
        if (animatorChecked) return animator != null;
        animatorChecked = true;

        if (animator == null) return false;

        attackingParameterHash = Animator.StringToHash(attackingParameter);
        attackStateHash = Animator.StringToHash(attackStateName);

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == attackingParameterHash && parameter.type == AnimatorControllerParameterType.Bool)
                return true;
        }

        Debug.LogError($"{name}: Animator has no bool parameter \"{attackingParameter}\". Attacking without the animation.", this);
        animator = null;
        return false;
    }

    private IEnumerator WaitForAttackState()
    {
        float giveUpTime = Time.time + MaxAnimationWait;

        while (!IsInAttackState())
        {
            if (Time.time >= giveUpTime)
            {
                Debug.LogWarning($"{name}: Animator never reached the state \"{attackStateName}\" after setting \"{attackingParameter}\". Check the state name and the transition.", this);
                yield break;
            }

            yield return null;
        }
    }

    // Also true while the Animator is blending into the attack state, since the attack clip is already playing then.
    private bool IsInAttackState()
    {
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == attackStateHash) return true;
        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == attackStateHash;
    }

    /// <summary>Instant attacks (e.g. spawning a projectile) override this.</summary>
    protected virtual void PerformAttack(Vector2 targetPosition, EntityData data) { }

    /// <summary>Attacks that take time (e.g. a sword swing) override this instead. The cooldown starts when it ends.</summary>
    protected virtual IEnumerator PerformAttackRoutine(Vector2 targetPosition, EntityData data)
    {
        PerformAttack(targetPosition, data);
        yield break;
    }

    // Disabling a MonoBehaviour stops its coroutines, so the windup would never finish.
    protected virtual void OnDisable()
    {
        IsAttacking = false;
        if (animator != null && animatorChecked && animator.isActiveAndEnabled) animator.SetBool(attackingParameterHash, false);
    }
}
