using System;
using System.Collections.Generic;
using DYP;
using UnityEngine;

/// <summary>
/// Shared 2D enemy brain with the states Patrol, Chase, Attack and (optionally) JumpAttack.
/// Movement goes through BasicMovementController2D (the same motor the player uses),
/// and the attack itself is delegated to an EnemyAttack component. A different enemy type
/// (e.g. a shooter) is this same brain with a different EntityData and EnemyAttack setup.
/// Adding a JumpAttack component makes the enemy leap through the player whenever its cooldown is ready.
/// Adding a ChargeAttack component makes it charge in a straight line instead, getting stunned if it runs into a wall.
/// Adding an EnemyFlight component (Kilili) makes it take off when the player enters its attack range, fly around the
/// player shooting from a distance, and fly back to where it took off and land once it has lost them.
/// </summary>
[RequireComponent(typeof(BasicMovementController2D))]
public class EnemyBehavior : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack,
        JumpAttack,
        Charge,
        Stunned,
        Flying,
        FlyingHome
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
    // Jump/fall prediction: seconds per simulated step and how many steps before giving up.
    private const float SimTimeStep = 0.04f;
    private const int MaxSimSteps = 100;
    // Small gap kept between the simulated body and the ground, so the prediction doesn't start inside a collider.
    private const float SkinWidth = 0.05f;
    // A planned route is recalculated after this many seconds even if nothing changed, in case the enemy got stuck.
    private const float RouteLifetime = 3f;
    // Being airborne for less than this (e.g. walking over the seam between two platforms) doesn't count as a failed jump.
    private const float MinAirTimeForFailure = 0.15f;
    // Extra walking cost given to jumps, so the enemy prefers just walking off an edge when both work.
    private const float JumpCost = 0.25f;
    // A jump attack whose leap was ruled out (e.g. it would land off a ledge) is re-checked after this many seconds.
    private const float JumpAttackRecheckInterval = 0.25f;
    // If the enemy hasn't left the ground this long after pressing jump, the leap is given up.
    private const float LeapTakeoffTimeout = 0.3f;

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

    [Header("Path Following")]
    [Tooltip("How many platforms the player touched (while in sight) the enemy remembers.")]
    [SerializeField, Min(1)] private int maxTrailLength = 10;
    [Tooltip("Seconds a remembered platform is kept after the player was last seen on it.")]
    [SerializeField, Min(0f)] private float trailMemoryTime = 10f;
    [Tooltip("How far (on X) the enemy looks for other platforms in the direction it last saw the player.")]
    [SerializeField, Min(0f)] private float platformSearchRange = 8f;
    [Tooltip("The enemy never plans a drop deeper than this.")]
    [SerializeField, Min(0f)] private float maxDropHeight = 8f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool spriteFacesRight = true;

    private BasicMovementController2D movement;
    private EnemyAttack attack;
    private JumpAttack jumpAttack;
    private ChargeAttack chargeAttack;
    private EnemyFlight flight;
    private EnemyHealth health;
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

    /// <summary>A platform the player was seen standing on.</summary>
    private struct PlatformMark
    {
        public Collider2D Platform;
        public Vector2 Point;
        public float LastSeenTime;
    }

    /// <summary>
    /// One move from the platform the enemy stands on: walk to TakeoffX, then jump (or just keep walking
    /// off the edge) in Direction. Target is the platform the move is predicted to land on.
    /// </summary>
    private struct Route
    {
        public Collider2D From;
        public Collider2D Target;
        public float TakeoffX;
        public int Direction;
        public bool Jump;
        public Vector2 Landing;
        public float Cost;
    }

    private static readonly Comparison<Route> ByCost = (a, b) => a.Cost.CompareTo(b.Cost);

    // Oldest platform first; the last entry is where the player was last seen standing.
    private readonly List<PlatformMark> playerTrail = new List<PlatformMark>();
    // Platforms the enemy jumped at and fell back from, skipped until the trail changes.
    private readonly HashSet<Collider2D> failedTargets = new HashSet<Collider2D>();
    // Platforms left on a guessed route, so the enemy doesn't bounce back and forth between two guesses.
    private readonly HashSet<Collider2D> deadEnds = new HashSet<Collider2D>();
    private readonly List<Route> candidates = new List<Route>();
    private readonly Collider2D[] overlapBuffer = new Collider2D[16];
    private readonly Collider2D[] simOverlapBuffer = new Collider2D[1];
    private readonly RaycastHit2D[] castBuffer = new RaycastHit2D[8];
    private ContactFilter2D groundFilter;
    private Collider2D currentGround;
    private Route route;
    private bool hasRoute;
    private float routeExpireTime;
    private int trailVersion;
    private int routeTrailVersion;
    private bool airborneOnRoute;
    private float airborneStartTime;

    private int leapDirection;
    private Vector2 lungeVelocity;
    private bool leapStarted;
    private bool leapLeftGround;
    private float leapStartTime;
    private float jumpAttackGiveUpTime;
    private float nextJumpAttackCheckTime;

    private int chargeDirection;
    private bool chargeRunStarted;
    private float chargeStartX;
    private float chargeGiveUpTime;

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
        jumpAttack = GetComponent<JumpAttack>();
        chargeAttack = GetComponent<ChargeAttack>();
        flight = GetComponent<EnemyFlight>();
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
        // Path prediction ignores triggers (ladder zones etc.) and the enemy's own layer, since it can't stand on either.
        groundFilter = new ContactFilter2D { useTriggers = false };
        groundFilter.SetLayerMask(groundMask & ~(1 << gameObject.layer));

        if (enemyData == null) Debug.LogError($"{name}: No EntityData assigned to EnemyBehavior.", this);
        if (attack == null) Debug.LogWarning($"{name}: No EnemyAttack component found, the enemy will not attack.", this);

        // Health sits on a child of the enemy.
        health = GetComponentInChildren<EnemyHealth>();
        if (health != null) health.Damaged += OnDamaged;
    }

    private void OnDestroy()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    /// <summary>
    /// Getting hit gives away where the player is: the enemy starts chasing toward them even without seeing them,
    /// and gives up after loseSightTime like when it loses sight.
    /// </summary>
    private void OnDamaged()
    {
        if (!enabled || enemyData == null || playerCollider == null) return;

        lastKnownPlayerPosition = playerCollider.bounds.center;
        lastSeenTime = Time.time;
        if (flight != null) flight.RememberPlayerPosition(lastKnownPlayerPosition);

        if (currentState == EnemyState.Patrol) SetState(EnemyState.Chase);
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

        // While flying the ground controller is switched off, so its ground check is stale.
        bool isFlying = flight != null && flight.IsFlying;
        currentGround = !isFlying && movement.IsOnGround() ? GetGroundBelow(selfCollider.bounds, out _) : null;

        // Player missing or destroyed: just keep patrolling.
        bool canSeePlayer = playerCollider != null && CanSeePlayer();
        if (canSeePlayer)
        {
            lastKnownPlayerPosition = playerCollider.bounds.center;
            lastSeenTime = Time.time;
            RememberPlayerPlatform();
            if (flight != null) flight.RememberPlayerPosition(lastKnownPlayerPosition);
        }

        ForgetOldPlatforms();

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
            case EnemyState.JumpAttack:
                RunJumpAttack();
                break;
            case EnemyState.Charge:
                RunCharge();
                break;
            case EnemyState.Stunned:
                Move(0f, 0f);
                break;
            case EnemyState.Flying:
                FlyAtPlayer(canSeePlayer);
                break;
            case EnemyState.FlyingHome:
                FlyHome();
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
                if (TryTakeOff(canSeePlayer, distanceToPlayer)) break;
                // Special attacks take priority whenever their cooldown is ready; the basic attack covers the time in between.
                if (TryStartCharge(canSeePlayer) || TryStartJumpAttack(canSeePlayer)) break;
                if (canSeePlayer && distanceToPlayer <= enemyData.attackRange) SetState(EnemyState.Attack);
                else if (!remembersPlayer) SetState(EnemyState.Patrol);
                break;

            case EnemyState.Attack:
                // Never cancel an attack halfway through its windup.
                if (attack != null && attack.IsAttacking) break;
                if (TryTakeOff(canSeePlayer, distanceToPlayer)) break;
                if (TryStartCharge(canSeePlayer) || TryStartJumpAttack(canSeePlayer)) break;
                if (!canSeePlayer || distanceToPlayer > enemyData.attackRange + AttackRangeTolerance) SetState(EnemyState.Chase);
                break;

            case EnemyState.JumpAttack:
                // Runs until the enemy lands; afterwards Chase decides what's next.
                if (jumpAttack == null || !jumpAttack.IsActive) SetState(EnemyState.Chase);
                break;

            case EnemyState.Charge:
                // Runs until the charge ends; a wall hit stuns the enemy first.
                if (chargeAttack == null || !chargeAttack.IsActive)
                    SetState(chargeAttack != null && chargeAttack.IsStunned ? EnemyState.Stunned : EnemyState.Chase);
                break;

            case EnemyState.Stunned:
                if (chargeAttack == null || !chargeAttack.IsStunned) SetState(EnemyState.Chase);
                break;

            case EnemyState.Flying:
                // Lost the player for loseSightTime: fly back and land.
                if (!remembersPlayer) SetState(EnemyState.FlyingHome);
                break;

            case EnemyState.FlyingHome:
                // Turn back if the player shows up in attack range again, or gives themselves away by hitting it.
                if (remembersPlayer && (!canSeePlayer || distanceToPlayer <= enemyData.attackRange)) SetState(EnemyState.Flying);
                break;
        }
    }

    /// <summary>
    /// Enemies with an EnemyFlight component take off as soon as they see the player inside their attack range.
    /// </summary>
    private bool TryTakeOff(bool canSeePlayer, float distanceToPlayer)
    {
        if (flight == null || !canSeePlayer || distanceToPlayer > enemyData.attackRange) return false;

        // Flying replaces the ground route.
        hasRoute = false;
        airborneOnRoute = false;

        flight.TakeOff();
        SetState(EnemyState.Flying);
        return true;
    }

    /// <summary>
    /// Flies around the player (or after them along their trail) and shoots whenever they are visible and in range.
    /// </summary>
    private void FlyAtPlayer(bool canSeePlayer)
    {
        flight.FlyTowardPlayer(lastKnownPlayerPosition, canSeePlayer, enemyData);
        Face(canSeePlayer ? lastKnownPlayerPosition.x - transform.position.x : flight.Velocity.x);

        if (attack == null || !canSeePlayer) return;
        if (Physics2D.Distance(selfCollider, playerCollider).distance <= enemyData.attackRange)
            attack.TryAttack(lastKnownPlayerPosition, enemyData);
    }

    private void FlyHome()
    {
        Face(flight.Velocity.x);
        if (flight.FlyHome(enemyData)) SetState(EnemyState.Patrol);
    }

    /// <summary>
    /// Starts a charge if the cooldown is ready, the enemy stands on the ground and the player is in range at roughly
    /// the same height. The direction is fixed when the windup starts, so the player can dodge it.
    /// </summary>
    private bool TryStartCharge(bool canSeePlayer)
    {
        if (chargeAttack == null || !chargeAttack.IsReady || !canSeePlayer || currentGround == null) return false;

        Bounds self = selfCollider.bounds;
        Bounds player = playerCollider.bounds;
        float deltaX = player.center.x - self.center.x;
        if (Mathf.Abs(deltaX) > enemyData.chargeRange) return false;
        if (Mathf.Abs(player.min.y - Feet) > enemyData.chargeMaxHeightDifference) return false;

        chargeDirection = deltaX >= 0f ? 1 : -1;
        chargeRunStarted = false;

        // The charge moves the enemy off its route; Chase replans from wherever it stops.
        hasRoute = false;
        airborneOnRoute = false;

        chargeAttack.Begin(enemyData);
        SetState(EnemyState.Charge);
        return true;
    }

    /// <summary>
    /// Stands still facing the player during the windup, then runs straight ahead at chargeSpeed, through the player.
    /// Stops at a wall (stunned), at a ledge or after chargeDistance.
    /// </summary>
    private void RunCharge()
    {
        if (!chargeAttack.WindupFinished)
        {
            Move(0f, 0f);
            Face(chargeDirection);
            return;
        }

        if (!chargeRunStarted)
        {
            // Knocked off the ground during the windup: cancel instead of charging in mid-air.
            if (currentGround == null)
            {
                chargeAttack.End(enemyData, false);
                return;
            }

            chargeRunStarted = true;
            chargeStartX = transform.position.x;
            // Backup in case the enemy gets stuck on something that is neither a wall nor a ledge.
            chargeGiveUpTime = Time.time + enemyData.chargeDistance / enemyData.chargeSpeed + 1f;
            chargeAttack.StartCharge(chargeDirection, enemyData);
        }

        float step = enemyData.chargeSpeed * Time.fixedDeltaTime;

        if (IsWallAhead(chargeDirection, step))
        {
            StopCharge(true);
            return;
        }

        float remaining = enemyData.chargeDistance - Mathf.Abs(transform.position.x - chargeStartX);
        bool atLedge = currentGround != null && IsGapAhead(chargeDirection, step + SkinWidth);

        if (remaining <= 0f || atLedge || Time.time >= chargeGiveUpTime)
        {
            StopCharge(false);
            return;
        }

        // Slow down for the last step, so the charge ends at exactly chargeDistance.
        float speed = Mathf.Min(enemyData.chargeSpeed, remaining / Time.fixedDeltaTime);
        Move(chargeDirection, speed / Mathf.Max(enemyData.speed, 0.01f));
    }

    private void StopCharge(bool hitWall)
    {
        Move(0f, 0f);
        movement.StopHorizontalMomentum();
        chargeAttack.End(enemyData, hitWall);
    }

    /// <summary>
    /// True when a steep wall (anything the enemy can't walk up) is within distance in front of the enemy.
    /// The check starts just above the feet, so the floor and gentle slopes don't count.
    /// </summary>
    private bool IsWallAhead(int direction, float distance)
    {
        Bounds self = selfCollider.bounds;
        float bottom = Feet + 0.1f;
        var center = new Vector2(self.center.x, (bottom + self.max.y) * 0.5f);
        var size = new Vector2(self.size.x - SkinWidth * 2f, self.max.y - bottom - SkinWidth * 2f);

        int count = Physics2D.BoxCast(center, size, 0f, Vector2.right * direction, groundFilter, castBuffer, distance + SkinWidth * 2f);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = castBuffer[i];
            if (hit.collider == playerCollider) continue;
            if (Mathf.Abs(hit.normal.x) >= WallNormalThreshold) return true;
        }

        return false;
    }

    /// <summary>
    /// Starts a jump attack if the cooldown is ready, the enemy stands on the ground and the player is in range at
    /// roughly the same height. The lunge is a low arc (jumpAttackLungeHeight, independent of the normal jump height)
    /// aimed to land jumpAttackOvershoot past the player, so the enemy passes through them. It is skipped if the
    /// predicted landing is off a ledge.
    /// </summary>
    private bool TryStartJumpAttack(bool canSeePlayer)
    {
        if (jumpAttack == null || !jumpAttack.IsReady || !canSeePlayer || currentGround == null) return false;
        // Gravity comes from the jump settings, so a zero jump height means no arc to lunge along.
        if (Time.time < nextJumpAttackCheckTime || Gravity <= 0f) return false;

        Bounds self = selfCollider.bounds;
        Bounds player = playerCollider.bounds;
        float deltaX = player.center.x - self.center.x;
        if (Mathf.Abs(deltaX) > enemyData.jumpAttackRange) return false;
        if (Mathf.Abs(player.min.y - Feet) > enemyData.jumpAttackMaxHeightDifference) return false;

        int direction = deltaX >= 0f ? 1 : -1;
        Vector2 velocity = GetLungeVelocity(Mathf.Abs(deltaX) + enemyData.jumpAttackOvershoot, direction, out float airTime);

        Vector2 start = new Vector2(self.center.x, self.center.y + SkinWidth);
        if (!SimulateArc(start, velocity, out _, out Vector2 landing) || landing.y < self.center.y - GapDepth)
        {
            nextJumpAttackCheckTime = Time.time + JumpAttackRecheckInterval;
            return false;
        }

        leapDirection = direction;
        lungeVelocity = velocity;
        leapStarted = false;
        leapLeftGround = false;
        jumpAttackGiveUpTime = Time.time + enemyData.jumpAttackWindup + airTime + 1f;

        // The leap moves the enemy off its route; Chase replans from wherever it lands.
        hasRoute = false;
        airborneOnRoute = false;

        jumpAttack.Begin(enemyData);
        SetState(EnemyState.JumpAttack);
        return true;
    }

    /// <summary>
    /// Launch velocity for a lunge that peaks at jumpAttackLungeHeight and covers the given distance.
    /// If that low arc would need more than jumpAttackMaxLungeSpeed, the arc is raised instead (longer air time)
    /// so the lunge still lands at the given distance.
    /// The lunge is launched as an impulse with no input afterwards, so the controller keeps the momentum in the air
    /// (slowed only by Recoil Air Deceleration) and it is stopped on landing.
    /// </summary>
    private Vector2 GetLungeVelocity(float distance, int direction, out float airTime)
    {
        float gravity = Gravity;
        float deceleration = enemyData.recoilAirDeceleration;
        float maxSpeedX = Mathf.Max(enemyData.jumpAttackMaxLungeSpeed, 0.01f);
        float speedY = Mathf.Sqrt(2f * gravity * enemyData.jumpAttackLungeHeight);
        airTime = 2f * speedY / gravity;

        // Aim slightly faster to make up for the air deceleration.
        float speedX = (distance + 0.5f * deceleration * airTime * airTime) / airTime;

        if (speedX > maxSpeedX)
        {
            // Air time at which the max speed covers the distance: distance = maxSpeedX * t - 0.5 * deceleration * t^2.
            float discriminant = maxSpeedX * maxSpeedX - 2f * deceleration * distance;
            if (deceleration <= 0f) airTime = distance / maxSpeedX;
            else if (discriminant >= 0f) airTime = (maxSpeedX - Mathf.Sqrt(discriminant)) / deceleration;
            // Out of reach even with the max speed: fly as far as it gets before the deceleration stops it.
            else airTime = maxSpeedX / deceleration;

            speedX = maxSpeedX;
            speedY = 0.5f * gravity * airTime;
        }

        return new Vector2(direction * speedX, speedY);
    }

    /// <summary>
    /// Crouches during the windup, then lunges toward (and through) the player, damaging the area in front of it
    /// while in the air. Ends on landing, which starts the jump attack cooldown.
    /// </summary>
    private void RunJumpAttack()
    {
        if (!jumpAttack.WindupFinished)
        {
            Move(0f, 0f);
            Face(leapDirection);
            return;
        }

        if (!leapStarted)
        {
            // Knocked off the ground during the windup: cancel instead of jumping in mid-air.
            if (currentGround == null)
            {
                jumpAttack.End(enemyData);
                return;
            }

            // The lunge is aimed at an exact landing spot, so it ignores the recoil speed cap meant for bow recoil.
            movement.AddImpulse(lungeVelocity, true);
            nextJumpTime = Time.time + enemyData.jumpCooldown;
            leapStarted = true;
            leapStartTime = Time.time;
            jumpAttack.StartLeap(leapDirection, enemyData);
        }

        // No input during the lunge: input would steer the momentum back toward the normal run speed.
        Move(0f, 0f);
        Face(leapDirection);

        if (currentGround == null) leapLeftGround = true;

        bool landed = currentGround != null && leapLeftGround;
        bool neverTookOff = !leapLeftGround && Time.time - leapStartTime > LeapTakeoffTimeout;

        if (landed || neverTookOff || Time.time >= jumpAttackGiveUpTime)
        {
            // Stop where the lunge lands instead of sliding on, so jumpAttackOvershoot is where the enemy ends up.
            if (landed) movement.StopHorizontalMomentum();
            jumpAttack.End(enemyData);
        }
    }

    private void SetState(EnemyState newState)
    {
        if (newState == currentState) return;

        currentState = newState;
        if (newState == EnemyState.Patrol) ResetPathing();
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
    /// Follows the platforms the player was seen on. When the enemy is already on the player's platform
    /// (or knows of no other), it runs straight at the last known position instead.
    /// </summary>
    private void Chase()
    {
        if (currentGround == null)
        {
            ChaseInAir();
            return;
        }

        if (airborneOnRoute)
        {
            // Landed back where the jump started: the target can't be reached from here, so try something else.
            airborneOnRoute = false;
            if (hasRoute && currentGround == route.From && Time.time - airborneStartTime >= MinAirTimeForFailure)
                failedTargets.Add(route.Target);
            hasRoute = false;
        }

        ConsumeTrail(currentGround);

        if (playerTrail.Count == 0)
        {
            ChaseDirectly();
            return;
        }

        if (!hasRoute || route.From != currentGround || routeTrailVersion != trailVersion || Time.time >= routeExpireTime)
            PlanRoute();

        if (hasRoute) FollowRoute();
        else ChaseDirectly();
    }

    /// <summary>
    /// Keeps steering in the direction of the current route, so the enemy follows the predicted arc.
    /// </summary>
    private void ChaseInAir()
    {
        if (hasRoute)
        {
            if (!airborneOnRoute)
            {
                airborneOnRoute = true;
                airborneStartTime = Time.time;
            }
            Move(route.Direction, 1f);
            return;
        }

        float deltaX = lastKnownPlayerPosition.x - transform.position.x;
        Move(Mathf.Abs(deltaX) <= ArriveDistance ? 0f : Mathf.Sign(deltaX), 1f);
    }

    /// <summary>
    /// Picks the next move, trying in order:
    /// 1. copy the player's path: reach one of the platforms the player touched, oldest first;
    /// 2. any other reachable platform in the direction the player was last seen;
    /// 3. predict where every possible jump or drop from here would land and take the one ending closest to the player.
    /// </summary>
    private void PlanRoute()
    {
        hasRoute = false;

        for (int i = 0; i < playerTrail.Count; i++)
        {
            Collider2D platform = playerTrail[i].Platform;
            if (failedTargets.Contains(platform)) continue;
            if (TryFindRoute(platform, out route))
            {
                CommitRoute();
                return;
            }
        }

        if (TryFindRouteTowardPlayer(out route))
        {
            CommitRoute();
            return;
        }

        if (TryPredictRoute(out route))
        {
            // A guess: don't come back to this platform as another guess, or the enemy could ping-pong forever.
            deadEnds.Add(currentGround);
            CommitRoute();
        }
    }

    private void CommitRoute()
    {
        hasRoute = true;
        routeTrailVersion = trailVersion;
        routeExpireTime = Time.time + RouteLifetime;
    }

    private void FollowRoute()
    {
        if (!route.Jump)
        {
            // Walking off the edge: the drop starts at the edge in the route's direction.
            Move(route.Direction, 1f);
            return;
        }

        float toTakeoff = route.TakeoffX - transform.position.x;
        // Wide enough that a full-speed step can't skip over the takeoff point.
        float tolerance = enemyData.speed * Time.fixedDeltaTime + SkinWidth;

        if (Mathf.Abs(toTakeoff) > tolerance)
        {
            Move(Mathf.Sign(toTakeoff), 1f);
            return;
        }

        // Wait at the takeoff point while the jump is on cooldown instead of running past it.
        if (Jump()) Move(route.Direction, 1f);
        else Move(0f, 0f);
    }

    /// <summary>
    /// Finds the cheapest move from the current platform that is predicted to land on the target.
    /// </summary>
    private bool TryFindRoute(Collider2D target, out Route best)
    {
        best = default;
        if (target == null || target == currentGround) return false;

        Bounds t = target.bounds;
        float feet = Feet;
        if (t.max.y - feet > MaxJumpReach + SkinWidth || feet - t.max.y > maxDropHeight) return false;

        candidates.Clear();
        AddCandidates(1, target);
        AddCandidates(-1, target);
        candidates.Sort(ByCost);

        for (int i = 0; i < candidates.Count; i++)
        {
            Route candidate = candidates[i];
            if (!Simulate(candidate, out Collider2D landedOn, out candidate.Landing) || landedOn != target) continue;

            candidate.Target = target;
            best = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Looks for reachable platforms between the enemy and the player's last known position (and a bit past it),
    /// and picks the one whose landing spot is closest to the player.
    /// </summary>
    private bool TryFindRouteTowardPlayer(out Route best)
    {
        best = default;

        Bounds self = selfCollider.bounds;
        float deltaX = lastKnownPlayerPosition.x - self.center.x;
        int direction = Mathf.Abs(deltaX) > ArriveDistance ? (deltaX > 0f ? 1 : -1) : facingDirection;

        float top = Feet + MaxJumpReach + 0.5f;
        float bottom = Feet - maxDropHeight;
        var center = new Vector2(self.center.x + direction * platformSearchRange * 0.5f, (top + bottom) * 0.5f);
        var size = new Vector2(platformSearchRange, top - bottom);

        int count = Physics2D.OverlapBox(center, size, 0f, groundFilter, overlapBuffer);
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider2D platform = overlapBuffer[i];
            if (platform == selfCollider || platform == currentGround) continue;
            if (failedTargets.Contains(platform) || deadEnds.Contains(platform)) continue;
            if (!TryFindRoute(platform, out Route candidate)) continue;

            float distance = Vector2.Distance(candidate.Landing, lastKnownPlayerPosition);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = candidate;
        }

        return bestDistance < float.PositiveInfinity;
    }

    /// <summary>
    /// Simulates every jump and drop available from here, in both directions, and keeps the one that lands
    /// closest to the player's last known position, even if that means going the "wrong" way for now.
    /// </summary>
    private bool TryPredictRoute(out Route best)
    {
        best = default;

        candidates.Clear();
        AddCandidates(1, null);
        AddCandidates(-1, null);

        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < candidates.Count; i++)
        {
            Route candidate = candidates[i];
            if (!Simulate(candidate, out Collider2D landedOn, out candidate.Landing)) continue;
            if (landedOn == currentGround || failedTargets.Contains(landedOn) || deadEnds.Contains(landedOn)) continue;

            float distance = Vector2.Distance(candidate.Landing, lastKnownPlayerPosition) + candidate.Cost * 0.1f;
            if (distance >= bestDistance) continue;

            candidate.Target = landedOn;
            bestDistance = distance;
            best = candidate;
        }

        return bestDistance < float.PositiveInfinity;
    }

    /// <summary>
    /// Adds the moves worth testing in one direction: jumps from where the enemy stands, from the platform's edge
    /// and halfway there, a run-up jump at the target (also used to get out from under it), and walking off the edge.
    /// </summary>
    private void AddCandidates(int direction, Collider2D target)
    {
        Bounds ground = currentGround.bounds;
        Bounds self = selfCollider.bounds;
        float x = self.center.x;
        // Takeoffs keep the enemy's center over the ground, so it is still standing when it jumps.
        float minX = ground.min.x + SkinWidth * 2f;
        float maxX = ground.max.x - SkinWidth * 2f;
        float edgeX = direction > 0 ? maxX : minX;

        if (enemyData.canJump)
        {
            AddCandidate(x, direction, true, minX, maxX);
            AddCandidate(edgeX, direction, true, minX, maxX);
            AddCandidate((x + edgeX) * 0.5f, direction, true, minX, maxX);

            if (target != null)
            {
                // Platforms are solid, so the enemy can't jump up through them: it takes off beside the target
                // (walking out from under it first if needed) and jumps toward it.
                Bounds t = target.bounds;
                float nearEdge = direction > 0 ? t.min.x : t.max.x;
                AddCandidate(nearEdge - direction * (self.extents.x + EdgeMargin), direction, true, minX, maxX);
                AddCandidate(nearEdge - direction * (self.extents.x + enemyData.jumpCheckDistance), direction, true, minX, maxX);
            }
        }

        AddCandidate(edgeX, direction, false, minX, maxX);
    }

    private void AddCandidate(float takeoffX, int direction, bool jump, float minX, float maxX)
    {
        takeoffX = Mathf.Clamp(takeoffX, minX, maxX);
        candidates.Add(new Route
        {
            From = currentGround,
            TakeoffX = takeoffX,
            Direction = direction,
            Jump = jump,
            Cost = Mathf.Abs(takeoffX - selfCollider.bounds.center.x) + (jump ? JumpCost : 0f)
        });
    }

    /// <summary>
    /// Predicts where a move ends up by sweeping the enemy's collider along the arc its own jump settings produce
    /// (the same gravity and jump speed the movement controller uses). Ceilings stop the rise, walls stop the
    /// sideways motion, so a jump without enough headroom correctly comes out as "lands somewhere else".
    /// Only single ground jumps are simulated: the enemy can't double jump or wall jump like the player.
    /// </summary>
    private bool Simulate(Route move, out Collider2D landedOn, out Vector2 landing)
    {
        Bounds self = selfCollider.bounds;
        float horizontalSpeed = move.Direction * enemyData.speed;

        if (move.Jump)
            return SimulateArc(new Vector2(move.TakeoffX, self.center.y + SkinWidth), new Vector2(horizontalSpeed, JumpSpeed), out landedOn, out landing);

        // Start just past the edge, where the enemy starts to fall.
        Bounds ground = move.From.bounds;
        float edge = move.Direction > 0 ? ground.max.x : ground.min.x;
        var start = new Vector2(edge + move.Direction * self.extents.x, self.center.y + SkinWidth);
        return SimulateArc(start, new Vector2(horizontalSpeed, 0f), out landedOn, out landing);
    }

    /// <summary>
    /// Sweeps the enemy's collider from position with the given launch velocity under the controller's gravity
    /// until it lands on something. Returns false if there's no room at the start or it falls too far.
    /// </summary>
    private bool SimulateArc(Vector2 position, Vector2 velocity, out Collider2D landedOn, out Vector2 landing)
    {
        landedOn = null;
        landing = default;

        Bounds self = selfCollider.bounds;
        Vector2 size = (Vector2)self.size - Vector2.one * (SkinWidth * 2f);

        // No room to stand there (low ceiling, a wall right past the edge...).
        if (Physics2D.OverlapBox(position, size, 0f, groundFilter, simOverlapBuffer) > 0) return false;

        float gravity = Gravity;
        float lowestY = self.center.y - maxDropHeight;

        for (int i = 0; i < MaxSimSteps; i++)
        {
            velocity.y -= gravity * SimTimeStep;
            Vector2 step = velocity * SimTimeStep;
            float distance = step.magnitude;
            if (distance < Mathf.Epsilon) continue;

            if (Physics2D.BoxCast(position, size, 0f, step / distance, groundFilter, castBuffer, distance) > 0)
            {
                RaycastHit2D hit = castBuffer[0];
                if (hit.normal.y >= WallNormalThreshold)
                {
                    landedOn = hit.collider;
                    landing = hit.centroid;
                    return true;
                }

                position = hit.centroid + hit.normal * 0.01f;
                if (hit.normal.y <= -WallNormalThreshold) velocity.y = Mathf.Min(velocity.y, 0f);
                else velocity.x = 0f;
                continue;
            }

            position += step;
            if (position.y < lowestY) return false;
        }

        return false;
    }

    /// <summary>
    /// Records the platform the player stands on, if it differs from the last one remembered.
    /// </summary>
    private void RememberPlayerPlatform()
    {
        Collider2D platform = GetGroundBelow(playerCollider.bounds, out Vector2 point);
        if (platform == null) return;

        int last = playerTrail.Count - 1;
        if (last >= 0 && playerTrail[last].Platform == platform)
        {
            PlatformMark mark = playerTrail[last];
            mark.LastSeenTime = Time.time;
            playerTrail[last] = mark;
            return;
        }

        // Already standing on the player's platform, nothing to follow.
        if (last < 0 && platform == currentGround) return;

        playerTrail.Add(new PlatformMark { Platform = platform, Point = point, LastSeenTime = Time.time });
        if (playerTrail.Count > maxTrailLength) playerTrail.RemoveAt(0);
        OnTrailChanged();
    }

    private void ForgetOldPlatforms()
    {
        int removed = 0;
        while (removed < playerTrail.Count)
        {
            PlatformMark mark = playerTrail[removed];
            if (mark.Platform != null && Time.time - mark.LastSeenTime <= trailMemoryTime) break;
            removed++;
        }

        if (removed == 0) return;
        playerTrail.RemoveRange(0, removed);
        OnTrailChanged();
    }

    /// <summary>
    /// Reaching a remembered platform means everything the player did before it is done.
    /// Uses the last occurrence, so if the player looped back here the loop is skipped.
    /// </summary>
    private void ConsumeTrail(Collider2D ground)
    {
        for (int i = playerTrail.Count - 1; i >= 0; i--)
        {
            if (playerTrail[i].Platform != ground) continue;

            playerTrail.RemoveRange(0, i + 1);
            OnTrailChanged();
            return;
        }
    }

    private void OnTrailChanged()
    {
        trailVersion++;
        failedTargets.Clear();
        deadEnds.Clear();
    }

    private void ResetPathing()
    {
        playerTrail.Clear();
        failedTargets.Clear();
        deadEnds.Clear();
        hasRoute = false;
        airborneOnRoute = false;
    }

    /// <summary>
    /// The ground collider right under the given bounds, checked at the center and both bottom corners
    /// so standing near an edge still counts. Uses the collider's bounds as the platform's edges elsewhere,
    /// so this works best with box-shaped platforms.
    /// </summary>
    private Collider2D GetGroundBelow(Bounds bounds, out Vector2 point)
    {
        float y = bounds.min.y + SkinWidth;
        float inset = bounds.extents.x * 0.9f;

        for (int i = 0; i < 3; i++)
        {
            float x = bounds.center.x + (i == 0 ? 0f : i == 1 ? -inset : inset);
            if (Physics2D.Raycast(new Vector2(x, y), Vector2.down, groundFilter, castBuffer, 0.3f) == 0) continue;

            point = castBuffer[0].point;
            return castBuffer[0].collider;
        }

        point = default;
        return null;
    }

    /// <summary>
    /// Runs toward where the player was last seen, jumping over walls and across gaps on the way.
    /// </summary>
    private void ChaseDirectly()
    {
        bool canJumpNow = enemyData.canJump && movement.IsOnGround();

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
    /// True when there is no ground lookAhead in front of the enemy's feet.
    /// </summary>
    private bool IsGapAhead(int direction, float lookAhead = EdgeMargin)
    {
        Bounds self = selfCollider.bounds;
        float frontX = direction > 0 ? self.max.x : self.min.x;
        Vector2 probe = new Vector2(frontX + direction * lookAhead, Feet + 0.1f);

        return Physics2D.Raycast(probe, Vector2.down, GapDepth, groundMask).collider == null;
    }

    /// <summary>
    /// Presses jump unless it is on cooldown. Returns whether the jump was pressed.
    /// </summary>
    private bool Jump()
    {
        if (Time.time < nextJumpTime) return false;

        nextJumpTime = Time.time + enemyData.jumpCooldown;
        movement.PressJump(true);
        return true;
    }

    private float Feet => selfCollider.bounds.min.y;

    private float MaxJumpReach => enemyData.maxJumpHeight * JumpReachFactor;

    // Same formula as BasicMovementController2D: gravity that reaches maxJumpHeight in timeToApex.
    private float Gravity
    {
        get
        {
            float timeToApex = Mathf.Max(enemyData.timeToApex, 0.01f);
            return 2f * enemyData.maxJumpHeight / (timeToApex * timeToApex);
        }
    }

    // Launch speed that peaks at MaxJumpReach rather than the full height, so predictions stay on the safe side.
    private float JumpSpeed => Mathf.Sqrt(2f * Gravity * MaxJumpReach);

    private void AttackPlayer()
    {
        Move(0f, 0f);

        // Turn toward the player only between attacks, so jumping over the enemy mid-attack doesn't flip it.
        if (attack != null && attack.IsAttacking) return;

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

        if (!Application.isPlaying) return;

        // Remembered player path (magenta) and the current planned move: takeoff point to predicted landing (orange).
        Gizmos.color = Color.magenta;
        for (int i = 0; i < playerTrail.Count; i++)
        {
            Gizmos.DrawWireSphere(playerTrail[i].Point, 0.12f);
            if (i > 0) Gizmos.DrawLine(playerTrail[i - 1].Point, playerTrail[i].Point);
        }

        if (hasRoute && selfCollider != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            var takeoff = new Vector2(route.Jump ? route.TakeoffX : selfCollider.bounds.center.x, Feet);
            Gizmos.DrawWireSphere(takeoff, 0.1f);
            Gizmos.DrawLine(takeoff, route.Landing);
            Gizmos.DrawWireSphere(route.Landing, 0.15f);
        }
    }
}
