using UnityEngine;

/// <summary>
/// A soul orb dropped by a dead enemy. The player collects it by touching it.
/// Needs a trigger collider and a layer that touches the player's layer (e.g. "Triggers", like the altar).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Soul : MonoBehaviour
{
    [Tooltip("Souls added to the player's inventory when collected.")]
    [SerializeField, Min(1)] private int value = 1;

    [Header("Hover")]
    [Tooltip("Height above the ground the soul floats at.")]
    [SerializeField, Min(0f)] private float hoverHeight = 0.75f;
    [Tooltip("Speed (units per second) the soul sinks or rises to its hover height after spawning. 0 = jump there instantly.")]
    [SerializeField, Min(0f)] private float settleSpeed = 4f;
    [Tooltip("How far below the spawn point to look for ground. If none is found, the soul stays where it spawned.")]
    [SerializeField, Min(0f)] private float maxGroundDistance = 30f;
    [Tooltip("Layers the soul hovers above. Empty = Environment.")]
    [SerializeField] private LayerMask groundLayers;

    private bool collected;
    private Vector3 hoverPosition;

    public int Value => value;

    private void Awake()
    {
        var trigger = GetComponent<Collider2D>();
        if (!trigger.isTrigger)
        {
            Debug.LogWarning("Soul's collider should be a trigger; making it one.", this);
            trigger.isTrigger = true;
        }

        if (groundLayers.value == 0)
            groundLayers = LayerMask.GetMask("Environment");
    }

    private void Start()
    {
        // Find the ground under the spawn point once; enemies dying in the air (e.g. Kilili) drop their soul to it.
        hoverPosition = transform.position;
        RaycastHit2D ground = Physics2D.Raycast(transform.position, Vector2.down, maxGroundDistance, groundLayers);
        if (ground.collider != null)
            hoverPosition.y = ground.point.y + hoverHeight;

        if (settleSpeed <= 0f)
            transform.position = hoverPosition;
    }

    private void Update()
    {
        if (transform.position != hoverPosition)
            transform.position = Vector3.MoveTowards(transform.position, hoverPosition, settleSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;

        var inventory = other.GetComponentInParent<SoulInventory>();
        if (inventory == null && other.attachedRigidbody != null)
            inventory = other.attachedRigidbody.GetComponent<SoulInventory>();
        if (inventory == null) return;

        // Several player colliders can enter in the same step; only count the soul once.
        collected = true;
        inventory.Add(value);
        Destroy(gameObject);
    }
}
