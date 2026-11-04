using UnityEngine;

/// <summary>Settings for BossSlashAttack: a close range swing, the same as the melee enemies' slash.</summary>
[CreateAssetMenu(fileName = "BossSlashData", menuName = "Scriptable Objects/Boss/Attacks/Slash")]
public class BossSlashData : BossAttackData
{
    [Header("Slash")]
    [Tooltip("Point the hitbox swings around, relative to the boss's feet (bottom center of its collider) when facing right. Mirrored when facing left.")]
    public Vector2 originOffset = new Vector2(0.5f, 1.2f);
    [Tooltip("Total angle (degrees) the hitbox sweeps through, centered on the direction to the player. It swings from top to bottom.")]
    [Range(0f, 360f)] public float arcAngle = 140f;
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
