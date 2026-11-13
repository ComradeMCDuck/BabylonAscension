using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A damage area: put it on an object with a Collider2D (e.g. a BoxCollider2D) and size the collider in the scene.
/// The collider is only enabled between Activate and Deactivate. While active it damages the targets it overlaps,
/// either once per activation (melee attacks: one swing can never hit the same target twice) or repeatedly, with a
/// cooldown per target (hazards like spikes or fire). With activeOnEnable it switches itself on, so a level hazard
/// needs no attack script.
/// Hits are found with overlap checks every physics step instead of trigger callbacks, because the player and
/// enemies both use kinematic Rigidbody2Ds, which don't send trigger events to each other.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DamageHitbox : MonoBehaviour
{
    public enum HitMode
    {
        /// <summary>Each target is hit at most once between Activate and Deactivate.</summary>
        OncePerActivation,
        /// <summary>Each target is hit again every rehitInterval seconds while it stays inside.</summary>
        Repeating,
    }

    [Tooltip("Layers that can be damaged. Defaults to \"Player\" and \"Triggers\" (the Adventurer's layer) if left empty. Add \"Enemy\" to hurt enemies too.")]
    [SerializeField] private LayerMask targetLayers;

    [Tooltip("Once Per Activation: each target is hit once per attack (slashes, charges).\nRepeating: each target is hit again every Rehit Interval while it stays inside (hazards).")]
    [SerializeField] private HitMode hitMode = HitMode.OncePerActivation;

    [Tooltip("Repeating only: seconds before the same target can be hit again. The cooldown keeps running when the target leaves, so stepping out and back in doesn't give an extra hit.")]
    [Min(0.02f)]
    [SerializeField] private float rehitInterval = 0.5f;

    [Header("Standalone (level hazards)")]
    [Tooltip("Activates by itself whenever this object is enabled, using Damage below. Use for hazards with no attack script. Attack hitboxes (slash, charge...) leave this off; their attack activates them.")]
    [SerializeField] private bool activeOnEnable;

    [Tooltip("Damage per hit when Active On Enable is used. Attacks pass their own damage to Activate instead.")]
    [Min(0f)]
    [SerializeField] private float damage = 1f;

    [Header("Debug")]
    [Tooltip("Shows the debug sprite while the hitbox is active.")]
    [SerializeField] private bool showDebugSprite = true;
    [Tooltip("Sprite shown only while the hitbox is active. If empty, the first SpriteRenderer on this object or its children is used.")]
    [SerializeField] private SpriteRenderer debugSprite;

    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    // OncePerActivation: targets already hit by this activation.
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
    // Repeating: the time each target can be hit again.
    private readonly Dictionary<IDamageable, float> nextHitTime = new Dictionary<IDamageable, float>();
    private Collider2D hitbox;
    private ContactFilter2D targetFilter;
    private Transform owner;
    private float currentDamage;

    public bool IsActive { get; private set; }
    public HitMode Mode => hitMode;

    private void Reset()
    {
        // Keep the hitbox off the Enemy layer, otherwise the player's arrows would hit (and damage the enemy through) it.
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        hitbox = GetComponent<Collider2D>();
        // A trigger, so it never pushes anything; damage only comes from the overlap checks below.
        hitbox.isTrigger = true;
        hitbox.enabled = false;

        if (debugSprite == null)
            debugSprite = GetComponentInChildren<SpriteRenderer>(true);
        SetDebugSpriteVisible(false);

        if (targetLayers.value == 0)
            targetLayers = LayerMask.GetMask("Player", "Triggers");

        targetFilter = new ContactFilter2D { useTriggers = true };
        targetFilter.SetLayerMask(targetLayers);

        // The Adventurer sits on the "Triggers" layer, not "Player", which is easy to miss.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && (targetLayers.value & (1 << player.layer)) == 0)
            Debug.LogWarning($"{name}: Target Layers doesn't include the player's layer \"{LayerMask.LayerToName(player.layer)}\", so this hitbox can't damage the player.", this);

        // The character this hitbox belongs to, so it never damages itself. A standalone hazard has no owner:
        // falling back to transform.root would make it ignore everything under its level parent (e.g. enemies).
        Rigidbody2D body = GetComponentInParent<Rigidbody2D>();
        if (body != null) owner = body.transform;
        else if (!activeOnEnable) owner = transform.root;
    }

    private void OnEnable()
    {
        if (activeOnEnable) Activate(damage);
    }

    /// <summary>Enables the collider for a new attack. Targets hit by earlier activations can be hit again.</summary>
    public void Activate(float attackDamage) => Activate(attackDamage, 0);

    /// <summary>
    /// Like Activate, but this attack can also hit extraTargetLayers (e.g. "Enemy" for friendly fire).
    /// The extra layers only last until the next Activate.
    /// </summary>
    public void Activate(float attackDamage, LayerMask extraTargetLayers)
    {
        targetFilter.SetLayerMask(targetLayers | extraTargetLayers);
        currentDamage = attackDamage;
        alreadyHit.Clear();
        nextHitTime.Clear();
        hitbox.enabled = true;
        IsActive = true;
        SetDebugSpriteVisible(true);
        CheckHits();
    }

    public void Deactivate()
    {
        IsActive = false;
        if (hitbox != null) hitbox.enabled = false;
        SetDebugSpriteVisible(false);
    }

    private void SetDebugSpriteVisible(bool visible)
    {
        if (debugSprite != null) debugSprite.enabled = visible && showDebugSprite;
    }

    private void FixedUpdate()
    {
        if (IsActive) CheckHits();
    }

    /// <summary>
    /// Damages targets overlapping the hitbox right now that are allowed to be hit (see HitMode). Runs every physics
    /// step while active; call it after moving the hitbox to check its new position straight away.
    /// </summary>
    public void CheckHits()
    {
        if (!IsActive) return;

        // The attack may have moved or rotated the hitbox since the last physics step.
        Physics2D.SyncTransforms();

        overlaps.Clear();
        hitbox.Overlap(targetFilter, overlaps);

        foreach (Collider2D other in overlaps)
        {
            if (owner != null && other.transform.IsChildOf(owner)) continue;

            IDamageable target = FindDamageable(other);
            if (target == null || !CanHit(target)) continue;

            // No owner means a level hazard, not someone's attack.
            if (owner == null && target is IHazardDamageable hazardTarget) hazardTarget.TakeHazardDamage(currentDamage);
            else target.TakeDamage(currentDamage);
        }
    }

    /// <summary>
    /// Checks whether target may be hit now and, if so, records the hit. A character with several colliders
    /// overlapping is still hit only once per step, because the first collider records it.
    /// </summary>
    private bool CanHit(IDamageable target)
    {
        if (hitMode == HitMode.OncePerActivation)
            return alreadyHit.Add(target);

        if (nextHitTime.TryGetValue(target, out float readyTime) && Time.time < readyTime)
            return false;

        nextHitTime[target] = Time.time + rehitInterval;
        return true;
    }

    private static IDamageable FindDamageable(Collider2D other)
    {
        // Health can sit on a sibling child of the character, so search everything under its Rigidbody2D.
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null) return target;

        Transform character = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
        return character.GetComponentInChildren<IDamageable>();
    }

    private void OnDisable()
    {
        Deactivate();
    }
}
