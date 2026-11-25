using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// An ambush: when the player walks into this object's trigger Collider2D, each listed enemy prefab is spawned
/// standing on its Start waypoint and leaps along an exact arc (like an EnemyLink jump) onto its End waypoint.
/// During the leap the enemy can't be damaged, doesn't attack and ignores the player (EnemyBehavior's Ambush state);
/// once it lands it chases the player like any other enemy.
/// Waypoints are plain Transforms (e.g. empty children) and snap to the ground below them, like EnemyLink points.
/// The arcs are drawn in the Scene view when this object is selected.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyAmbush : MonoBehaviour
{
    [Serializable]
    private class AmbushEnemy
    {
        [Tooltip("Enemy prefab to spawn. Its root needs an EnemyBehavior.")]
        public EnemyBehavior prefab;
        [Tooltip("Where the enemy spawns (its feet). Snaps to the ground below unless Snap Start To Ground is off.")]
        public Transform start;
        [Tooltip("Where the leap lands (the enemy's feet). Snaps to the ground below.")]
        public Transform end;
        [Tooltip("Seconds after the trigger before this enemy spawns, to stagger the ambush.")]
        [Min(0f)] public float delay;
    }

    [Tooltip("The enemies of this ambush, each with its own spawn and landing waypoint.")]
    [SerializeField] private AmbushEnemy[] enemies = Array.Empty<AmbushEnemy>();
    [Tooltip("How far above the higher of Start and End the leaps peak. Raise it if an arc clips a platform's corner.")]
    [SerializeField, Min(0f)] private float arcHeight = 1.5f;
    [Tooltip("Snap Start to the ground below it. Turn off to have enemies drop in from a point in the air.")]
    [SerializeField] private bool snapStartToGround = true;
    [Tooltip("Layers the waypoints snap to. Defaults to \"Environment\" if left empty.")]
    [SerializeField] private LayerMask groundLayers;
    [Tooltip("Spawned enemies are parented under this object. Empty = scene root.")]
    [SerializeField] private Transform spawnParent;

    private bool triggered;

    /// <summary>Raised when the player sets off the ambush, before the first enemy spawns.</summary>
    public event Action Triggered;
    /// <summary>Raised for each enemy as it spawns and starts its leap.</summary>
    public event Action<EnemyBehavior> EnemySpawned;

    public bool HasTriggered => triggered;

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger == null || !trigger.isTrigger)
            Debug.LogError($"{name}: EnemyAmbush needs a trigger Collider2D on the same object, or the player can't set it off.", this);

        if (groundLayers.value == 0) groundLayers = LayerMask.GetMask("Environment");

        for (int i = 0; i < enemies.Length; i++)
        {
            AmbushEnemy entry = enemies[i];
            if (entry == null || entry.prefab == null || entry.start == null || entry.end == null)
                Debug.LogError($"{name}: Ambush enemy {i} is missing its prefab, Start or End, and will be skipped.", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || !IsPlayer(other)) return;
        triggered = true;
        Triggered?.Invoke();

        foreach (AmbushEnemy entry in enemies)
        {
            if (entry == null || entry.prefab == null || entry.start == null || entry.end == null) continue;

            if (entry.delay > 0f) StartCoroutine(SpawnAfter(entry));
            else Spawn(entry);
        }
    }

    private IEnumerator SpawnAfter(AmbushEnemy entry)
    {
        yield return new WaitForSeconds(entry.delay);
        Spawn(entry);
    }

    private void Spawn(AmbushEnemy entry)
    {
        Vector2 start = GetStart(entry);
        Vector2 end = EnemyLink.SnapToGround(entry.end.position, groundLayers, out _);

        EnemyBehavior enemy = Instantiate(entry.prefab, start, Quaternion.identity, spawnParent);
        PlaceFeetAt(enemy, start);
        enemy.BeginAmbush(end, arcHeight);
        EnemySpawned?.Invoke(enemy);
    }

    /// <summary>
    /// Moves the enemy so the bottom center of its collider is on point (the prefab's pivot may not be at its feet),
    /// then syncs physics and the visual interpolation so nothing sees or draws the old position.
    /// </summary>
    private static void PlaceFeetAt(EnemyBehavior enemy, Vector2 point)
    {
        Collider2D body = enemy.GetComponent<Collider2D>();
        if (body == null) return;

        Physics2D.SyncTransforms();
        Bounds bounds = body.bounds;
        Vector3 offset = new Vector3(point.x - bounds.center.x, point.y - bounds.min.y, 0f);
        enemy.transform.position += offset;
        Physics2D.SyncTransforms();

        MotionInterpolator interpolator = enemy.GetComponent<MotionInterpolator>();
        if (interpolator != null) interpolator.Snap();
    }

    private Vector2 GetStart(AmbushEnemy entry) =>
        snapStartToGround ? EnemyLink.SnapToGround(entry.start.position, groundLayers, out _) : (Vector2)entry.start.position;

    private static bool IsPlayer(Collider2D other)
    {
        return other.CompareTag("Player")
            || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"));
    }

    private void OnDrawGizmosSelected()
    {
        if (enemies == null) return;

        foreach (AmbushEnemy entry in enemies)
        {
            if (entry == null || entry.start == null || entry.end == null) continue;

            Vector2 start = snapStartToGround
                ? EnemyLink.SnapToGround(entry.start.position, groundLayers, out _)
                : (Vector2)entry.start.position;
            Vector2 end = EnemyLink.SnapToGround(entry.end.position, groundLayers, out Collider2D endGround);

            Gizmos.color = new Color(1f, 0.25f, 0.25f);
            Gizmos.DrawWireSphere(start, 0.2f);
            Gizmos.color = endGround != null ? Color.cyan : Color.red;
            Gizmos.DrawWireSphere(end, 0.2f);

            Gizmos.color = new Color(1f, 0.25f, 0.25f);
            EnemyLink.DrawArcGizmo(start, end, true, arcHeight);
        }
    }
}
