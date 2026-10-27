using UnityEngine;

/// <summary>
/// Damage over time, added at runtime to whatever was hit by a poisonous arrow (see ArrowData's Poison section).
/// Deals its damage every interval until the poison time runs out, then removes itself.
/// It only calls TakeDamage, so poison ticks never cause knockback.
/// Poisoning a target that is already poisoned restarts the timer instead of stacking.
/// </summary>
public class PoisonEffect : MonoBehaviour
{
    private IDamageable target;
    private float damagePerTick;
    private float interval;
    private float remainingTime;
    private float tickTimer;

    public float RemainingTime => remainingTime;

    /// <summary>
    /// Poisons the target, or refreshes the poison if it is already poisoned.
    /// </summary>
    public static void Apply(IDamageable target, float damagePerTick, float duration, float interval)
    {
        Component targetComponent = target as Component;
        if (targetComponent == null || duration <= 0f || damagePerTick <= 0f)
            return;

        PoisonEffect poison = targetComponent.GetComponent<PoisonEffect>();
        if (poison == null)
        {
            poison = targetComponent.gameObject.AddComponent<PoisonEffect>();
            // First tick one interval after the hit (the hit itself already did damage).
            poison.tickTimer = interval;
        }

        poison.target = target;
        poison.damagePerTick = damagePerTick;
        poison.interval = Mathf.Max(0.05f, interval);
        poison.remainingTime = duration;
    }

    private void Update()
    {
        // The target was destroyed (Unity's null check, through the interface).
        if (target == null || (target is Object targetObject && targetObject == null))
        {
            Destroy(this);
            return;
        }

        remainingTime -= Time.deltaTime;
        tickTimer -= Time.deltaTime;

        if (tickTimer <= 0f)
        {
            tickTimer += interval;
            target.TakeDamage(damagePerTick);
        }

        if (remainingTime <= 0f)
            Destroy(this);
    }
}
