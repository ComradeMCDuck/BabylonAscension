using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Something an arrow can hit to make things happen in the level: a lamp to shoot down, a switch, a target.
/// Arrows have no collider or Rigidbody2D (they sweep a circle cast each step), so trigger callbacks such as
/// Trigger2D's never fire for them. Instead <see cref="ArrowProjectile"/> looks for this component on what its
/// cast hits. Needs a Collider2D (Is Trigger is fine) on the same object, on a layer arrows check: by default
/// "Ignore Raycast" (ArrowProjectile's Arrow Target Layers).
/// </summary>
public class ArrowTarget : MonoBehaviour
{
    [Tooltip("Only the player's bow shots count. Off = enemy projectiles can set it off too.")]
    [SerializeField] private bool playerArrowsOnly = true;
    [Tooltip("The arrow stops here (and explodes, if explosive). Off = it flies on, like through a trigger.")]
    [SerializeField] private bool stopsArrow = false;
    [Tooltip("Only the first hit counts. Later arrows fly through as if it weren't there.")]
    [SerializeField] private bool triggerOnce = true;

    [Space]
    [Tooltip("Called when a counting arrow hits, e.g. LAMPA > GameObject.SetActive(false).")]
    [SerializeField] private UnityEvent onArrowHit = new UnityEvent();

    private bool used;

    public bool StopsArrow => stopsArrow;

    /// <summary>Same as On Arrow Hit, for code.</summary>
    public event Action<ArrowProjectile> Hit;

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
            Debug.LogError($"{name}: ArrowTarget needs a Collider2D on the same object, or arrows can't hit it.", this);
    }

    /// <summary>
    /// Called by the arrow when its cast hits this target. Returns true if the hit counted (the events ran).
    /// </summary>
    public bool TryHit(ArrowProjectile arrow, bool firedByPlayer)
    {
        if (!isActiveAndEnabled || (triggerOnce && used) || (playerArrowsOnly && !firedByPlayer))
            return false;

        used = true;
        onArrowHit.Invoke();
        Hit?.Invoke(arrow);
        return true;
    }

    /// <summary>Lets a once-only target be hit again, e.g. from a reset lever's event.</summary>
    public void ResetTarget() => used = false;
}
