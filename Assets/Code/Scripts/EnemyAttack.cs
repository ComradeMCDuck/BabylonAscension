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

    /// <summary>True during the windup. EnemyBehavior does not leave the Attack state while this is set.</summary>
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

        PerformAttack(targetPosition, data);
        nextAttackTime = Time.time + data.attackCooldown;
        IsAttacking = false;
    }

    protected abstract void PerformAttack(Vector2 targetPosition, EntityData data);

    // Disabling a MonoBehaviour stops its coroutines, so the windup would never finish.
    protected virtual void OnDisable()
    {
        IsAttacking = false;
    }
}
