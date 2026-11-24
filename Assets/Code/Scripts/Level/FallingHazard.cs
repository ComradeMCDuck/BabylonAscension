using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A level object hanging on a rope (a rock, a crate, a lantern) that the player shoots down onto enemies.
/// It hangs still until Release (usually the rope's <see cref="ArrowTarget"/>), then falls straight down and
/// stops on the first Environment collider it touches. On landing it raises On Landed (e.g. WeakFloor >
/// SetActive(false)) and can spawn a prefab at the impact point (e.g. the fire of a lantern).
/// While falling, the child Fall Hitbox is enabled: a <see cref="DamageHitbox"/> set to Active On Enable, so it
/// hurts what it touches. Turn on the hitbox's Alerts Enemies so a hit enemy starts chasing the player.
/// It moves itself with a collider cast each physics step, like the character controllers, so it can't fall
/// through thin floors and needs no dynamic Rigidbody2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FallingHazard : MonoBehaviour
{
    public enum AfterLandingMode
    {
        /// <summary>Stays where it landed (e.g. a crate that becomes something to stand on).</summary>
        Stay,
        /// <summary>Is destroyed after Disappear Delay (e.g. a lantern that shatters).</summary>
        Disappear,
    }

    [Header("Release")]
    [Tooltip("The rope. Shooting it releases this object. Can be empty: then call Release() from any UnityEvent.")]
    [SerializeField] private ArrowTarget rope;
    [Tooltip("Hides the rope object when the object is released, as if the arrow cut it.")]
    [SerializeField] private bool hideRopeOnRelease = true;

    [Header("Fall")]
    [Tooltip("The solid shape that falls and lands. If empty, the first non-trigger Collider2D on this object or its children.")]
    [SerializeField] private Collider2D body;
    [Tooltip("Layers it lands on. Defaults to \"Environment\" if left empty.")]
    [SerializeField] private LayerMask landingLayers;
    [Tooltip("Downward acceleration in units/s².")]
    [Min(0f)]
    [SerializeField] private float gravity = 40f;
    [Tooltip("Fall speed cap in units/s.")]
    [Min(0.1f)]
    [SerializeField] private float maxFallSpeed = 30f;

    [Header("Damage")]
    [Tooltip("DamageHitbox (Active On Enable on, Hit Mode Once Per Activation, usually on a child) that hurts what the object falls on. The component is disabled while hanging and after landing. Set its Target Layers to Player, Triggers and Enemy so it hits both, and turn on Alerts Enemies so hit enemies chase the player.")]
    [SerializeField] private DamageHitbox fallHitbox;

    [Header("Landing")]
    [Tooltip("Spawned at the impact point on landing, e.g. the lantern's fire area. Its pivot should be at its bottom center.")]
    [SerializeField] private GameObject spawnOnLanding;
    [Tooltip("Stay: remains where it landed. Disappear: destroyed after Disappear Delay.")]
    [SerializeField] private AfterLandingMode afterLanding = AfterLandingMode.Stay;
    [Tooltip("Disappear only: seconds after landing before it is destroyed.")]
    [Min(0f)]
    [SerializeField] private float disappearDelay;
    [Tooltip("Called once when the object lands, e.g. WeakFloor > GameObject.SetActive(false).")]
    [SerializeField] private UnityEvent onLanded = new UnityEvent();

    // Small gap kept between the body and the ground so the next cast doesn't start inside it.
    private const float SkinWidth = 0.01f;

    private static readonly List<RaycastHit2D> castHits = new List<RaycastHit2D>();

    private Rigidbody2D rb;
    private ContactFilter2D landingFilter;
    private float fallSpeed;

    public bool IsFalling { get; private set; }
    public bool HasLanded { get; private set; }

    /// <summary>Same as On Landed, for code. Passes the collider it landed on.</summary>
    public event Action<FallingHazard, Collider2D> Landed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Kinematic: it moves only where this script puts it, and pushes nothing.
        rb.bodyType = RigidbodyType2D.Kinematic;

        if (body == null)
        {
            foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true))
            {
                if (!col.isTrigger) { body = col; break; }
            }
        }
        if (body == null)
        {
            Debug.LogError($"{name}: FallingHazard needs a non-trigger Collider2D (Body) to land with.", this);
            enabled = false;
            return;
        }

        if (landingLayers.value == 0)
            landingLayers = LayerMask.GetMask("Environment");
        landingFilter = new ContactFilter2D { useTriggers = false };
        landingFilter.SetLayerMask(landingLayers);

        if (fallHitbox != null)
            fallHitbox.enabled = false;
    }

    private void OnEnable()
    {
        if (rope != null) rope.Hit += OnRopeHit;
    }

    private void OnDisable()
    {
        if (rope != null) rope.Hit -= OnRopeHit;
    }

    private void OnRopeHit(ArrowProjectile arrow) => Release();

    /// <summary>Cuts the rope: the object starts falling. Does nothing if it is already falling or has landed.</summary>
    public void Release()
    {
        if (!enabled || IsFalling || HasLanded) return;

        IsFalling = true;
        fallSpeed = 0f;

        if (hideRopeOnRelease && rope != null)
            rope.gameObject.SetActive(false);
        if (fallHitbox != null)
            fallHitbox.enabled = true;
    }

    private void FixedUpdate()
    {
        if (!IsFalling) return;

        fallSpeed = Mathf.Min(fallSpeed + gravity * Time.fixedDeltaTime, maxFallSpeed);
        float distance = fallSpeed * Time.fixedDeltaTime;

        Physics2D.SyncTransforms();
        RaycastHit2D ground = CastDown(distance + SkinWidth);

        if (ground.collider != null)
        {
            transform.position += Vector3.down * Mathf.Max(0f, ground.distance - SkinWidth);
            Land(ground);
        }
        else
        {
            transform.position += Vector3.down * distance;
            if (fallHitbox != null) fallHitbox.CheckHits();
        }
    }

    /// <summary>Closest landing-layer hit below the body, ignoring this object's own colliders (e.g. a child rope).</summary>
    private RaycastHit2D CastDown(float distance)
    {
        castHits.Clear();
        body.Cast(Vector2.down, landingFilter, castHits, distance, true);

        RaycastHit2D closest = default;
        foreach (RaycastHit2D hit in castHits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            // Already overlapping or below the bottom (e.g. a wall it hangs beside): not something to land on.
            if (hit.normal.y <= 0f) continue;
            if (closest.collider == null || hit.distance < closest.distance) closest = hit;
        }
        return closest;
    }

    private void Land(RaycastHit2D ground)
    {
        IsFalling = false;
        HasLanded = true;

        // One last check at the final spot, so whoever stands right where it lands is hit too.
        if (fallHitbox != null)
        {
            fallHitbox.CheckHits();
            fallHitbox.enabled = false;
        }

        if (spawnOnLanding != null)
            Instantiate(spawnOnLanding, new Vector3(ground.point.x, ground.point.y, transform.position.z), Quaternion.identity);

        onLanded.Invoke();
        Landed?.Invoke(this, ground.collider);

        if (afterLanding == AfterLandingMode.Disappear)
            Destroy(gameObject, disappearDelay);
    }
}
