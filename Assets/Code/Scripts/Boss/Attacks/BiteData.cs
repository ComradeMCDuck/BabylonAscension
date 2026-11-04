using UnityEngine;

/// <summary>
/// Settings for BiteAttack: the head shoots out along a diagonal from a start point to an end point and back.
/// Both points are relative to the boss's feet when facing right; select the boss to see them as gizmos.
/// </summary>
[CreateAssetMenu(fileName = "BiteData", menuName = "Scriptable Objects/Boss/Attacks/Bite")]
public class BiteData : BossAttackData
{
    [Header("Bite")]
    [Tooltip("Where the head starts (the neck's base), relative to the boss's feet (bottom center of its collider) when facing right. Mirrored when facing left.")]
    public Vector2 startOffset = new Vector2(1f, 2.5f);
    [Tooltip("Where the head ends up when the neck is fully stretched, relative to the boss's feet when facing right. With Aim At Player this is only used when there is no player.")]
    public Vector2 endOffset = new Vector2(4.5f, 0.4f);
    [Tooltip("Bite toward where the player was when the windup started, instead of the fixed End Offset.")]
    public bool aimAtPlayer = true;
    [Tooltip("With Aim At Player: the longest the neck can stretch from the start point. A player further away is bitten short, so they can dash back out of reach.")]
    [Min(0.1f)] public float maxReach = 4.5f;
    [Tooltip("With Aim At Player: how far past the player's position the head aims, along the bite, so it doesn't stop just at their center.")]
    [Min(0f)] public float aimOvershoot = 0.3f;
    [Tooltip("With Aim At Player: the steepest the bite can go, in degrees below (negative) or above (positive) horizontal. Keeps it from biting straight down into the boss's own feet or straight up.")]
    [Range(-90f, 0f)] public float minAimAngle = -70f;
    [Range(0f, 90f)] public float maxAimAngle = 30f;

    [Header("Timing")]
    [Tooltip("Seconds the neck takes to stretch out.")]
    [Min(0.01f)] public float extendTime = 0.2f;
    [Tooltip("Seconds the head stays at the end point.")]
    [Min(0f)] public float holdTime = 0.25f;
    [Tooltip("Seconds the head takes to pull back. It doesn't deal damage while pulling back.")]
    [Min(0.01f)] public float retractTime = 0.4f;

    [Header("Neck Visual (optional)")]
    [Tooltip("Thickness of the neck sprite stretched between the start point and the head.")]
    [Min(0.01f)] public float neckThickness = 0.6f;

    private void Reset()
    {
        minRange = 2f;
        maxRange = 4f;
        maxHeightDifference = 2.5f;
        windup = 0.5f;
        recovery = 0.5f;
        cooldown = 3f;
        damage = 30f;
    }
}
