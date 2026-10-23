using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional leap attack for an enemy. EnemyBehavior starts it when the cooldown is ready and the player is in range,
/// and drives the movement (crouch, jump at the player, land past them). This component keeps the cooldown and
/// deals the damage: while the enemy is in the air, an area in front of it hurts each target once.
/// While the cooldown runs, the enemy falls back to its normal EnemyAttack. Settings live in EntityData ("Jump Attack").
/// </summary>
public class JumpAttack : MonoBehaviour
{
    [Tooltip("Layers the damage area hits. Defaults to \"Player\" and \"Triggers\" if left empty.")]
    [SerializeField] private LayerMask targetLayers;

    private readonly Collider2D[] overlapBuffer = new Collider2D[8];
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
    private ContactFilter2D targetFilter;
    private float nextReadyTime;
    private float windupEndTime;

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
        if (targetLayers.value == 0)
            targetLayers = LayerMask.GetMask("Player", "Triggers");

        targetFilter = new ContactFilter2D { useTriggers = true };
        targetFilter.SetLayerMask(targetLayers);
    }

    public void Begin(EntityData data)
    {
        IsActive = true;
        windupEndTime = Time.time + data.jumpAttackWindup;
        alreadyHit.Clear();
        Started?.Invoke();
    }

    /// <summary>
    /// Damages everything in the area in front of the enemy that hasn't been hit by this jump yet.
    /// </summary>
    public void DealDamage(Collider2D self, int facingDirection, EntityData data)
    {
        int count = Physics2D.OverlapBox(GetHitboxCenter(self, facingDirection, data), data.jumpAttackHitboxSize, 0f, targetFilter, overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D other = overlapBuffer[i];
            if (other.transform.IsChildOf(transform)) continue;

            // Health can sit on a sibling child of the character, so search everything under its Rigidbody2D.
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null)
            {
                Transform character = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
                target = character.GetComponentInChildren<IDamageable>();
            }

            if (target == null || !alreadyHit.Add(target)) continue;

            target.TakeDamage(data.jumpAttackDamage);
        }
    }

    /// <summary>Ends the attack and starts the cooldown.</summary>
    public void End(EntityData data)
    {
        if (!IsActive) return;

        IsActive = false;
        nextReadyTime = Time.time + data.jumpAttackCooldown;
        Ended?.Invoke();
    }

    // The area starts at the enemy's center and reaches hitboxSize.x forward.
    public static Vector2 GetHitboxCenter(Collider2D self, int facingDirection, EntityData data)
    {
        return (Vector2)self.bounds.center + Vector2.right * (facingDirection * data.jumpAttackHitboxSize.x * 0.5f);
    }

    private void OnDisable()
    {
        IsActive = false;
    }
}
