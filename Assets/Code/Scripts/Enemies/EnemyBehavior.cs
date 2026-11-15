using System;
using System.Collections.Generic;
using DYP;
using UnityEngine;

/// <summary>
/// Shared 2D enemy brain with the states Patrol, Chase, Attack and (optionally) JumpAttack.
/// Movement goes through BasicMovementController2D (the same motor the player uses),
/// and the attack itself is delegated to an EnemyAttack component. A different enemy type
/// (e.g. a shooter) is this same brain with a different EntityData and EnemyAttack setup.
/// To reach a player on another platform, the enemy follows hand-placed EnemyLinks (jump here, fall here),
/// chaining several if needed; on the player's platform it runs straight at them.
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
    // How far ahead the ground is checked for gaps.
    private const float EdgeMargin = 0.3f;
    // Drops shallower than this are walked down instead of jumped over.
    private const float GapDepth = 1f;
    // Walls steeper than this (normal mostly horizontal) are jumped over; gentler slopes are walked up.
    private const float WallNormalThreshold = 0.7f;
    // Jump attack landing prediction: seconds per simulated step and how many steps before giving up.
    private const float SimTimeStep = 0.04f;
    private const int MaxSimSteps = 100;
    // Small gap kept between the simulated body and the ground, so the prediction doesn't start inside a collider.
    private const float SkinWidth = 0.05f;
    // A planned route is recalculated after this many seconds even if nothing changed, in case the enemy got stuck.
    private const float RouteLifetime = 3f;
    // The player's platform is looked for this far below their last known position (they may be mid-jump).
    private const float PlayerGroundSearchDistance = 30f;
    // A link that failed (landed on the wrong platform, or took too long) is left out of routes for this many seconds.
    private const float LinkBlockTime = 5f;
    // Longest time in the air on a link before it counts as failed.
    private const float LinkTimeout = 4f;
    // A fall link fails if the enemy hasn't walked off the edge within this many seconds.
    private const float WalkOffTimeout = 1.5f;
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
    // Cached so TryAttack can read the position later (see EnemyAttack.aimAtWindupEnd) without allocating every call.
    private Func<Vector2> getLastKnownPlayerPosition;
    private float lastSeenTime = float.NegativeInfinity;
    private int facingDirection = 1;

    private readonly Collider2D[] simOverlapBuffer = new Collider2D[1];
    private readonly RaycastHit2D[] castBuffer = new RaycastHit2D[8];
    private ContactFilter2D groundFilter;
    private Collider2D currentGround;

    // Route over EnemyLinks toward the player (see EnemyLinkPlanner).
    private readonly List<EnemyLinkPlanner.Step> route = new List<EnemyLinkPlanner.Step>();
    // Links that failed for this enemy, and until when they are left out.
    private readonly Dictionary<EnemyLink, float> blockedLinks = new Dictionary<EnemyLink, float>();
    private int routeIndex;
    private bool hasRoute;
    private bool routeReachesPlayer;
    private Vector2 routeGoal;
    private int routePlayerPlatform = -1;
    private float routeExpireTime;
    // The platform the player was last seen standing on. Not updated while they are in the air, so jumping over
    // a gap doesn't make the enemy replan toward whatever is below the gap.
    private Collider2D playerGround;
    // The link being taken right now (Link is null when not on one).
    private EnemyLinkPlanner.Step activeLink;
    private bool linkLeftGround;
    private float linkStartTime;

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
        spriteRenderer = FindBodySprite();
    }

    /// <summary>
    /// The first SpriteRenderer under the enemy that isn't a DamageHitbox debug sprite.
    /// </summary>
    private SpriteRenderer FindBodySprite()
    {
        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sprite.GetComponentInParent<DamageHitbox>(true) == null) return sprite;
        }

        return null;
    }

    private void Awake()
    {
        movement = GetComponent<BasicMovementController2D>();
        attack = GetComponent<EnemyAttack>();
        getLastKnownPlayerPosition = () => lastKnownPlayerPosition;
        jumpAttack = GetComponent<JumpAttack>();
        chargeAttack = GetComponent<ChargeAttack>();
        flight = GetComponent<EnemyFlight>();
        selfCollider = GetComponent<Collider2D>();

        // Reset only fills this in when EnemyBehavior is first added, which may be before the sprite existed.
        if (spriteRenderer == null)
        {
            spriteRenderer = FindBodySprite();
            if (spriteRenderer == null)
                Debug.LogWarning($"{name}: No SpriteRenderer found for EnemyBehavior, the enemy won't flip.", this);
        }

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
            if (flight != null) flight.RememberPlayerPosition(lastKnownPlayerPosition);

            // Only a landing changes the player's platform; in the air they keep the one they jumped from.
            Collider2D standingOn = GetGroundBelow(playerCollider.bounds, out _);
            if (standingOn != null) playerGround = standingOn;
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
            attack.TryAttack(getLastKnownPlayerPosition, enemyData);
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
        // Attacks and other states take over mid-link; Chase plans again from wherever the enemy is afterwards.
        else if (newState != EnemyState.Chase) activeLink = default;
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
    /// On the player's platform, runs straight at them. Otherwise follows a route of hand-placed EnemyLinks
    /// (see EnemyLinkPlanner) to the player's platform, or to the reachable spot closest to them, and waits there.
    /// </summary>
    private void Chase()
    {
        if (activeLink.Link != null)
        {
            RunLink();
            return;
        }

        if (currentGround == null)
        {
            ChaseInAir();
            return;
        }

        // Never seen the player standing yet (e.g. hit by an arrow shot mid-jump): use the platform below them.
        if (playerGround == null) playerGround = GetPlayerGround();

        if (EnemyLinkPlanner.SamePlatform(currentGround, playerGround, groundMask))
        {
            hasRoute = false;
            ChaseDirectly();
            return;
        }

        int playerPlatform = EnemyLinkPlanner.GroupOf(playerGround, groundMask);
        if (!hasRoute || playerPlatform != routePlayerPlatform || Time.time >= routeExpireTime)
            PlanRoute(playerGround, playerPlatform);

        FollowRoute();
    }

    /// <summary>
    /// In the air without a link (knocked off a ledge, pushed): drift toward the player and replan after landing.
    /// </summary>
    private void ChaseInAir()
    {
        hasRoute = false;
        float deltaX = lastKnownPlayerPosition.x - transform.position.x;
        Move(Mathf.Abs(deltaX) <= ArriveDistance ? 0f : Mathf.Sign(deltaX), 1f);
    }

    /// <summary>The platform under the player's last known position (the one below them if they are in the air), or null.</summary>
    private Collider2D GetPlayerGround()
    {
        int count = Physics2D.Raycast(lastKnownPlayerPosition, Vector2.down, groundFilter, castBuffer, PlayerGroundSearchDistance);
        Collider2D nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (castBuffer[i].distance >= nearestDistance) continue;
            nearest = castBuffer[i].collider;
            nearestDistance = castBuffer[i].distance;
        }
        return nearest;
    }

    private void PlanRoute(Collider2D playerGround, int playerPlatform)
    {
        var feet = new Vector2(selfCollider.bounds.center.x, Feet);
        routeReachesPlayer = EnemyLinkPlanner.Plan(currentGround, feet, playerGround, lastKnownPlayerPosition,
            enemyData, groundMask, blockedLinks, route, out routeGoal);

        routeIndex = 0;
        hasRoute = true;
        routePlayerPlatform = playerPlatform;
        routeExpireTime = Time.time + RouteLifetime;
    }

    /// <summary>
    /// Walks to the start of the next link and starts it. After the last link, the enemy is on the player's platform
    /// (where Chase switches to running at them), or, if they can't be reached, it walks to the closest spot and waits.
    /// </summary>
    private void FollowRoute()
    {
        float x = selfCollider.bounds.center.x;

        if (routeIndex >= route.Count)
        {
            float toGoal = routeGoal.x - x;
            if (Mathf.Abs(toGoal) > ArriveDistance)
            {
                WalkOnRoute(Mathf.Sign(toGoal));
                return;
            }

            Move(0f, 0f);
            Face(lastKnownPlayerPosition.x - transform.position.x);
            return;
        }

        EnemyLinkPlanner.Step step = route[routeIndex];

        // Pushed or knocked onto another platform: plan again from here.
        if (!EnemyLinkPlanner.SamePlatform(currentGround, step.FromGround, groundMask))
        {
            hasRoute = false;
            Move(0f, 0f);
            return;
        }

        float toTakeoff = step.From.x - x;
        // Wide enough that a full-speed step can't skip over the takeoff point.
        float tolerance = enemyData.speed * Time.fixedDeltaTime + SkinWidth;
        if (Mathf.Abs(toTakeoff) > tolerance)
        {
            WalkOnRoute(Mathf.Sign(toTakeoff));
            return;
        }

        StartLink(step);
    }

    /// <summary>Walks along the current platform, hopping over low walls in the way.</summary>
    private void WalkOnRoute(float direction)
    {
        Move(direction, 1f);
        if (enemyData.canJump && movement.IsOnGround() && IsJumpableWallAhead((int)direction)) Jump();
    }

    /// <summary>
    /// Jumps are launched with the link's exact arc (an impulse, like the jump attack), from a standstill so the
    /// walking speed doesn't add to it. Falls just keep walking off the edge; RunLink steers them in the air.
    /// </summary>
    private void StartLink(EnemyLinkPlanner.Step step)
    {
        int direction = step.To.x >= step.From.x ? 1 : -1;

        if (step.Traversal == EnemyLink.Traversal.Jump)
        {
            // Wait on the takeoff point while the jump is on cooldown instead of running past it.
            if (Time.time < nextJumpTime || Gravity <= 0f)
            {
                Move(0f, 0f);
                Face(direction);
                return;
            }

            Vector2 velocity = step.Link.GetJumpVelocity(step.Reverse, Gravity, enemyData.recoilAirDeceleration,
                enemyData.recoilGroundDeceleration, out _);
            movement.StopHorizontalMomentum();
            // The arc is aimed at an exact landing spot, so it ignores the recoil speed cap meant for bow recoil.
            movement.AddImpulse(velocity, true);
            nextJumpTime = Time.time + enemyData.jumpCooldown;
            Move(0f, 0f);
        }
        else
        {
            Move(direction, 1f);
        }

        Face(direction);
        activeLink = step;
        linkLeftGround = false;
        linkStartTime = Time.time;
    }

    /// <summary>
    /// While on a link: no input during a jump (input would steer the momentum toward the run speed), and during a fall
    /// the input that lands on the link's end. On landing, the route moves on if the enemy is on the right platform;
    /// otherwise, or if the link takes too long, the link is blocked for a while and the route is planned again.
    /// </summary>
    private void RunLink()
    {
        EnemyLinkPlanner.Step step = activeLink;
        int direction = step.To.x >= step.From.x ? 1 : -1;
        bool jump = step.Traversal == EnemyLink.Traversal.Jump;
        bool airborne = currentGround == null;
        if (airborne) linkLeftGround = true;

        if (jump)
        {
            Move(0f, 0f);
            Face(direction);
        }
        else if (!linkLeftGround)
        {
            Move(direction, 1f);
        }
        else
        {
            float input = GetFallSteering(step.To);
            if (Mathf.Approximately(input, 0f)) Move(0f, 0f);
            else Move(Mathf.Sign(input), Mathf.Abs(input));
        }

        float elapsed = Time.time - linkStartTime;
        bool landed = !airborne && linkLeftGround;
        bool stuck = linkLeftGround ? elapsed > LinkTimeout : elapsed > (jump ? LeapTakeoffTimeout : WalkOffTimeout);

        if (landed)
        {
            // Stop where the jump lands instead of sliding on.
            if (jump) movement.StopHorizontalMomentum();

            bool arrived = EnemyLinkPlanner.SamePlatform(currentGround, step.ToGround, groundMask);
            activeLink = default;
            if (arrived)
            {
                routeIndex++;
            }
            else
            {
                blockedLinks[step.Link] = Time.time + LinkBlockTime;
                hasRoute = false;
            }
        }
        else if (stuck)
        {
            blockedLinks[step.Link] = Time.time + LinkBlockTime;
            activeLink = default;
            hasRoute = false;
        }
    }

    /// <summary>
    /// Horizontal input that brings the enemy to target.x by the time it has fallen to target.y,
    /// recomputed every step so it corrects itself.
    /// </summary>
    private float GetFallSteering(Vector2 target)
    {
        float gravity = Gravity;
        float riseSpeed = movement.InputVelocity.y;
        float drop = Feet - target.y;
        // Time until the feet reach the target's height: drop = -v t + g t^2 / 2.
        float timeLeft = gravity > 0f ? (riseSpeed + Mathf.Sqrt(Mathf.Max(0f, riseSpeed * riseSpeed + 2f * gravity * drop))) / gravity : 0f;

        float wantedSpeed = (target.x - selfCollider.bounds.center.x) / Mathf.Max(timeLeft, Time.fixedDeltaTime);
        return Mathf.Clamp(wantedSpeed / Mathf.Max(enemyData.speed, 0.01f), -1f, 1f);
    }

    private void ResetPathing()
    {
        playerGround = null;
        route.Clear();
        blockedLinks.Clear();
        hasRoute = false;
        activeLink = default;
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
        // The jump attack rejects landings deeper than GapDepth anyway, so there is no need to follow the arc further down.
        float lowestY = self.center.y - GapDepth * 2f;

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

    private void AttackPlayer()
    {
        Move(0f, 0f);

        // Turn toward the player only between attacks, so jumping over the enemy mid-attack doesn't flip it.
        if (attack != null && attack.IsAttacking) return;

        Face(lastKnownPlayerPosition.x - transform.position.x);
        if (attack != null) attack.TryAttack(getLastKnownPlayerPosition, enemyData);
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

        // The planned route: the links still to take (magenta), ending at the goal, green if it is the player's
        // platform and red if the player can't be reached and the enemy waits there.
        if (hasRoute && selfCollider != null)
        {
            Gizmos.color = Color.magenta;
            Vector2 position = new Vector2(selfCollider.bounds.center.x, Feet);
            for (int i = routeIndex; i < route.Count; i++)
            {
                Gizmos.DrawLine(position, route[i].From);
                Gizmos.DrawLine(route[i].From, route[i].To);
                position = route[i].To;
            }

            Gizmos.color = routeReachesPlayer ? Color.green : Color.red;
            Gizmos.DrawWireSphere(routeReachesPlayer ? position : routeGoal, 0.2f);
        }
    }
}
