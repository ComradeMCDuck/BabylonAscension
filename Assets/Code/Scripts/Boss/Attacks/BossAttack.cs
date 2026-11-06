using System;
using System.Collections;
using UnityEngine;

public enum BossAttackPhase
{
    /// <summary>Telegraph: the boss stands still facing the player.</summary>
    Windup,
    /// <summary>The attack is happening (hitboxes out, charging, projectiles flying).</summary>
    Active,
    /// <summary>Stands still after the attack, before moving again.</summary>
    Recovery,
    /// <summary>Not attacking.</summary>
    Finished
}

/// <summary>Where the player is relative to the boss, measured when the brain picks an attack.</summary>
public readonly struct BossTargetInfo
{
    /// <summary>Center of the player's collider.</summary>
    public readonly Vector2 PlayerCenter;
    /// <summary>Shortest distance between the boss's and the player's colliders. 0 while they overlap.</summary>
    public readonly float Gap;
    /// <summary>Gap between the colliders on X only. 0 while they overlap on X.</summary>
    public readonly float HorizontalGap;
    /// <summary>True while the colliders overlap, i.e. the player stands inside the boss.</summary>
    public readonly bool Inside;
    /// <summary>The player's feet minus the boss's feet. Positive = the player is higher.</summary>
    public readonly float HeightDifference;
    /// <summary>1 if the player is right of the boss's center, -1 if left.</summary>
    public readonly int Side;

    public BossTargetInfo(Vector2 playerCenter, float gap, float horizontalGap, bool inside, float heightDifference, int side)
    {
        PlayerCenter = playerCenter;
        Gap = gap;
        HorizontalGap = horizontalGap;
        Inside = inside;
        HeightDifference = heightDifference;
        Side = side;
    }
}

/// <summary>
/// One attack in a boss's attack bank. Put attack components on the boss (or its children); BossBrain finds them all
/// and picks one of those that fit (in range, off cooldown) by weight. Disable a component to take the attack out of
/// the bank. The base class runs windup, the attack, recovery and cooldown; subclasses only write Perform (what
/// happens after the windup) and Cleanup (switch hitboxes off, also when the attack is cancelled).
/// Tuning lives in a BossAttackData asset, so the same attack can be reused with other numbers on another boss.
/// </summary>
public abstract class BossAttack : MonoBehaviour
{
    private static readonly WaitForFixedUpdate s_WaitForFixedUpdate = new WaitForFixedUpdate();

    private float nextReadyTime;

    /// <summary>The attack's settings asset.</summary>
    public abstract BossAttackData BaseData { get; }

    /// <summary>True from the start of the windup until the recovery is over.</summary>
    public bool IsRunning { get; private set; }
    public bool IsReady => !IsRunning && BaseData != null && Time.time >= nextReadyTime;
    public BossAttackPhase Phase { get; private set; } = BossAttackPhase.Finished;

    /// <summary>
    /// True while the attack moves the boss itself (e.g. the charge). The brain doesn't stop the boss then,
    /// and it ignores knockback.
    /// </summary>
    public bool ControlsMovement { get; protected set; }

    /// <summary>Raised whenever the phase changes. Hook animations and sounds here.</summary>
    public event Action<BossAttack, BossAttackPhase> PhaseChanged;

    protected BossBrain Boss { get; private set; }

    protected virtual void Awake()
    {
        Boss = GetComponentInParent<BossBrain>();
        if (Boss == null)
        {
            Debug.LogError($"{name}: {GetType().Name} must be on a boss (a BossBrain on this object or a parent). Disabling.", this);
            enabled = false;
            return;
        }

        if (BaseData == null)
        {
            Debug.LogError($"{name}: {GetType().Name} has no data asset assigned. Disabling.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// How likely this attack is to be picked right now, 0 if it can't be used. The brain multiplies this with its
    /// own rules (e.g. not repeating the last attack).
    /// </summary>
    public float GetWeight(in BossTargetInfo target, bool bossOnGround)
    {
        BossAttackData data = BaseData;
        if (data == null || !IsReady || !isActiveAndEnabled) return 0f;
        if (data.requiresGround && !bossOnGround) return 0f;

        float weight;
        if (target.Inside)
        {
            weight = data.insideWeight;
        }
        else
        {
            if (target.Gap < data.minRange || target.Gap > data.maxRange) return 0f;
            if (Mathf.Abs(target.HeightDifference) > data.maxHeightDifference) return 0f;
            weight = data.weight;
        }

        return weight > 0f && CanUse(target) ? weight : 0f;
    }

    /// <summary>Extra conditions on top of range and height (e.g. room to charge). Called only when those already fit.</summary>
    protected virtual bool CanUse(in BossTargetInfo target) => true;

    /// <summary>Starts the attack. The brain faces the boss toward the player first; it doesn't turn until the attack ends.</summary>
    public bool Begin(in BossTargetInfo target)
    {
        if (!IsReady) return false;

        IsRunning = true;
        StartCoroutine(Run(target));
        return true;
    }

    /// <summary>
    /// Stops the attack straight away (e.g. the boss died), including anything it left running after it ended
    /// (see OnCancelled). The cooldown still starts.
    /// </summary>
    public void Cancel()
    {
        StopAllCoroutines();
        OnCancelled();
        if (!IsRunning) return;

        Cleanup();
        StartCooldown();
        Finish();
    }

    private IEnumerator Run(BossTargetInfo target)
    {
        SetPhase(BossAttackPhase.Windup);
        yield return WaitFixed(BaseData.windup);

        SetPhase(BossAttackPhase.Active);
        yield return Perform(target);
        Cleanup();
        StartCooldown();

        SetPhase(BossAttackPhase.Recovery);
        yield return WaitFixed(BaseData.recovery);

        Finish();
    }

    /// <summary>
    /// The attack itself, after the windup. Runs as a coroutine; wait with WaitFixed or yield WaitForFixedUpdate,
    /// so it follows the physics steps (and stops while the game is paused).
    /// </summary>
    protected abstract IEnumerator Perform(BossTargetInfo target);

    /// <summary>Switches hitboxes off and removes leftovers. Runs after Perform and when the attack is cancelled.</summary>
    protected virtual void Cleanup() { }

    /// <summary>
    /// Called by Cancel, also when the attack itself is already over. Attacks that keep something going after the
    /// boss moves on (e.g. the roar's projectiles) remove it here.
    /// </summary>
    protected virtual void OnCancelled() { }

    /// <summary>Waits the given seconds in physics steps.</summary>
    protected static IEnumerator WaitFixed(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return s_WaitForFixedUpdate;
            elapsed += Time.fixedDeltaTime;
        }
    }

    protected static WaitForFixedUpdate NextFixedUpdate => s_WaitForFixedUpdate;

    /// <summary>
    /// World point for an offset given relative to the boss's feet (bottom center of its collider) when facing right.
    /// Mirrored when the boss faces left.
    /// </summary>
    protected Vector2 FromFeet(Vector2 offset) => FromFeet(offset, Boss.FeetCenter, Boss.FacingDirection);

    protected static Vector2 FromFeet(Vector2 offset, Vector2 feet, int facing) =>
        feet + new Vector2(offset.x * facing, offset.y);

    /// <summary>
    /// Feet position and facing for drawing gizmos, also in edit mode (facing right then).
    /// </summary>
    protected bool TryGetGizmoFrame(out Vector2 feet, out int facing)
    {
        BossBrain brain = Boss != null ? Boss : GetComponentInParent<BossBrain>();
        feet = default;
        facing = 1;
        if (brain == null) return false;

        feet = brain.FeetCenter;
        facing = Application.isPlaying ? brain.FacingDirection : 1;
        return true;
    }

    private void StartCooldown()
    {
        if (BaseData != null) nextReadyTime = Time.time + BaseData.cooldown;
    }

    private void Finish()
    {
        IsRunning = false;
        ControlsMovement = false;
        SetPhase(BossAttackPhase.Finished);
    }

    private void SetPhase(BossAttackPhase phase)
    {
        if (Phase == phase) return;
        Phase = phase;
        PhaseChanged?.Invoke(this, phase);
    }

    // Disabling a MonoBehaviour stops its coroutines, so clean up instead of leaving hitboxes switched on.
    protected virtual void OnDisable()
    {
        Cancel();
    }
}

/// <summary>
/// Base for attacks with their own data asset type, so each attack's Inspector shows a field of the right type.
/// </summary>
public abstract class BossAttack<TData> : BossAttack where TData : BossAttackData
{
    [Tooltip("Settings for this attack. Make another asset of the same type for a variation of it.")]
    [SerializeField] protected TData data;

    public override BossAttackData BaseData => data;
    public TData Data => data;
}
