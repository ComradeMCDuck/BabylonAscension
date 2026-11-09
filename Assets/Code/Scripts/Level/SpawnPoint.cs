using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A named place where the player appears when arriving from another scene. A <see cref="LevelExit"/> in the
/// other scene names this id, e.g. Level 2's back exit sends the player to Level 1's "FromLevel2" spawn, placed
/// next to Level 1's forward exit. Put it just outside any exit's collider, or the player would leave again at once.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Name a LevelExit in another scene uses to send the player here, e.g. \"FromLevel2\". Must be unique in this scene.")]
    [SerializeField] private string id;

    private static readonly List<SpawnPoint> active = new List<SpawnPoint>();

    public string Id => id;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    /// <summary>The enabled spawn point with this id, or null (with a warning if an id was given but not found).</summary>
    public static SpawnPoint Find(string spawnId)
    {
        if (string.IsNullOrEmpty(spawnId)) return null;
        foreach (SpawnPoint point in active)
            if (point.id == spawnId) return point;

        Debug.LogWarning($"No SpawnPoint with id '{spawnId}' in this scene; using the default spawn.");
        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
    }
}
