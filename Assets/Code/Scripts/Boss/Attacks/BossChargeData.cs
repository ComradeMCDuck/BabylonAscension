using UnityEngine;

/// <summary>Settings for BossChargeAttack: a straight bull charge through the player, like Humbaba's.</summary>
[CreateAssetMenu(fileName = "BossChargeData", menuName = "Scriptable Objects/Boss/Attacks/Charge")]
public class BossChargeData : BossAttackData
{
    [Header("Charge")]
    [Tooltip("Running speed during the charge (m/s). Independent of the normal movement speed.")]
    [Min(0.01f)] public float chargeSpeed = 14f;
    [Tooltip("The charge stops (without a stun) after running this far past where it started, or earlier at a ledge.")]
    [Min(0f)] public float chargeDistance = 12f;
    [Tooltip("Keep running this far past the player's position at the start, so the boss ends up behind them. Caps Charge Distance.")]
    [Min(0f)] public float overshoot = 3f;
    [Tooltip("Seconds the boss is stunned (can't move or attack) after charging into a wall. 0 = no stun.")]
    [Min(0f)] public float wallStunDuration = 2.5f;
    [Tooltip("Only charge when there is at least this much floor between the boss and the player, so it doesn't start a charge that ends at once.")]
    [Min(0f)] public float minRunwayLength = 2f;

    private void Reset()
    {
        minRange = 4f;
        maxRange = 20f;
        maxHeightDifference = 1f;
        windup = 0.7f;
        recovery = 0.5f;
        cooldown = 6f;
        damage = 40f;
    }
}
