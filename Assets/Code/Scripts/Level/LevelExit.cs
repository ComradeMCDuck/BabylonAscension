using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A door to another scene. It needs a trigger Collider2D on the same object, on a layer that touches the
/// player's (e.g. "Triggers", like the altar). While the player stands in it, the HUD's <see cref="InteractPrompt"/>
/// is shown and E starts the scene change; with Require Interact off, touching it is enough.
/// Keeping the destination here lets exits be prefabs, since a prefab can't reference the scene's GameManager.
/// Works for both directions: forward to the next level and back to the previous one.
/// Uses the E key directly, like ShopMenu, because the "Interact" action has a Hold interaction.
/// </summary>
public class LevelExit : MonoBehaviour
{
    [Tooltip("Name of the scene to load, exactly as in File > Build Profiles > Scene List (e.g. \"Viki LEVEL 7\").")]
    [SerializeField] private string targetScene;
    [Tooltip("Id of the SpawnPoint in the target scene where the player appears. Empty = that scene's default spawn (GameManager's Respawn Point).")]
    [SerializeField] private string targetSpawnId;

    [Header("Interaction")]
    [Tooltip("The player must press E while standing in the trigger. Off = the scene changes as soon as the player touches it.")]
    [SerializeField] private bool requireInteract = true;
    [Tooltip("Text for the HUD prompt while the player is in range. Only used if the InteractPrompt has a Label assigned. Empty = the prompt keeps its own text.")]
    [SerializeField] private string promptText = "Press E to enter";

    // The player can have several colliders, so count them instead of using a bool.
    private int playerCollidersInRange;

    public bool PlayerInRange => playerCollidersInRange > 0;

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger == null || !trigger.isTrigger)
            Debug.LogError($"{name}: LevelExit needs a trigger Collider2D on the same object, or the player can't use it.", this);
    }

    private void Update()
    {
        if (!requireInteract || !PlayerInRange) return;
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        // Frozen time means the pause menu, the shop window or a scene change is in charge.
        if (Time.timeScale == 0f) return;

        Load();
    }

    /// <summary>Kept for old Trigger2D events. They skip the E press, so remove them from exits that should wait for it.</summary>
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        playerCollidersInRange++;
        if (playerCollidersInRange != 1) return;

        if (requireInteract) InteractPrompt.Show(this, promptText);
        else Load();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other) || playerCollidersInRange == 0) return;
        playerCollidersInRange--;
        if (playerCollidersInRange == 0) InteractPrompt.Hide(this);
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other.CompareTag("Player")
            || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"));
    }

    private void OnDisable()
    {
        playerCollidersInRange = 0;
        InteractPrompt.Hide(this);
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
