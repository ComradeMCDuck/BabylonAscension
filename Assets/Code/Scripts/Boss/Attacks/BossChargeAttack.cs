using System.Collections;
using UnityEngine;

/// <summary>
/// Long range attack: after the windup (standing still facing the player), the boss runs straight at chargeSpeed
/// through the player and past them. Runs through the player (it doesn't push them) and damages them at most once.
/// It stops at a ledge, after chargeDistance, or overshoot past where the player stood. Running into a wall stops it
/// and stuns the boss for wallStunDuration, which is the player's chance to punish it.
/// Setup: a child of the boss with a Collider2D and a DamageHitbox, placed in front of the boss as if facing right.
/// It is mirrored to the side the boss charges toward. Keep it out of the Visual Root.
/// </summary>
public class BossChargeAttack : BossAttack<BossChargeData>
{
    // Extra seconds before a charge that got stuck on something (neither a wall nor a ledge) is given up.
    private const float StuckGraceTime = 1f;

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
        float startX = Boss.transform.position.x;
        // Run past where the player stood at the start of the windup, but never further than chargeDistance.
        float toPlayer = Mathf.Max(0f, (target.PlayerCenter.x - Boss.SelfCollider.bounds.center.x) * direction);
        float distance = Mathf.Min(data.chargeDistance, toPlayer + data.overshoot);
        float giveUpTime = Time.time + distance / data.chargeSpeed + StuckGraceTime;

        hitbox.transform.localPosition = new Vector3(hitboxLocalPosition.x * direction, hitboxLocalPosition.y, hitboxLocalPosition.z);
        hitbox.Activate(data.damage);
        ControlsMovement = true;

        while (true)
        {
            float step = data.chargeSpeed * Time.fixedDeltaTime;

            if (Boss.IsWallAhead(direction, step))
            {
                StopRunning();
                Boss.Stun(data.wallStunDuration);
                yield break;
            }

            float remaining = distance - Mathf.Abs(Boss.transform.position.x - startX);
            bool atLedge = Boss.IsOnGround && Boss.IsLedgeAhead(direction, step + 0.05f);
            if (remaining <= 0f || atLedge || Time.time >= giveUpTime)
            {
                StopRunning();
                yield break;
            }

            // Slow down for the last step, so the charge ends exactly where it should.
            float speed = Mathf.Min(data.chargeSpeed, remaining / Time.fixedDeltaTime);
            Boss.Move(direction, speed / Boss.MoveSpeed);

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
