using System;
using UnityEngine;

/// <summary>
/// Optional leap attack for an enemy. EnemyBehavior starts it when the cooldown is ready and the player is in range,
/// and drives the movement (crouch, jump at the player, land past them). This component keeps the cooldown and
/// owns the damage: when the leap starts, a DamageHitbox in front of the enemy is enabled. It keeps its place
/// relative to the enemy for the whole leap, hurts each target at most once, and is disabled on landing.
/// While the cooldown runs, the enemy falls back to its normal EnemyAttack. Settings live in EntityData ("Jump Attack").
/// </summary>
public class JumpAttack : MonoBehaviour
{
    [Tooltip("Hitbox on a child of the enemy, placed in front of it as if facing right. Mirrored to the side the enemy leaps toward.")]
    [SerializeField] private DamageHitbox hitbox;

    private float nextReadyTime;
    private float windupEndTime;
    private Vector3 hitboxLocalPosition;

    /// <summary>True from the start of the windup until the enemy lands.</summary>
    public bool IsActive { get; private set; }
    public bool IsReady => !IsActive && Time.time >= nextReadyTime;
    public bool WindupFinished => Time.time >= windupEndTime;

    /// <summary>Raised when the windup starts. Hook animations or sounds here.</summary>
    public event Action Started;
    /// <summary>Raised when the enemy lands (or the attack is cancelled).</summary>
    public event Action Ended;

    private void Awake()
    {
        if (hitbox == null)
            Debug.LogError($"{name}: JumpAttack needs a Hitbox assigned.", this);
        else
            hitboxLocalPosition = hitbox.transform.localPosition;
    }

    public void Begin(EntityData data)
    {
        IsActive = true;
        windupEndTime = Time.time + data.jumpAttackWindup;
        Started?.Invoke();
    }

    /// <summary>Enables the hitbox in front of the enemy, on the side it leaps toward. Call when the leap starts.</summary>
    public void StartLeap(int facingDirection, EntityData data)
    {
        if (hitbox == null) return;

        hitbox.transform.localPosition = new Vector3(hitboxLocalPosition.x * facingDirection, hitboxLocalPosition.y, hitboxLocalPosition.z);
        hitbox.Activate(data.jumpAttackDamage);
    }

    /// <summary>Ends the attack, disables the hitbox and starts the cooldown.</summary>
    public void End(EntityData data)
    {
        if (!IsActive) return;

        IsActive = false;
        if (hitbox != null) hitbox.Deactivate();
        nextReadyTime = Time.time + data.jumpAttackCooldown;
        Ended?.Invoke();
    }

    private void OnDisable()
    {
        IsActive = false;
        if (hitbox != null) hitbox.Deactivate();
    }
}
