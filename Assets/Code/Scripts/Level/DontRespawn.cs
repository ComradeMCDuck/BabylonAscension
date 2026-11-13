using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put this on an enemy, a puzzle piece, a door or a pickup that should stay gone once the player has dealt with
/// it. Scene loads (death, level exits) rebuild the level from the scene file, so without this everything comes back.
/// When the object is destroyed or switched off during play it is noted in <see cref="LevelMemory"/>. The note
/// becomes permanent when the player reaches a later level in the Build Profiles scene list; dying or going back
/// to an earlier level drops it, so everything is back. Once permanent, the object switches itself off on every later load of the scene, before
/// any of its other scripts wake up.
/// For things that don't disappear (a chest that stays open), call <see cref="Remember"/> from an event, turn
/// Remove On Load off and use On Loaded Remembered to put the object in its finished state.
/// </summary>
// Before everything else, so a remembered object is switched off before its other components run Awake.
[DefaultExecutionOrder(-3000)]
[DisallowMultipleComponent]
public class DontRespawn : MonoBehaviour
{
    [Tooltip("Remember this object by itself when it is destroyed or deactivated during play (an enemy dying, a lamp switched off by an ArrowTarget). Off = only when something calls Remember(), e.g. from a UnityEvent.")]
    [SerializeField] private bool rememberWhenRemoved = true;
    [Tooltip("When the level loads and this object is remembered, deactivate it. Turn off to keep it and only run On Loaded Remembered.")]
    [SerializeField] private bool removeOnLoad = true;
    [Tooltip("Called when the level loads and this object is remembered, e.g. to open the door this puzzle opened. Runs before any other script's Awake, so keep it to simple things like GameObject.SetActive.")]
    [SerializeField] private UnityEvent onLoadedRemembered = new UnityEvent();

    [Space]
    [Tooltip("Identifies this object between scene loads. Filled in automatically and unique per scene; don't edit it.")]
    [SerializeField] private string id;

    private string key;

    /// <summary>True once the removal is permanent (the player left the level after it), so it won't come back.</summary>
    public bool IsRemembered => HasId && LevelMemory.Contains(Key);

    private bool HasId => !string.IsNullOrEmpty(id);
    // The scene path is part of the key, because a copied scene file carries the same ids.
    private string Key => key ??= gameObject.scene.path + ":" + id;

    private void Awake()
    {
        if (!HasId)
        {
            Debug.LogWarning($"{name}: DontRespawn has no id, so it will respawn. Select it in the scene (not in the prefab) and save the scene.", this);
            return;
        }

        if (!LevelMemory.Contains(Key)) return;

        onLoadedRemembered.Invoke();
        // Deactivated instead of destroyed: it takes effect at once, and other objects' references to it stay valid.
        if (removeOnLoad) gameObject.SetActive(false);
    }

    /// <summary>
    /// Marks this object as done. It is removed on later loads once the player reaches a later level.
    /// For UnityEvents and code.
    /// </summary>
    public void Remember()
    {
        if (HasId) LevelMemory.Remember(Key);
    }

    /// <summary>Undoes <see cref="Remember"/>, so the object is back on the next load.</summary>
    public void Forget()
    {
        if (HasId) LevelMemory.Forget(Key);
    }

    private void OnDisable()
    {
        // Only a deactivated GameObject counts, not this component being disabled.
        if (rememberWhenRemoved && !gameObject.activeInHierarchy && !IsSceneClosing()) Remember();
    }

    private void OnDestroy()
    {
        if (rememberWhenRemoved && !IsSceneClosing()) Remember();
    }

    /// <summary>
    /// A scene change destroys and disables everything too, and that must not count as the player removing it.
    /// </summary>
    private bool IsSceneClosing() => SceneLoader.IsTransitioning || !gameObject.scene.isLoaded;

#if UNITY_EDITOR
    /// <summary>
    /// Gives every placed object its own id. Duplicating an object copies its id, so a copy gets a new one.
    /// Prefab assets keep an empty id; each instance in a scene stores its own as an override.
    /// </summary>
    private void OnValidate()
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!gameObject.scene.IsValid()
            || UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)
            || UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(this))
            return;

        if (HasId && !IdIsUsedByAnother()) return;

        id = Guid.NewGuid().ToString("N");
        UnityEditor.EditorUtility.SetDirty(this);
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    private bool IdIsUsedByAnother()
    {
        if (!gameObject.scene.isLoaded) return false;

        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (DontRespawn other in root.GetComponentsInChildren<DontRespawn>(true))
                if (other != this && other.id == id) return true;

        return false;
    }
#endif
}
