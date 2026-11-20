using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A patch of fire that burns for a while and then goes out, e.g. spawned by a falling lantern
/// (<see cref="FallingHazard"/>'s Spawn On Landing). The damage itself is a <see cref="DamageHitbox"/> on this
/// object or a child, set to Active On Enable and Hit Mode Repeating, so anything inside takes damage every
/// Rehit Interval and stops burning as soon as it steps out. Turn on the hitbox's Alerts Enemies so burned enemies
/// start chasing the player, who set the fire.
/// When the time is up, the hitbox switches off, particle systems stop emitting, and the object is destroyed
/// once the last particles have had time to fade.
/// </summary>
public class FireArea : MonoBehaviour
{
    [Tooltip("Seconds the fire burns and deals damage. 0 = burns forever.")]
    [Min(0f)]
    [SerializeField] private float duration = 5f;
    [Tooltip("Seconds between the fire going out and the object being destroyed, so particles and fading visuals can finish.")]
    [Min(0f)]
    [SerializeField] private float fadeOutTime = 1f;
    [Tooltip("The damage area. If empty, the first DamageHitbox on this object or its children.")]
    [SerializeField] private DamageHitbox hitbox;
    [Tooltip("Called when the fire goes out (the damage stops).")]
    [SerializeField] private UnityEvent onExtinguished = new UnityEvent();

    public bool IsBurning { get; private set; }

    private void Awake()
    {
        if (hitbox == null)
            hitbox = GetComponentInChildren<DamageHitbox>(true);
        if (hitbox == null)
            Debug.LogError($"{name}: FireArea needs a DamageHitbox (Active On Enable, Repeating) on itself or a child, or it deals no damage.", this);
    }

    private void Start()
    {
        IsBurning = true;
        if (duration > 0f) StartCoroutine(BurnRoutine());
    }

    private IEnumerator BurnRoutine()
    {
        yield return new WaitForSeconds(duration);
        Extinguish();
    }

    /// <summary>Puts the fire out now: damage stops, particles stop, then the object is destroyed after Fade Out Time.</summary>
    public void Extinguish()
    {
        if (!IsBurning) return;
        IsBurning = false;

        if (hitbox != null) hitbox.enabled = false;
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>())
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        onExtinguished.Invoke();
        Destroy(gameObject, fadeOutTime);
    }
}
