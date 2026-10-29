using System.Collections.Generic;
using DYP;
using UnityEngine;

/// <summary>
/// Lets a ground enemy take off and fly (Kilili). EnemyBehavior decides when; this component does the flying.
/// While flying, the ground controller (BasicMovementController2D) is switched off, so there is no gravity,
/// and the enemy is moved here instead:
/// - a circle of clearanceRadius around the enemy is swept along every move, so it never flies into the environment,
///   and steering looks ahead with the same circle to fly around obstacles instead of into them;
/// - around the player it hovers at a distance from which it can see them, slowly circling over them;
/// - when it can't see the player it follows the trail of positions where it last saw them;
/// - it remembers its own flight path, so it can always fly back to where it took off and land there.
/// A small up and down bob (wing flaps) is added on top of every movement. Settings live in EntityData ("Flight").
/// </summary>
[RequireComponent(typeof(BasicMovementController2D))]
public class EnemyFlight : MonoBehaviour
{
    // Gap kept from surfaces when a move is stopped by a collision.
    private const float Skin = 0.02f;
    // How close counts as "reached" for trail points and the hover point.
    private const float ArriveDistance = 0.4f;
    // Within this distance of its target the enemy slows down, so it doesn't overshoot.
    private const float SlowDownDistance = 1.5f;
    // Spacing of the remembered points of the player's trail and of the enemy's own path.
    private const float TrailSpacing = 0.75f;
    private const int MaxPlayerTrail = 40;
    private const int MaxOwnPath = 300;
    // Angles tried, left and right of the wanted direction, when the way ahead is blocked.
    private static readonly float[] SteerAngles = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f, 135f, -135f };
    // Steps (in degrees) and distances tried around the player when looking for a free hover point with line of sight.
    private const float HoverSearchStep = 10f;
    private const int HoverDistanceSteps = 3;
    // The return path is re-checked for shortcuts this often (it costs a cast per remembered point).
    private const float ShortcutCheckInterval = 0.25f;

    [Tooltip("Radius of the circle kept free of the environment around the enemy while flying. Make it a bit larger than the enemy's body.")]
    [SerializeField, Min(0.05f)] private float clearanceRadius = 0.6f;
    [Tooltip("Layers the enemy flies around. Defaults to \"Environment\" if left empty.")]
    [SerializeField] private LayerMask obstacleLayers;

    private enum Phase { Grounded, TakingOff, Flying, Descending }

    private readonly List<Vector2> playerTrail = new List<Vector2>();
    private readonly List<Vector2> ownPath = new List<Vector2>();
    private readonly Collider2D[] overlapBuffer = new Collider2D[8];
    private readonly RaycastHit2D[] castBuffer = new RaycastHit2D[4];

    private BasicMovementController2D movement;
    private Collider2D bodyCollider;
    private ContactFilter2D obstacleFilter;
    private Phase phase = Phase.Grounded;
    private Vector2 velocity;
    private Vector2 takeoffPoint;
    private float bobPhaseOffset;
    private float orbitAngle;
    private int orbitDirection = 1;
    private bool hovering;
    private Vector2 hoverTarget;
    private int returnTargetIndex = -1;
    private float nextShortcutCheckTime;

    /// <summary>True from takeoff until the enemy has landed again.</summary>
    public bool IsFlying => phase != Phase.Grounded;
    /// <summary>Where the enemy is currently trying to fly to (for debugging and gizmos).</summary>
    public Vector2 CurrentTarget { get; private set; }
    public Vector2 Velocity => velocity;

    private void Awake()
    {
        movement = GetComponent<BasicMovementController2D>();
        bodyCollider = GetComponent<Collider2D>();

        if (obstacleLayers.value == 0)
            obstacleLayers = LayerMask.GetMask("Environment");

        obstacleFilter = new ContactFilter2D { useTriggers = false };
        obstacleFilter.SetLayerMask(obstacleLayers);

        // Different enemies flap out of sync.
        bobPhaseOffset = Random.value * 10f;
    }

    private Vector2 Center => bodyCollider.bounds.center;

    /// <summary>
    /// Switches the ground controller off and rises straight up until the clearance circle is free of the ground.
    /// </summary>
    public void TakeOff()
    {
        if (IsFlying) return;

        movement.InputMovement(Vector2.zero);
        movement.StopHorizontalMomentum();
        movement.enabled = false;

        phase = Phase.TakingOff;
        velocity = Vector2.zero;
        takeoffPoint = transform.position;
        ownPath.Clear();
        returnTargetIndex = -1;
    }

    /// <summary>Remember a position where the player was seen (or gave themselves away by hitting the enemy).</summary>
    public void RememberPlayerPosition(Vector2 position)
    {
        int last = playerTrail.Count - 1;
        if (last >= 0 && (playerTrail[last] - position).sqrMagnitude < TrailSpacing * TrailSpacing) return;

        playerTrail.Add(position);
        if (playerTrail.Count > MaxPlayerTrail) playerTrail.RemoveAt(0);
    }

    /// <summary>
    /// One physics step of chasing the player: hover around them at a distance with line of sight when they are
    /// visible, otherwise follow the trail of positions where they were last seen.
    /// </summary>
    public void FlyTowardPlayer(Vector2 playerCenter, bool canSeePlayer, EntityData data)
    {
        if (RunTakeoff(data)) return;
        phase = Phase.Flying;
        returnTargetIndex = -1;

        Vector2 target;
        if (canSeePlayer)
        {
            // Seen again: the old trail is no longer needed.
            playerTrail.Clear();
            target = ChooseHoverPoint(playerCenter, data);
        }
        else
        {
            hovering = false;
            target = ChooseTrailPoint(playerCenter);
        }

        RecordOwnPath();
        FlyTo(target, data);
    }

    /// <summary>
    /// One physics step of flying back along the remembered path to the takeoff point and landing there.
    /// Returns true once the enemy is on the ground and the ground controller is back on.
    /// </summary>
    public bool FlyHome(EntityData data)
    {
        if (phase == Phase.Grounded) return true;
        if (RunTakeoff(data)) return false;

        if (phase == Phase.Descending) return RunDescent(data);

        phase = Phase.Flying;
        hovering = false;
        playerTrail.Clear();

        // The first point of the path is right above the takeoff point.
        if (ownPath.Count == 0) ownPath.Add(Center);

        if (returnTargetIndex < 0 || Time.time >= nextShortcutCheckTime)
        {
            nextShortcutCheckTime = Time.time + ShortcutCheckInterval;
            returnTargetIndex = FindShortcut();
        }

        Vector2 target = ownPath[returnTargetIndex];
        if ((target - Center).sqrMagnitude <= ArriveDistance * ArriveDistance)
        {
            // Everything flown after this point is no longer needed.
            ownPath.RemoveRange(returnTargetIndex + 1, ownPath.Count - returnTargetIndex - 1);

            if (returnTargetIndex == 0)
            {
                phase = Phase.Descending;
                velocity = Vector2.zero;
                return false;
            }

            returnTargetIndex = FindShortcut();
            target = ownPath[returnTargetIndex];
        }

        FlyTo(target, data);
        return false;
    }

    /// <summary>Knockback while flying: pushes the enemy, and steering brings it back under control.</summary>
    public void ApplyKnockback(Vector2 impulse)
    {
        velocity += impulse;
    }

    /// <summary>
    /// Rises straight up from the ground, moving only the body's own collider (the clearance circle doesn't fit
    /// on the ground yet). Returns true while the takeoff is still running.
    /// </summary>
    private bool RunTakeoff(EntityData data)
    {
        if (phase != Phase.TakingOff) return false;

        float step = data.flySpeed * Time.fixedDeltaTime;
        bool clear = Physics2D.OverlapCircle(Center, clearanceRadius, obstacleFilter, overlapBuffer) == 0;
        bool risenEnough = transform.position.y - takeoffPoint.y >= clearanceRadius * 2f + 0.5f;
        bool blocked = bodyCollider.Cast(Vector2.up, obstacleFilter, castBuffer, step + Skin) > 0;

        if ((clear && risenEnough) || blocked)
        {
            phase = Phase.Flying;
            ownPath.Clear();
            ownPath.Add(Center);
            return false;
        }

        transform.position += Vector3.up * step;
        return true;
    }

    /// <summary>
    /// Sinks straight down onto the takeoff point with the body's collider, then hands control back to the ground controller.
    /// </summary>
    private bool RunDescent(EntityData data)
    {
        float step = data.flySpeed * 0.5f * Time.fixedDeltaTime;
        int hits = bodyCollider.Cast(Vector2.down, obstacleFilter, castBuffer, step + Skin);

        if (hits > 0 || transform.position.y <= takeoffPoint.y)
        {
            if (hits > 0) transform.position += Vector3.down * Mathf.Max(0f, castBuffer[0].distance - Skin);
            Land();
            return true;
        }

        transform.position += Vector3.down * step;
        return false;
    }

    private void Land()
    {
        phase = Phase.Grounded;
        velocity = Vector2.zero;
        hovering = false;
        playerTrail.Clear();
        ownPath.Clear();

        movement.enabled = true;
        movement.StopHorizontalMomentum();
    }

    /// <summary>
    /// A point at flyPreferredDistance from the player, above them, from which the enemy can see them.
    /// The point slowly sweeps from side to side over the player so the enemy keeps circling instead of hanging still.
    /// If that spot is inside a wall or has no line of sight, nearby angles and shorter distances are tried.
    /// </summary>
    private Vector2 ChooseHoverPoint(Vector2 playerCenter, EntityData data)
    {
        float minAngle = Mathf.Min(data.flyMinAngle, data.flyMaxAngle);
        float maxAngle = Mathf.Max(data.flyMinAngle, data.flyMaxAngle);

        // Just started hovering: start from the side the enemy is already on.
        if (!hovering)
        {
            Vector2 fromPlayer = Center - playerCenter;
            orbitAngle = Mathf.Atan2(fromPlayer.y, fromPlayer.x) * Mathf.Rad2Deg;
            hovering = true;
        }

        orbitAngle += orbitDirection * data.flyOrbitSpeed * Time.fixedDeltaTime;
        if (orbitAngle >= maxAngle) orbitDirection = -1;
        else if (orbitAngle <= minAngle) orbitDirection = 1;
        orbitAngle = Mathf.Clamp(orbitAngle, minAngle, maxAngle);

        for (int d = 0; d < HoverDistanceSteps; d++)
        {
            float distance = Mathf.Lerp(data.flyPreferredDistance, data.flyMinDistance, d / (float)(HoverDistanceSteps - 1));

            for (float offset = 0f; offset <= maxAngle - minAngle; offset += HoverSearchStep)
            {
                if (TryHoverPoint(playerCenter, orbitAngle + offset, distance, minAngle, maxAngle, out hoverTarget)) return hoverTarget;
                if (offset > 0f && TryHoverPoint(playerCenter, orbitAngle - offset, distance, minAngle, maxAngle, out hoverTarget)) return hoverTarget;
            }
        }

        // Nowhere free around the player (tight corridor): just close in, steering keeps it out of the walls.
        hoverTarget = playerCenter;
        return playerCenter;
    }

    private bool TryHoverPoint(Vector2 playerCenter, float angle, float distance, float minAngle, float maxAngle, out Vector2 point)
    {
        point = default;
        if (angle < minAngle || angle > maxAngle) return false;

        float radians = angle * Mathf.Deg2Rad;
        point = playerCenter + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * distance;

        if (Physics2D.OverlapCircle(point, clearanceRadius, obstacleFilter, overlapBuffer) > 0) return false;
        if (Physics2D.Linecast(point, playerCenter, obstacleFilter, castBuffer) > 0) return false;

        // Keep circling from the spot that worked.
        orbitAngle = angle;
        return true;
    }

    /// <summary>
    /// The newest remembered player position the enemy can fly straight to; older ones lead around corners.
    /// </summary>
    private Vector2 ChooseTrailPoint(Vector2 lastKnownPosition)
    {
        if (playerTrail.Count == 0) return lastKnownPosition;

        for (int i = playerTrail.Count - 1; i >= 0; i--)
        {
            if (!IsPathClear(Center, playerTrail[i])) continue;

            // Reached the newest point it can see: everything older is behind it.
            if (i > 0) playerTrail.RemoveRange(0, i);
            return playerTrail[0];
        }

        // None directly reachable: head for the oldest one and let steering find the way.
        return playerTrail[0];
    }

    /// <summary>The oldest point of the own path (closest to home) that can be flown to directly.</summary>
    private int FindShortcut()
    {
        for (int i = 0; i < ownPath.Count; i++)
        {
            if (IsPathClear(Center, ownPath[i])) return i;
        }

        return ownPath.Count - 1;
    }

    private void RecordOwnPath()
    {
        Vector2 center = Center;
        int last = ownPath.Count - 1;
        if (last >= 0 && (ownPath[last] - center).sqrMagnitude < TrailSpacing * TrailSpacing) return;

        ownPath.Add(center);

        // Very long flights: keep every other point (but always the first one, above the takeoff point).
        if (ownPath.Count > MaxOwnPath)
        {
            for (int i = ownPath.Count - 2; i >= 1; i -= 2) ownPath.RemoveAt(i);
        }
    }

    /// <summary>
    /// Accelerates toward the target, steering around anything in the way, and moves with collision.
    /// </summary>
    private void FlyTo(Vector2 target, EntityData data)
    {
        CurrentTarget = target;

        Vector2 toTarget = target - Center;
        float distance = toTarget.magnitude;
        Vector2 desired = Vector2.zero;

        if (distance > 0.05f)
        {
            float speed = data.flySpeed * Mathf.Clamp01(distance / SlowDownDistance);
            float lookAhead = Mathf.Min(distance, clearanceRadius + data.flySpeed * 0.3f);
            desired = Steer(toTarget / distance, lookAhead) * speed;
        }

        velocity = Vector2.MoveTowards(velocity, desired, data.flyAcceleration * Time.fixedDeltaTime);

        Move(velocity * Time.fixedDeltaTime + BobStep(data));
    }

    /// <summary>
    /// The wanted direction if the clearance circle can move that way, otherwise the closest direction that is free.
    /// </summary>
    private Vector2 Steer(Vector2 direction, float lookAhead)
    {
        foreach (float angle in SteerAngles)
        {
            Vector2 candidate = Rotate(direction, angle);
            if (Physics2D.CircleCast(Center, clearanceRadius, candidate, obstacleFilter, castBuffer, lookAhead) == 0)
                return candidate;
        }

        return Vector2.zero;
    }

    /// <summary>The change in the wing-flap offset since the last physics step.</summary>
    private Vector2 BobStep(EntityData data)
    {
        float angularFrequency = 2f * Mathf.PI * data.flyBobFrequency;
        float time = Time.time + bobPhaseOffset;
        float now = Mathf.Sin(time * angularFrequency);
        float before = Mathf.Sin((time - Time.fixedDeltaTime) * angularFrequency);
        return Vector2.up * (data.flyBobAmplitude * (now - before));
    }

    /// <summary>
    /// Moves the clearance circle by delta. If something is in the way it stops just before it and slides along the
    /// surface with the rest of the move. Pushes out first if it is already overlapping (e.g. after a knockback).
    /// </summary>
    private void Move(Vector2 delta)
    {
        PushOutOfObstacles();

        for (int i = 0; i < 2 && delta.sqrMagnitude > 0.000001f; i++)
        {
            float length = delta.magnitude;
            Vector2 direction = delta / length;

            if (Physics2D.CircleCast(Center, clearanceRadius, direction, obstacleFilter, castBuffer, length + Skin) == 0)
            {
                transform.position += (Vector3)delta;
                return;
            }

            RaycastHit2D hit = castBuffer[0];
            float travel = Mathf.Max(0f, hit.distance - Skin);
            transform.position += (Vector3)(direction * travel);

            // Slide: drop the part of the move (and the velocity) that goes into the surface.
            Vector2 remaining = delta - direction * travel;
            delta = remaining - hit.normal * Vector2.Dot(remaining, hit.normal);
            float intoSurface = Vector2.Dot(velocity, hit.normal);
            if (intoSurface < 0f) velocity -= hit.normal * intoSurface;
        }
    }

    private void PushOutOfObstacles()
    {
        Vector2 center = Center;
        int count = Physics2D.OverlapCircle(center, clearanceRadius, obstacleFilter, overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            Vector2 closest = overlapBuffer[i].ClosestPoint(center);
            Vector2 away = center - closest;
            float distance = away.magnitude;

            // Center inside the collider: ClosestPoint returns the center itself, so push up out of it.
            Vector2 push = distance > 0.0001f
                ? away / distance * (clearanceRadius - distance + Skin)
                : Vector2.up * (clearanceRadius + Skin);

            transform.position += (Vector3)push;
            center += push;
        }
    }

    private bool IsPathClear(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f) return true;
        return Physics2D.CircleCast(from, clearanceRadius, delta / distance, obstacleFilter, castBuffer, distance) == 0;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private void OnDisable()
    {
        // Never leave the enemy without its ground controller.
        if (IsFlying && movement != null) movement.enabled = true;
        phase = Phase.Grounded;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D body = bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();
        Vector2 center = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;

        Gizmos.color = new Color(0.3f, 0.8f, 1f);
        Gizmos.DrawWireSphere(center, clearanceRadius);

        if (!Application.isPlaying || !IsFlying) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(center, CurrentTarget);
        Gizmos.DrawWireSphere(CurrentTarget, 0.15f);

        Gizmos.color = Color.magenta;
        for (int i = 1; i < playerTrail.Count; i++) Gizmos.DrawLine(playerTrail[i - 1], playerTrail[i]);

        Gizmos.color = Color.white;
        for (int i = 1; i < ownPath.Count; i++) Gizmos.DrawLine(ownPath[i - 1], ownPath[i]);
    }
}
