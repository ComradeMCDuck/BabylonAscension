using System;
using UnityEngine;

/// <summary>
/// Optional bull charge for an enemy (Humbaba). EnemyBehavior starts it when the cooldown is ready and the player is
/// in range, and drives the movement (windup, then a straight run at chargeSpeed). This component keeps the cooldown,
/// the stun timer and owns the damage: while charging, a DamageHitbox in front of the enemy is enabled and hurts each
/// target at most once, without stopping the charge. With chargeDamagesEnemies it also hurts other enemies it runs
/// through (they never block the charge, because EnemyBehavior's wall check ignores the Enemy layer). The charge ends after chargeDistance or at a ledge; running into
/// a wall ends it and stuns the enemy for chargeStunDuration. While the cooldown runs, the enemy falls back to its
/// normal EnemyAttack. Settings live in EntityData ("Charge Attack").
/// </summary>
public class ChargeAttack : MonoBehaviour
{
    [Tooltip("Hitbox on a child of the enemy, placed in front of it as if facing right. Mirrored to the side the enemy charges toward.")]
    [SerializeField] private DamageHitbox hitbox;

    private float nextReadyTime;
    private float windupEndTime;
    private float stunEndTime;
    private Vector3 hitboxLocalPosition;

    /// <summary>True from the start of the windup until the charge ends (the stun afterwards is not included).</summary>
    public bool IsActive { get; private set; }
    public bool IsReady => !IsActive && Time.time >= nextReadyTime;
    public bool WindupFinished => Time.time >= windupEndTime;
    public bool IsStunned => Time.time < stunEndTime;

    /// <summary>Raised when the windup starts. Hook animations or sounds here.</summary>
    public event Action Started;
    /// <summary>Raised when the windup is over and the enemy starts running.</summary>
    public event Action ChargeStarted;
    /// <summary>Raised when the charge ends, for any reason.</summary>
    public event Action Ended;
    /// <summary>Raised when the enemy runs into a wall and gets stunned.</summary>
    public event Action StunStarted;

    private void Awake()
    {
        if (hitbox == null)
            Debug.LogError($"{name}: ChargeAttack needs a Hitbox assigned.", this);
        else
            hitboxLocalPosition = hitbox.transform.localPosition;
    }

    public void Begin(EntityData data)
    {
        IsActive = true;
        windupEndTime = Time.time + data.chargeWindup;
        Started?.Invoke();
    }

    /// <summary>Enables the hitbox in front of the enemy, on the side it charges toward. Call when the run starts.</summary>
    public void StartCharge(int facingDirection, EntityData data)
    {
        ChargeStarted?.Invoke();
        if (hitbox == null) return;

        hitbox.transform.localPosition = new Vector3(hitboxLocalPosition.x * facingDirection, hitboxLocalPosition.y, hitboxLocalPosition.z);
        LayerMask extraLayers = data.chargeDamagesEnemies ? LayerMask.GetMask("Enemy") : 0;
        hitbox.Activate(data.chargeDamage, extraLayers);
    }

    /// <summary>Ends the charge, disables the hitbox and starts the cooldown. hitWall also starts the stun.</summary>
    public void End(EntityData data, bool hitWall)
    {
        if (!IsActive) return;

        IsActive = false;
        if (hitbox != null) hitbox.Deactivate();
        nextReadyTime = Time.time + data.chargeCooldown;
        Ended?.Invoke();

        if (hitWall)
        {
            stunEndTime = Time.time + data.chargeStunDuration;
            StunStarted?.Invoke();
        }
    }

    private void OnDisable()
    {
        IsActive = false;
        if (hitbox != null) hitbox.Deactivate();
    }
}
