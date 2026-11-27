using System.Collections;
using UnityEngine;

/// <summary>
/// Long range attack: after the windup (standing still facing the player), the boss runs straight at chargeSpeed
/// through the player and on until it hits the arena wall. Runs through the player (it doesn't push them) and damages
/// them at most once. Every charge ends in a stun (wallStunDuration), which is the player's chance to punish it.
/// A ledge or maxChargeTime also end it (with the stun), only as safety stops.
/// Setup: a child of the boss with a Collider2D and a DamageHitbox, placed in front of the boss as if facing right.
/// It is mirrored to the side the boss charges toward. Keep it out of the Visual Root.
/// </summary>
public class BossChargeAttack : BossAttack<BossChargeData>
{
    // Extra look-ahead for the ledge check, so the boss stops before its front foot leaves the floor.
    private const float LedgeCheckMargin = 0.05f;

    [Tooltip("Hitbox on a child of the boss, placed in front of it as if facing right. Mirrored to the side the boss charges toward.")]
    [SerializeField] private DamageHitbox hitbox;

    private Vector3 hitboxLocalPosition;

    protected override void Awake()
    {
        base.Awake();

        if (hitbox == null)
        {
            Debug.LogError($"{name}: BossChargeAttack needs a Hitbox assigned. Disabling.", this);
            enabled = false;
            return;
        }

        hitboxLocalPosition = hitbox.transform.localPosition;
    }

    /// <summary>Skips the charge when a wall or ledge is so close it would end straight away.</summary>
    protected override bool CanUse(in BossTargetInfo target)
    {
        float runway = Mathf.Min(data.minRunwayLength, target.HorizontalGap);
        return !Boss.IsWallAhead(target.Side, runway) && !Boss.IsLedgeAhead(target.Side, runway);
    }

    protected override IEnumerator Perform(BossTargetInfo target)
    {
        // Knocked off the ground during the windup: cancel instead of charging in mid-air.
        if (!Boss.IsOnGround) yield break;

        int direction = Boss.FacingDirection;
        float giveUpTime = Time.time + data.maxChargeTime;

        hitbox.transform.localPosition = new Vector3(hitboxLocalPosition.x * direction, hitboxLocalPosition.y, hitboxLocalPosition.z);
        hitbox.Activate(data.damage, data.knockback, default);
        ControlsMovement = true;

        // Run until the arena wall. A ledge or the time limit are only safety stops; the boss is stunned either way.
        while (true)
        {
            float step = data.chargeSpeed * Time.fixedDeltaTime;

            bool atWall = Boss.IsWallAhead(direction, step);
            bool atLedge = Boss.IsOnGround && Boss.IsLedgeAhead(direction, step + LedgeCheckMargin);
            if (atWall || atLedge || Time.time >= giveUpTime)
            {
                StopRunning();
                Boss.Stun(data.wallStunDuration);
                yield break;
            }

            Boss.Move(direction, data.chargeSpeed / Boss.MoveSpeed);

            yield return NextFixedUpdate;
        }
    }

    private void StopRunning()
    {
        Boss.Stop();
        Boss.Movement.StopHorizontalMomentum();
        ControlsMovement = false;
        hitbox.Deactivate();
    }

    protected override void Cleanup()
    {
        if (hitbox != null) hitbox.Deactivate();
        if (Boss != null && ControlsMovement && Boss.Movement != null)
        {
            Boss.Stop();
            Boss.Movement.StopHorizontalMomentum();
        }
    }
}
