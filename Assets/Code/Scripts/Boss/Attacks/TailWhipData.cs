using UnityEngine;

/// <summary>
/// Settings for TailWhipAttack: a low hitbox that lashes out along the ground toward the player, so they have to
/// jump over it.
/// </summary>
[CreateAssetMenu(fileName = "TailWhipData", menuName = "Scriptable Objects/Boss/Attacks/Tail Whip")]
public class TailWhipData : BossAttackData
{
    [Header("Tail Whip")]
    [Tooltip("Where the tail starts, measured from the boss's front edge (positive = in front of it). Negative values start it inside the body, so a player hugging the boss is hit too.")]
    public float startOffset = -0.3f;
    [Tooltip("How far the tail reaches past its start when fully out.")]
    [Min(0.1f)] public float reach = 4.5f;
    [Tooltip("Height of the hitbox's center above the boss's feet. Keep it low so a jump clears it.")]
    [Min(0f)] public float heightAboveFeet = 0.3f;
    [Tooltip("Height of the hitbox. Keep it below the player's jump height.")]
    [Min(0.05f)] public float thickness = 0.5f;

    [Header("Timing")]
    [Tooltip("Seconds the tail takes to lash out to full reach.")]
    [Min(0.01f)] public float extendTime = 0.18f;
    [Tooltip("Seconds it stays fully out.")]
    [Min(0f)] public float holdTime = 0.15f;
    [Tooltip("Seconds it takes to pull back. It still deals damage while pulling back.")]
    [Min(0.01f)] public float retractTime = 0.35f;

    private void Reset()
    {
        minRange = 2f;
        maxRange = 4f;
        maxHeightDifference = 1f;
        windup = 0.55f;
        recovery = 0.4f;
        cooldown = 3f;
        damage = 25f;
    }
}
