using UnityEngine;

/// <summary>
/// Settings for BossSlashAttack: a close range swing, the same as the melee enemies' slash.
/// The swing's path is set on the BossSlashAttack component, not here.
/// </summary>
[CreateAssetMenu(fileName = "BossSlashData", menuName = "Scriptable Objects/Boss/Attacks/Slash")]
public class BossSlashData : BossAttackData
{
    [Header("Slash")]
    [Tooltip("Seconds the swing takes. The hitbox is active for this long.")]
    [Min(0.01f)] public float swingDuration = 0.3f;

    private void Reset()
    {
        minRange = 0f;
        maxRange = 2f;
        maxHeightDifference = 2.5f;
        windup = 0.45f;
        recovery = 0.35f;
        cooldown = 1.2f;
        damage = 20f;
    }
}
