using System;
using System.Collections.Generic;
using DYP;
using UnityEngine;

/// <summary>
/// Boss AI. It stands still (Idle) until a BossEncounter starts the fight (the player walks into its trigger).
/// While fighting it walks toward the player (or out of them when they stand inside it)
/// between attacks. When the pause between attacks is over, it picks one attack from its bank:
/// the BossAttack components on it and its children that are ready and fit the player's distance, chosen by weight.
/// Movement goes through BasicMovementController2D like the player and the other enemies. Arena fights only, so there
/// is no patrolling, line of sight or path finding: the boss always knows where the player is.
/// </summary>
[RequireComponent(typeof(BasicMovementController2D))]
public class BossBrain : MonoBehaviour
{
    public enum BossState
    {
        Idle,
        Repositioning,
        Attacking,
        Stunned,
        Dead
    }

    // Small gap kept between casts and colliders, so a cast doesn't start inside the floor.
    private const float SkinWidth = 0.05f;
    // How far ahead of its front edge the boss checks for a ledge before walking, and how deep a drop counts as one.
    private const float LedgeLookAhead = 0.3f;
    private const float LedgeDepth = 1f;
    // Wall check distance for normal walking.
    private const float WalkWallCheckDistance = 0.1f;

    [SerializeField] private BossData bossData;

    [Header("Visuals")]
    [Tooltip("Mirrored on X (localScale.x) to face left or right. Put the boss's sprites under it, but not the attack hitboxes, which place themselves. If empty, the Sprite Renderer is flipped instead.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Flipped with flipX when there is no Visual Root. Also the sprite that flashes red when hit, unless BossHealth has its own.")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool spriteFacesRight = true;

    private BasicMovementController2D movement;
    private Collider2D selfCollider;
    private Collider2D playerCollider;
    private BossHealth health;
    private readonly List<BossAttack> attacks = new List<BossAttack>();
    private readonly List<BossAttack> candidates = new List<BossAttack>();
    private readonly List<float> candidateWeights = new List<float>();
    private readonly RaycastHit2D[] castBuffer = new RaycastHit2D[8];
    private ContactFilter2D groundFilter;
    private int groundMask;

    private BossState currentState = BossState.Idle;
    private BossAttack currentAttack;
    private BossAttack lastAttack;
    private float nextAttackTime;
    private float stunEndTime;
    private int facingDirection = 1;
    private float visualScaleX = 1f;

    public BossState CurrentState => currentState;
    public BossData Data => bossData;
    public int FacingDirection => facingDirection;
    public bool IsOnGround => movement != null && movement.IsOnGround();
    public bool IsFighting => currentState is BossState.Repositioning or BossState.Attacking or BossState.Stunned;
    public BossAttack CurrentAttack => currentAttack;
    public BasicMovementController2D Movement => movement;
    public Collider2D SelfCollider => selfCollider;
    public Collider2D PlayerCollider => playerCollider;
    public SpriteRenderer MainSprite => spriteRenderer;
    /// <summary>Speed for a full movement input, to turn a speed in m/s into controller input.</summary>
    public float MoveSpeed => movement != null ? Mathf.Max(movement.MoveSpeed, 0.01f) : 1f;

    /// <summary>Bottom center of the boss's collider. Works in edit mode too, for gizmos.</summary>
    public Vector2 FeetCenter
    {
        get
        {
            Collider2D body = selfCollider != null ? selfCollider : GetComponent<Collider2D>();
            if (body == null) return transform.position;
            Bounds bounds = body.bounds;
            return new Vector2(bounds.center.x, bounds.min.y);
        }
    }

    /// <summary>Top center of the boss's collider.</summary>
    public Vector2 TopCenter
    {
        get
        {
            Collider2D body = selfCollider != null ? selfCollider : GetComponent<Collider2D>();
            if (body == null) return transform.position;
            Bounds bounds = body.bounds;
            return new Vector2(bounds.center.x, bounds.max.y);
        }
    }

    public event Action<BossState> StateChanged;
    /// <summary>Raised when an attack from the bank starts. Use BossAttack.PhaseChanged for its phases.</summary>
    public event Action<BossAttack> AttackStarted;

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        movement = GetComponent<BasicMovementController2D>();
        selfCollider = GetComponent<Collider2D>();

        if (movement == null || selfCollider == null)
        {
            Debug.LogError($"{name}: BossBrain needs a BasicMovementController2D and a Collider2D on the same GameObject. Disabling.", this);
            enabled = false;
            return;
        }

        if (bossData == null)
        {
            Debug.LogError($"{name}: No BossData assigned to BossBrain. Disabling.", this);
            enabled = false;
            return;
        }

        // Wall and ledge checks use the layers the motor collides with, so "ground" means the same thing to both.
        var motor = GetComponent<CharacterMotor2D>();
        groundMask = motor != null && motor.Raycaster != null ? motor.Raycaster.CollisionLayer.value : LayerMask.GetMask("Environment");
        groundFilter = new ContactFilter2D { useTriggers = false };
        groundFilter.SetLayerMask(groundMask & ~(1 << gameObject.layer));

        // The attack bank: every attack on the boss. Disabled components are skipped when picking.
        GetComponentsInChildren(true, attacks);
        if (attacks.Count == 0) Debug.LogWarning($"{name}: The boss has no BossAttack components, so it will never attack.", this);

        if (visualRoot != null) visualScaleX = Mathf.Abs(visualRoot.localScale.x);

        health = GetComponentInChildren<BossHealth>();
        if (health != null) health.Died += OnDied;
        else Debug.LogWarning($"{name}: No BossHealth found on the boss or its children, so it can't be killed.", this);
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
    }

    private void Start()
    {
        // Like the other enemies: always "hold" jump so a jump (if any attack uses one) reaches full height.
        movement.HoldJump(true);

        var player = GameObject.FindGameObjectWithTag("Player");
        playerCollider = player != null ? player.GetComponentInParent<Collider2D>() : null;
        if (playerCollider == null)
            Debug.LogError("BossBrain: Player with a Collider2D not found in the scene. Please ensure the player has the 'Player' tag.", this);
    }

    /// <summary>Starts the fight. Called by BossEncounter when the player walks into its trigger.</summary>
    public void BeginFight()
    {
        if (currentState != BossState.Idle) return;
        FacePlayer();
        nextAttackTime = Time.time + bossData.firstAttackDelay;
        SetState(BossState.Repositioning);
    }

    /// <summary>Stuns the boss: it stands still and can't attack. Used by attacks, e.g. a charge into a wall.</summary>
    public void Stun(float seconds)
    {
        if (seconds <= 0f || currentState == BossState.Dead) return;
        stunEndTime = Mathf.Max(stunEndTime, Time.time + seconds);
    }

    public bool IsStunned => Time.time < stunEndTime;

    /// <summary>
    /// Knockback from arrows and explosions (ArrowProjectile sends it here instead of to the controller), scaled by
    /// BossData.knockbackMultiplier. Ignored while an attack moves the boss, so a charge can't be pushed off course.
    /// </summary>
    public void ApplyKnockback(Vector2 impulse)
    {
        if (movement == null || bossData == null || bossData.knockbackMultiplier <= 0f) return;
        if (currentState == BossState.Dead) return;
        if (currentAttack != null && currentAttack.ControlsMovement) return;

        movement.ApplyKnockback(impulse * bossData.knockbackMultiplier);
    }

    private void FixedUpdate()
    {
        switch (currentState)
        {
            case BossState.Idle:
            case BossState.Dead:
                Stop();
                return;
        }

        if (playerCollider == null)
        {
            Stop();
            return;
        }

        // The running attack owns the boss until its recovery is over.
        if (currentAttack != null)
        {
            if (currentAttack.IsRunning)
            {
                if (!currentAttack.ControlsMovement) Stop();
                return;
            }

            lastAttack = currentAttack;
            currentAttack = null;
            nextAttackTime = Time.time + UnityEngine.Random.Range(bossData.minTimeBetweenAttacks, bossData.maxTimeBetweenAttacks);
        }

        if (IsStunned)
        {
            Stop();
            SetState(BossState.Stunned);
            return;
        }

        BossTargetInfo target = GetTargetInfo();

        if (Time.time >= nextAttackTime && TryPickAttack(target, out BossAttack picked))
        {
            Stop();
            Face(target.Side);
            if (picked.Begin(target))
            {
                currentAttack = picked;
                SetState(BossState.Attacking);
                AttackStarted?.Invoke(picked);
                return;
            }
        }

        SetState(BossState.Repositioning);
        Reposition(target);
    }

    /// <summary>
    /// Between attacks: walk out of the player when they stand inside the boss, otherwise walk up to
    /// preferredDistance and wait there facing them.
    /// </summary>
    private void Reposition(in BossTargetInfo target)
    {
        int towardPlayer = target.Side;

        if (target.Inside)
        {
            // Back off while still facing the player, so the next attack goes their way.
            WalkIfSafe(-towardPlayer, bossData.retreatSpeedMultiplier, towardPlayer);
        }
        else if (target.HorizontalGap > bossData.preferredDistance + bossData.distanceTolerance)
        {
            WalkIfSafe(towardPlayer, bossData.approachSpeedMultiplier, towardPlayer);
        }
        else
        {
            Stop();
            Face(towardPlayer);
        }
    }

    /// <summary>Walks unless a wall or a ledge is right in front; then just stands, facing faceDirection.</summary>
    private void WalkIfSafe(int direction, float speedMultiplier, int faceDirection)
    {
        bool blocked = IsWallAhead(direction, WalkWallCheckDistance) || (IsOnGround && IsLedgeAhead(direction, LedgeLookAhead));
        movement.InputMovement(new Vector2(blocked ? 0f : direction * speedMultiplier, 0f));
        Face(faceDirection);
    }

    /// <summary>Weighted random pick between the ready attacks that fit the player's position.</summary>
    private bool TryPickAttack(in BossTargetInfo target, out BossAttack picked)
    {
        picked = null;
        candidates.Clear();
        candidateWeights.Clear();
        float total = 0f;
        bool onGround = IsOnGround;

        foreach (BossAttack attack in attacks)
        {
            if (attack == null) continue;

            float weight = attack.GetWeight(target, onGround);
            if (attack == lastAttack) weight *= bossData.repeatWeightMultiplier;
            if (weight <= 0f) continue;

            candidates.Add(attack);
            candidateWeights.Add(weight);
            total += weight;
        }

        // Only the last attack fits, but it was weighted down to 0: use it anyway rather than doing nothing.
        if (candidates.Count == 0 && lastAttack != null && bossData.repeatWeightMultiplier <= 0f
            && lastAttack.GetWeight(target, onGround) > 0f)
        {
            picked = lastAttack;
            return true;
        }

        if (candidates.Count == 0) return false;

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= candidateWeights[i];
            if (roll <= 0f)
            {
                picked = candidates[i];
                return true;
            }
        }

        picked = candidates[candidates.Count - 1];
        return true;
    }

    private BossTargetInfo GetTargetInfo()
    {
        Bounds self = selfCollider.bounds;
        Bounds player = playerCollider.bounds;

        ColliderDistance2D distance = Physics2D.Distance(selfCollider, playerCollider);
        bool inside = distance.isOverlapped;
        float gap = inside ? 0f : Mathf.Max(0f, distance.distance);
        float horizontalGap = Mathf.Max(0f, Mathf.Abs(player.center.x - self.center.x) - self.extents.x - player.extents.x);

        float deltaX = player.center.x - self.center.x;
        // Exactly on top of each other: keep facing the same way.
        int side = Mathf.Abs(deltaX) < 0.01f ? facingDirection : (deltaX > 0f ? 1 : -1);

        return new BossTargetInfo(player.center, gap, horizontalGap, inside, player.min.y - self.min.y, side);
    }

    /// <summary>Feeds horizontal input to the controller, faced the same way. Speed multiplier is relative to MoveSpeed.</summary>
    public void Move(int direction, float speedMultiplier)
    {
        movement.InputMovement(new Vector2(direction * speedMultiplier, 0f));
        Face(direction);
    }

    public void Stop()
    {
        if (movement != null) movement.InputMovement(Vector2.zero);
    }

    public void FacePlayer()
    {
        if (playerCollider == null || selfCollider == null) return;
        Face(playerCollider.bounds.center.x - selfCollider.bounds.center.x);
    }

    public void Face(float direction)
    {
        if (Mathf.Approximately(direction, 0f)) return;

        facingDirection = direction > 0f ? 1 : -1;

        if (visualRoot != null)
        {
            Vector3 scale = visualRoot.localScale;
            bool flip = spriteFacesRight ? facingDirection < 0 : facingDirection > 0;
            scale.x = flip ? -visualScaleX : visualScaleX;
            visualRoot.localScale = scale;
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.flipX = spriteFacesRight ? facingDirection < 0 : facingDirection > 0;
        }
    }

    /// <summary>
    /// True when a wall is within distance in front of the boss. The level has no slopes, so anything the cast hits
    /// counts. It starts just above the feet, so the floor doesn't. The player never counts as a wall.
    /// </summary>
    public bool IsWallAhead(int direction, float distance)
    {
        Bounds self = selfCollider.bounds;
        float bottom = self.min.y + 0.1f;
        var center = new Vector2(self.center.x, (bottom + self.max.y) * 0.5f);
        var size = new Vector2(self.size.x - SkinWidth * 2f, self.max.y - bottom - SkinWidth * 2f);

        int count = Physics2D.BoxCast(center, size, 0f, Vector2.right * direction, groundFilter, castBuffer, distance + SkinWidth * 2f);
        for (int i = 0; i < count; i++)
            if (castBuffer[i].collider != playerCollider) return true;

        return false;
    }

    /// <summary>True when there is no ground lookAhead in front of the boss's feet.</summary>
    public bool IsLedgeAhead(int direction, float lookAhead)
    {
        Bounds self = selfCollider.bounds;
        float frontX = direction > 0 ? self.max.x : self.min.x;
        var probe = new Vector2(frontX + direction * lookAhead, self.min.y + 0.1f);

        return Physics2D.Raycast(probe, Vector2.down, LedgeDepth, groundMask).collider == null;
    }

    private void OnDied()
    {
        if (currentState == BossState.Dead) return;

        if (currentAttack != null) currentAttack.Cancel();
        currentAttack = null;
        foreach (BossAttack attack in attacks)
            if (attack != null) attack.Cancel();

        Stop();
        if (movement != null) movement.StopHorizontalMomentum();
        SetState(BossState.Dead);

        if (bossData.soulPrefab != null)
            Instantiate(bossData.soulPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject, bossData.deathRemoveDelay);
    }

    private void SetState(BossState newState)
    {
        if (newState == currentState) return;
        currentState = newState;
        StateChanged?.Invoke(newState);
    }

    private void OnDrawGizmosSelected()
    {
        if (bossData == null) return;

        // Preferred waiting distance, drawn from both sides of the body.
        Collider2D body = selfCollider != null ? selfCollider : GetComponent<Collider2D>();
        if (body == null) return;

        Bounds bounds = body.bounds;
        Gizmos.color = Color.cyan;
        float y = bounds.min.y + 0.05f;
        Gizmos.DrawLine(new Vector3(bounds.max.x, y), new Vector3(bounds.max.x + bossData.preferredDistance, y));
        Gizmos.DrawLine(new Vector3(bounds.min.x, y), new Vector3(bounds.min.x - bossData.preferredDistance, y));
    }
}
