using UnityEngine;

/// <summary>
/// Sends the player to another scene. It doesn't detect the player itself: put a Trigger2D (GameDevForBeginners
/// package, Filter Tags = Player) on the same object and wire its On Trigger Enter to <see cref="Load"/>.
/// Keeping the destination here instead of in the event lets exits be prefabs, since a prefab can't reference
/// the scene's GameManager. Works for both directions: forward to the next level and back to the previous one.
/// </summary>
public class LevelExit : MonoBehaviour
{
    [Tooltip("Name of the scene to load, exactly as in File > Build Profiles > Scene List (e.g. \"Viki LEVEL 7\").")]
    [SerializeField] private string targetScene;
    [Tooltip("Id of the SpawnPoint in the target scene where the player appears. Empty = that scene's default spawn (GameManager's Respawn Point).")]
    [SerializeField] private string targetSpawnId;

    /// <summary>For Trigger2D's On Trigger Enter (Collider2D) event. Pick the "Dynamic Collider2D" version.</summary>
    public void Load(Collider2D _) => Load();

    /// <summary>Starts the scene change. Extra calls while it is already running are ignored.</summary>
    public void Load()
    {
        if (SceneLoader.Instance == null)
        {
            Debug.LogError($"{name}: no SceneLoader in the scene (it belongs on the GameManager prefab).", this);
            return;
        }
        SceneLoader.Instance.TryLoadScene(targetScene, targetSpawnId);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(targetScene)) return;
        foreach (UnityEditor.EditorBuildSettingsScene scene in UnityEditor.EditorBuildSettings.scenes)
            if (scene.enabled && System.IO.Path.GetFileNameWithoutExtension(scene.path) == targetScene) return;
        Debug.LogWarning($"{name}: LevelExit target '{targetScene}' is not an enabled scene in the Build Profiles scene list.", this);
    }
#endif
}
