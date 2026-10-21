using System;
using DYP;
using UnityEngine;

/// <summary>
/// Shared 2D enemy brain with three states: Patrol, Chase and Attack.
/// Movement goes through BasicMovementController2D (the same motor the player uses),
/// and the attack itself is delegated to an EnemyAttack component. A different enemy type
/// (e.g. a shooter) is this same brain with a different EntityData and EnemyAttack setup.
/// </summary>
[RequireComponent(typeof(BasicMovementController2D))]
public class EnemyBehavior : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack
    }

    // Extra distance the player has to move away before the enemy stops attacking, so it doesn't flicker between Chase and Attack.
    private const float AttackRangeTolerance = 0.25f;
    // How close (on X) counts as "arrived" at a patrol point or the last known player position.
    private const float ArriveDistance = 0.1f;
    // The player's feet must be at least this much higher than the enemy's feet to count as "on a higher platform".
    private const float StepHeight = 0.5f;
    // Fraction of the jump height the enemy trusts itself to reach, so it doesn't try jumps it barely misses.
    private const float JumpReachFactor = 0.9f;
    // Distance kept from a platform's edge when walking out from under it, and how far ahead the ground is checked for gaps.
    private const float EdgeMargin = 0.3f;
    // Drops shallower than this are walked down instead of jumped over.
    private const float GapDepth = 1f;
    // Walls steeper than this (normal mostly horizontal) are jumped over; gentler slopes are walked up.
    private const float WallNormalThreshold = 0.7f;

    [SerializeField] private EntityData enemyData;

    [Header("Patrol")]
    [Tooltip("The enemy walks back and forth between these two points (only X is used). Leave empty to stand still.")]
    [SerializeField] private Transform patrolPointA;
    [SerializeField] private Transform patrolPointB;

    [Header("Line of Sight")]
    [Tooltip("Layers that block the enemy's view, e.g. Environment. Must not contain the enemy's own layer.")]
    [SerializeField] private LayerMask sightBlockingLayers;
    [Tooltip("Where the sight rays start, relative to the enemy's position.")]
    [SerializeField] private Vector2 eyeOffset = new Vector2(0f, 0.5f);

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool spriteFacesRight = true;

    private BasicMovementController2D movement;
    private EnemyAttack attack;
    private Collider2D selfCollider;
    private Collider2D playerCollider;
    private int sightMask;
    private int groundMask;
    private float nextJumpTime;

    private EnemyState currentState = EnemyState.Patrol;
    private bool hasPatrolArea;
    private float patrolMinX;
    private float patrolMaxX;
    private int patrolDirection = 1;
    private float patrolWaitEndTime;
    private Vector2 lastKnownPlayerPosition;
    private float lastSeenTime = float.NegativeInfinity;
    private int facingDirection = 1;

    public EnemyState CurrentState => currentState;
    public int FacingDirection => facingDirection;
    public event Action<EnemyState> StateChanged;

    private void Reset()
    {
        sightBlockingLayers = LayerMask.GetMask("Environment");
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        movement = GetComponent<BasicMovementController2D>();
        attack = GetComponent<EnemyAttack>();
        selfCollider = GetComponent<Collider2D>();

        // RequireComponent only adds missing components when EnemyBehavior is added,
        // not to objects that already had it, so check explicitly.
        if (movement == null || selfCollider == null)
        {
            Debug.LogError($"{name}: EnemyBehavior needs a BasicMovementController2D and a Collider2D on the same GameObject. Disabling.", this);
            enabled = false;
            return;
        }

        // Jump checks use the same layers the motor collides with, so "ground" means the same thing to both.
        var motor = GetComponent<CharacterMotor2D>();
        groundMask = motor != null && motor.Raycaster != null ? motor.Raycaster.CollisionLayer.value : sightBlockingLayers.value;

        if (enemyData == null) Debug.LogError($"{name}: No EntityData assigned to EnemyBehavior.", this);
        if (attack == null) Debug.LogWarning($"{name}: No EnemyAttack component found, the enemy will not attack.", this);
    }

    private void Start()
    {
        // With variable jump height, letting go of jump cuts the jump short. Enemies always "hold" it to jump at full height.
        movement.HoldJump(true);

        // Patrol points are cached so they can be children of the enemy in the prefab.
        if (patrolPointA != null && patrolPointB != null)
        {
            hasPatrolArea = true;
            patrolMinX = Mathf.Min(patrolPointA.position.x, patrolPointB.position.x);
            patrolMaxX = Mathf.Max(patrolPointA.position.x, patrolPointB.position.x);
        }

        // The tag can also be on the player's child objects, so look for the 2D collider upwards.
        var player = GameObject.FindGameObjectWithTag("Player");
        playerCollider = player != null ? player.GetComponentInParent<Collider2D>() : null;
        if (playerCollider == null)
        {
            Debug.LogError("Player with a Collider2D not found in the scene. Please ensure the player has the 'Player' tag.", this);
            return;
        }

        sightMask = sightBlockingLayers.value | (1 << playerCollider.gameObject.layer);
    }

    private void FixedUpdate()
    {
        if (enemyData == null) return;

        // Player missing or destroyed: just keep patrolling.
        bool canSeePlayer = playerCollider != null && CanSeePlayer();
        if (canSeePlayer)
        {
            lastKnownPlayerPosition = playerCollider.bounds.center;
            lastSeenTime = Time.time;
        }

        UpdateState(canSeePlayer);

        switch (currentState)
        {
            case EnemyState.Patrol:
                Patrol();
                break;
            case EnemyState.Chase:
                Chase();
                break;
            case EnemyState.Attack:
                AttackPlayer();
                break;
        }
    }

    private void UpdateState(bool canSeePlayer)
    {
        bool remembersPlayer = Time.time - lastSeenTime <= enemyData.loseSightTime;
        float distanceToPlayer = canSeePlayer ? Physics2D.Distance(selfCollider, playerCollider).distance : float.PositiveInfinity;

        switch (currentState)
        {
            case EnemyState.Patrol:
                if (canSeePlayer) SetState(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                if (canSeePlayer && distanceToPlayer <= enemyData.attackRange) SetState(EnemyState.Attack);
                else if (!remembersPlayer) SetState(EnemyState.Patrol);
                break;

            case EnemyState.Attack:
                // Never cancel an attack halfway through its windup.
                if (attack != null && attack.IsAttacking) break;
                if (!canSeePlayer || distanceToPlayer > enemyData.attackRange + AttackRangeTolerance) SetState(EnemyState.Chase);
                break;
        }
    }

    private void SetState(EnemyState newState)
    {
        if (newState == currentState) return;

        currentState = newState;
        StateChanged?.Invoke(newState);
    }

    /// <summary>
    /// Walks between the two patrol points, waiting a moment at each end.
    /// If the enemy is outside the area (e.g. after a chase), it walks back into it.
    /// </summary>
    private void Patrol()
    {
        if (!hasPatrolArea || Time.time < patrolWaitEndTime)
        {
            Move(0f, 0f);
            return;
        }

        float targetX = patrolDirection > 0 ? patrolMaxX : patrolMinX;
        float deltaX = targetX - transform.position.x;

        // Reached or passed the patrol point in the current direction.
        if (deltaX * patrolDirection <= ArriveDistance)
        {
            patrolDirection = -patrolDirection;
            patrolWaitEndTime = Time.time + enemyData.patrolWaitTime;
            Move(0f, 0f);
            return;
        }

        Move(patrolDirection, enemyData.patrolSpeedMultiplier);
    }

    /// <summary>
    /// Runs toward where the player was last seen, jumping onto the player's platform,
    /// over walls and across gaps on the way.
    /// </summary>
    private void Chase()
    {
        bool canJumpNow = enemyData.canJump && movement.IsOnGround();

        if (canJumpNow && TryClimbToPlayer()) return;

        float deltaX = lastKnownPlayerPosition.x - transform.position.x;

        if (Mathf.Abs(deltaX) <= ArriveDistance)
        {
            Move(0f, 0f);
            return;
        }

        int direction = deltaX > 0f ? 1 : -1;
        Move(direction, 1f);

        // Jump across gaps only when the player isn't below; otherwise dropping down is the way to them.
        bool playerIsBelow = playerCollider != null && playerCollider.bounds.min.y < Feet - StepHeight;

        if (canJumpNow && (IsJumpableWallAhead(direction) || (!playerIsBelow && IsGapAhead(direction))))
            Jump();
    }

    /// <summary>
    /// If the player stands on a platform above the enemy that a jump can reach, the enemy walks out from
    /// under it and jumps onto it from the side (platforms are solid, so it can't jump up through them).
    /// Returns false when there is nothing to climb, so the normal chase runs instead.
    /// </summary>
    private bool TryClimbToPlayer()
    {
        if (!TryGetPlayerPlatform(out Bounds platform)) return false;

        Bounds self = selfCollider.bounds;
        float x = self.center.x;
        bool overlapsPlatform = self.max.x > platform.min.x && self.min.x < platform.max.x;

        if (overlapsPlatform)
        {
            // Under the platform: walk out past the nearer edge.
            if (self.max.y > platform.min.y + 0.05f) return false;

            float leftExit = platform.min.x - self.extents.x - EdgeMargin;
            float rightExit = platform.max.x + self.extents.x + EdgeMargin;
            float exitX = x - leftExit < rightExit - x ? leftExit : rightExit;
            Move(Mathf.Sign(exitX - x), 1f);
            return true;
        }

        // Beside the platform: run at it and jump once its edge is close enough.
        int direction = x < platform.center.x ? 1 : -1;
        float distanceToEdge = direction > 0 ? platform.min.x - self.max.x : self.min.x - platform.max.x;
        Move(direction, 1f);

        if (distanceToEdge <= enemyData.jumpCheckDistance) Jump();
        return true;
    }

    /// <summary>
    /// Finds the ground the player is standing on, if it is higher than the enemy's feet but low enough to jump onto.
    /// Uses the collider's bounds as the platform's edges, so it works best with box-shaped platforms.
    /// </summary>
    private bool TryGetPlayerPlatform(out Bounds platform)
    {
        platform = default;
        if (playerCollider == null) return false;

        Bounds player = playerCollider.bounds;
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(player.center.x, player.min.y + 0.05f), Vector2.down, 0.3f, groundMask);
        if (hit.collider == null) return false;

        float height = hit.point.y - Feet;
        if (height <= StepHeight || height > MaxJumpReach) return false;

        platform = hit.collider.bounds;
        return true;
    }

    /// <summary>
    /// True when a wall or step is right in front of the enemy and its top is low enough to jump over.
    /// </summary>
    private bool IsJumpableWallAhead(int direction)
    {
        Bounds self = selfCollider.bounds;
        float frontX = direction > 0 ? self.max.x : self.min.x;
        Vector2 forward = Vector2.right * direction;

        RaycastHit2D low = Physics2D.Raycast(new Vector2(frontX, Feet + 0.1f), forward, enemyData.jumpCheckDistance, groundMask);
        if (low.collider == null || Mathf.Abs(low.normal.x) < WallNormalThreshold) return false;

        RaycastHit2D high = Physics2D.Raycast(new Vector2(frontX, Feet + MaxJumpReach), forward, enemyData.jumpCheckDistance, groundMask);
        return high.collider == null;
    }

    /// <summary>
    /// True when there is no ground just in front of the enemy's feet.
    /// </summary>
    private bool IsGapAhead(int direction)
    {
        Bounds self = selfCollider.bounds;
        float frontX = direction > 0 ? self.max.x : self.min.x;
        Vector2 probe = new Vector2(frontX + direction * EdgeMargin, Feet + 0.1f);

        return Physics2D.Raycast(probe, Vector2.down, GapDepth, groundMask).collider == null;
    }

    private void Jump()
    {
        if (Time.time < nextJumpTime) return;

        nextJumpTime = Time.time + enemyData.jumpCooldown;
        movement.PressJump(true);
    }

    private float Feet => selfCollider.bounds.min.y;

    private float MaxJumpReach => enemyData.maxJumpHeight * JumpReachFactor;

    private void AttackPlayer()
    {
        Move(0f, 0f);
        Face(lastKnownPlayerPosition.x - transform.position.x);

        if (attack != null) attack.TryAttack(lastKnownPlayerPosition, enemyData);
    }

    /// <summary>
    /// Feeds horizontal input to the movement controller. The controller multiplies it by EntityData.speed.
    /// </summary>
    private void Move(float direction, float speedMultiplier)
    {
        movement.InputMovement(new Vector2(direction * speedMultiplier, 0f));
        Face(direction);
    }

    private void Face(float direction)
    {
        if (Mathf.Approximately(direction, 0f)) return;

        facingDirection = direction > 0f ? 1 : -1;
        if (spriteRenderer != null) spriteRenderer.flipX = spriteFacesRight ? facingDirection < 0 : facingDirection > 0;
    }

    /// <summary>
    /// The player is seen when they are inside the detection circle and at least one ray
    /// (to the center, top or bottom of their collider) hits them before hitting a wall.
    /// </summary>
    private bool CanSeePlayer()
    {
        Vector2 eye = EyePosition;
        float range = enemyData.detectionRange;

        if (Vector2.Distance(eye, playerCollider.ClosestPoint(eye)) > range) return false;

        Bounds bounds = playerCollider.bounds;
        Vector2 center = bounds.center;
        Vector2 verticalOffset = new Vector2(0f, bounds.extents.y * 0.8f);

        return IsVisible(eye, center, range)
            || IsVisible(eye, center + verticalOffset, range)
            || IsVisible(eye, center - verticalOffset, range);
    }

    private bool IsVisible(Vector2 eye, Vector2 point, float range)
    {
        Vector2 toPoint = point - eye;
        RaycastHit2D hit = Physics2D.Raycast(eye, toPoint.normalized, Mathf.Min(toPoint.magnitude, range), sightMask);
        return hit.collider == playerCollider;
    }

    private Vector2 EyePosition => (Vector2)transform.position + eyeOffset;

    private void OnDrawGizmosSelected()
    {
        Vector2 eye = EyePosition;

        if (enemyData != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(eye, enemyData.detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, enemyData.attackRange);
        }

        if (patrolPointA != null && patrolPointB != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(patrolPointA.position, patrolPointB.position);
            Gizmos.DrawWireSphere(patrolPointA.position, 0.15f);
            Gizmos.DrawWireSphere(patrolPointB.position, 0.15f);
        }

        // Line to the player while playing: green while chasing or attacking, grey while patrolling.
        if (Application.isPlaying && playerCollider != null)
        {
            Gizmos.color = currentState == EnemyState.Patrol ? Color.grey : Color.green;
            Gizmos.DrawLine(eye, playerCollider.bounds.center);
        }
    }
}
