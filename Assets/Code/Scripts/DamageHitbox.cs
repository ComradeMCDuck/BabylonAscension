using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A damage area for melee attacks: put it on a child object with a Collider2D (e.g. a BoxCollider2D) and size the
/// collider in the scene. The collider is only enabled between Activate and Deactivate, and while active it damages
/// every target it overlaps once per activation, so one attack can never hit the same target twice.
/// Hits are found with overlap checks every physics step instead of trigger callbacks, because the player and
/// enemies both use kinematic Rigidbody2Ds, which don't send trigger events to each other.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DamageHitbox : MonoBehaviour
{
    [Tooltip("Layers that can be damaged. Defaults to \"Player\" and \"Triggers\" (the Adventurer's layer) if left empty.")]
    [SerializeField] private LayerMask targetLayers;

    [Header("Debug")]
    [Tooltip("Shows the debug sprite while the hitbox is active.")]
    [SerializeField] private bool showDebugSprite = true;
    [Tooltip("Sprite shown only while the hitbox is active. If empty, the first SpriteRenderer on this object or its children is used.")]
    [SerializeField] private SpriteRenderer debugSprite;

    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
    private Collider2D hitbox;
    private ContactFilter2D targetFilter;
    private Transform owner;
    private float damage;

    public bool IsActive { get; private set; }

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

        // The character this hitbox belongs to, so it never damages itself.
        Rigidbody2D body = GetComponentInParent<Rigidbody2D>();
        owner = body != null ? body.transform : transform.root;
    }

    /// <summary>Enables the collider for a new attack. Targets hit by earlier attacks can be hit again.</summary>
    public void Activate(float attackDamage)
    {
        damage = attackDamage;
        alreadyHit.Clear();
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
    /// Damages new targets overlapping the hitbox right now. Runs every physics step while active;
    /// call it after moving the hitbox to check its new position straight away.
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
            if (other.transform.IsChildOf(owner)) continue;

            IDamageable target = FindDamageable(other);
            if (target == null || !alreadyHit.Add(target)) continue;

            target.TakeDamage(damage);
        }
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
