using UnityEngine;

/// <summary>
/// Settings every boss attack has: when the brain may pick it (range, height, weight), its timing and its damage.
/// Each attack type derives its own asset from this with its extra settings. The same asset type with different
/// numbers gives a variation of the attack (e.g. a longer, slower tail whip for a bigger boss), so new bosses can
/// reuse the existing attacks instead of starting from scratch.
/// </summary>
public abstract class BossAttackData : ScriptableObject
{
    [Header("Selection")]
    [Tooltip("The attack can be picked when the gap between the boss's and the player's colliders is between Min Range and Max Range (meters).")]
    [Min(0f)] public float minRange = 0f;
    [Min(0f)] public float maxRange = 2f;
    [Tooltip("The player's feet can be at most this much higher or lower than the boss's feet.")]
    [Min(0f)] public float maxHeightDifference = 3f;
    [Tooltip("Chance of being picked compared to the other attacks that fit (two attacks with weight 1 = 50/50).")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("Weight used instead when the player is inside the boss (colliders overlap). Range and height are ignored then. 0 = never used while the player is inside.")]
    [Min(0f)] public float insideWeight = 0f;
    [Tooltip("Only picked while the boss stands on the ground.")]
    public bool requiresGround = true;

    [Header("Timing")]
    [Tooltip("Seconds the boss telegraphs the attack, standing still facing the player, before it hits. Gives the player time to react.")]
    [Min(0f)] public float windup = 0.5f;
    [Tooltip("Seconds the boss stands still after the attack before it moves again. A punish window for the player.")]
    [Min(0f)] public float recovery = 0.4f;
    [Tooltip("Seconds after this attack ends before it can be picked again. Other attacks can be used in the meantime.")]
    [Min(0f)] public float cooldown = 3f;

    [Header("Damage")]
    [Min(0f)] public float damage = 20f;

    protected virtual void OnValidate()
    {
        if (maxRange < minRange) maxRange = minRange;
    }
}
