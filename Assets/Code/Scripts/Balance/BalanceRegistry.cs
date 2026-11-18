using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// List of every ScriptableObject whose numbers can be overridden from JSON while the game runs (see BalanceLoader).
/// The asset must live in a Resources folder (Assets/Resources/BalanceRegistry.asset): that way it is loaded before
/// the first scene, and it keeps the data assets in memory so the scenes use the same, already patched instances.
/// </summary>
[CreateAssetMenu(fileName = "BalanceRegistry", menuName = "Babylon Ascension/Balance Registry")]
public class BalanceRegistry : ScriptableObject
{
    [Tooltip("Every data asset that balance JSON files may override. Use the context menu 'Collect all balance assets' to fill the list.")]
    public List<ScriptableObject> assets = new List<ScriptableObject>();

    [Tooltip("Folder with the JSON files, relative to StreamingAssets. One file per asset, named <asset name>.json")]
    public string folder = "Balance";

    [Tooltip("Reload a JSON file while the game runs as soon as it is saved (checked once per second).")]
    public bool hotReload = true;

#if UNITY_EDITOR
    /// <summary>
    /// Adds every EntityData, ArrowData, BossData, BossAttackData (any derived attack data) and BowUpgrade asset in
    /// Assets/. The type check is done in C# instead of "t:" filters, so derived classes are never missed.
    /// </summary>
    [ContextMenu("Collect all balance assets")]
    private void CollectAll()
    {
        assets.Clear();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (IsBalanceAsset(asset) && !assets.Contains(asset)) assets.Add(asset);
        }
        // Stable order, so the .asset diff only changes when assets are added or removed.
        assets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"BalanceRegistry: collected {assets.Count} assets.", this);
    }

    private static bool IsBalanceAsset(ScriptableObject asset)
    {
        return asset is EntityData || asset is ArrowData || asset is BossData || asset is BossAttackData || asset is BowUpgrade;
    }
#endif
}
