using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Base class for enemy attacks. Handles the windup and cooldown from EntityData;
/// subclasses only decide what actually happens (spawn a projectile, swing a hitbox, ...).
/// EnemyBehavior finds this component on the same GameObject and calls TryAttack.
/// </summary>
public abstract class EnemyAttack : MonoBehaviour
{
    private float nextAttackTime;

    /// <summary>
    /// True from the start of the windup until the attack is over. EnemyBehavior does not leave the Attack state
    /// or turn the enemy around while this is set.
    /// </summary>
    public bool IsAttacking { get; private set; }

    /// <summary>Raised when the windup starts. Hook animations or sounds here.</summary>
    public event Action AttackStarted;

    /// <summary>
    /// Starts an attack aimed at targetPosition if the cooldown has passed.
    /// The target is fixed when the windup starts, so the player can dodge it.
    /// </summary>
    public bool TryAttack(Vector2 targetPosition, EntityData data)
    {
        if (IsAttacking || Time.time < nextAttackTime) return false;

        StartCoroutine(AttackRoutine(targetPosition, data));
        return true;
    }

    private IEnumerator AttackRoutine(Vector2 targetPosition, EntityData data)
    {
        IsAttacking = true;
        AttackStarted?.Invoke();

        if (data.attackWindup > 0f) yield return new WaitForSeconds(data.attackWindup);

        yield return StartCoroutine(PerformAttackRoutine(targetPosition, data));
        nextAttackTime = Time.time + data.attackCooldown;
        IsAttacking = false;
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
    }
}
