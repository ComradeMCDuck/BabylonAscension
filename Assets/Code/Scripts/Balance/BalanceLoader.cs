using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Overrides the numbers of data ScriptableObjects (EntityData, ArrowData, BossData, ...) from JSON files at runtime,
/// so balancing can change without recompiling the game. Created automatically before the first scene loads.
///
/// How it works:
/// - One file per asset: StreamingAssets/Balance/&lt;asset name&gt;.json, e.g. PlayerData.json.
/// - A file only needs the fields it changes: {"maxHealth": 120, "moveSpeed": 4.5}. Missing fields keep the asset's value.
/// - Saving a file while the game runs applies it within a second (hot reload).
/// - Standalone builds read the same folder next to the exe (&lt;Game&gt;_Data/StreamingAssets/Balance), so it can be edited after the build.
/// - In the Editor the assets are restored when play mode ends, so the JSON never leaks into the committed .asset files.
///   (That also reverts DebugMenu's edits to PlayerData once a BalanceRegistry exists.)
///
/// Known limits, on purpose (simple beats feature-rich):
/// - [Min]/[Range] attributes are not enforced for JSON values; a typo in a value is applied as written.
/// - Only numbers, bools and Vector2 are exported; enums, strings and prefab references stay in the assets.
/// - A deleted JSON file keeps the last applied values until the game restarts.
/// - Values that a component copies once (e.g. Health copies maxHealth into currentHealth in Awake) only change for new objects.
/// - WebGL is not supported (StreamingAssets can't be read with System.IO in the browser; it would need UnityWebRequest in a boot scene).
/// </summary>
public class BalanceLoader : MonoBehaviour
{
    private const string RegistryResourceName = "BalanceRegistry";
    private const float PollInterval = 1f;

    /// <summary>
    /// Short hash of the JSON files currently applied ("default" when none). Whitespace is ignored, so it is the same on every OS.
    /// Attach it to analytics events to compare balance variants.
    /// </summary>
    public static string Version { get; private set; } = "default";

    /// <summary>Raised after one or more files were applied.</summary>
    public static event Action Reloaded;

    private class Entry
    {
        public ScriptableObject asset;
        public string path;
        public DateTime lastWrite;
        public string appliedText;
    }

    private static readonly Regex KeyPattern = new Regex("\"([A-Za-z_][A-Za-z0-9_]*)\"\\s*:", RegexOptions.Compiled);

    private readonly List<Entry> entries = new List<Entry>();
    private BalanceRegistry registry;
    private float nextPoll;

#if UNITY_EDITOR
    // Copies of the assets as they were before the first override, put back when play mode ends.
    private readonly Dictionary<ScriptableObject, ScriptableObject> backups = new Dictionary<ScriptableObject, ScriptableObject>();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        // With "Enter Play Mode > Reload Domain" off the statics survive between plays; start clean.
        Version = "default";
        Reloaded = null;

        BalanceRegistry loaded = Resources.Load<BalanceRegistry>(RegistryResourceName);
        if (loaded == null)
        {
            Debug.Log("Balance: no Resources/BalanceRegistry asset, JSON overrides are off.");
            return;
        }

        GameObject host = new GameObject("BalanceLoader") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(host);
        host.AddComponent<BalanceLoader>().Init(loaded);
#endif
    }

    private void Init(BalanceRegistry source)
    {
        registry = source;
        HashSet<string> names = new HashSet<string>();
        foreach (ScriptableObject asset in registry.assets)
        {
            if (asset == null) continue;
            if (!names.Add(asset.name))
            {
                Debug.LogWarning($"Balance: two assets are called '{asset.name}'. Only the first one is used.", asset);
                continue;
            }
            entries.Add(new Entry
            {
                asset = asset,
                path = Path.Combine(Application.streamingAssetsPath, registry.folder, asset.name + ".json"),
            });
#if UNITY_EDITOR
            ScriptableObject backup = Instantiate(asset);
            backup.hideFlags = HideFlags.HideAndDontSave;
            backups[asset] = backup;
#endif
        }

        Application.quitting += OnQuitting;
        ReloadChanged(true);
    }

    private void Update()
    {
        if (registry == null || !registry.hotReload || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + PollInterval;
        ReloadChanged(false);
    }

    /// <summary>Applies every file that is new or has a newer write time than the last time it was read.</summary>
    private void ReloadChanged(bool force)
    {
        int applied = 0;
        foreach (Entry entry in entries)
        {
            if (!File.Exists(entry.path)) continue; // A deleted file keeps the last values until the game restarts.

            DateTime stamp = File.GetLastWriteTimeUtc(entry.path);
            if (!force && stamp == entry.lastWrite) continue;

            string text;
            try
            {
                text = File.ReadAllText(entry.path);
            }
            catch (IOException)
            {
                continue; // The editor is still writing the file. entry.lastWrite is unchanged, so it is retried next poll.
            }
            entry.lastWrite = stamp;
            if (text == entry.appliedText) continue;

            try
            {
                WarnAboutUnknownKeys(entry, text);
                JsonUtility.FromJsonOverwrite(text, entry.asset);
                entry.appliedText = text;
                if (entry.asset is EntityData entityData) entityData.NotifyChanged();
                applied++;
            }
            catch (ArgumentException ex)
            {
                Debug.LogWarning($"Balance: {Path.GetFileName(entry.path)} is not valid JSON, previous values kept. {ex.Message}", entry.asset);
            }
        }

        if (applied == 0) return;
        Version = ComputeVersion();
        Debug.Log($"Balance: applied {applied} file(s), version {Version}.");
        Reloaded?.Invoke();
    }

    /// <summary>
    /// JsonUtility silently ignores unknown fields, so a typo in a file would do nothing. This lists them in the console.
    /// A name is "known" if it appears as a key in the asset's own JSON.
    /// </summary>
    private static void WarnAboutUnknownKeys(Entry entry, string json)
    {
        string known = JsonUtility.ToJson(entry.asset);
        foreach (Match match in KeyPattern.Matches(json))
        {
            string key = match.Groups[1].Value;
            if (!known.Contains("\"" + key + "\":"))
                Debug.LogWarning($"Balance: '{key}' in {Path.GetFileName(entry.path)} is not a field of {entry.asset.GetType().Name} and is ignored.", entry.asset);
        }
    }

    /// <summary>FNV-1a over asset name and text of every applied file, ignoring whitespace.</summary>
    private string ComputeVersion()
    {
        bool any = false;
        uint hash = 2166136261;
        unchecked
        {
            foreach (Entry entry in entries)
            {
                if (entry.appliedText == null) continue;
                any = true;
                foreach (char c in entry.asset.name + entry.appliedText)
                {
                    if (char.IsWhiteSpace(c)) continue;
                    hash ^= c;
                    hash *= 16777619;
                }
            }
        }
        return any ? hash.ToString("x8") : "default";
    }

    private void OnQuitting()
    {
        Application.quitting -= OnQuitting;
#if UNITY_EDITOR
        // Runtime changes to ScriptableObjects would otherwise stay in the asset after play mode.
        // "IfDifferent" so untouched assets are not marked dirty and rewritten on the next save.
        foreach (KeyValuePair<ScriptableObject, ScriptableObject> pair in backups)
        {
            if (pair.Value == null) continue;
            if (pair.Key != null) UnityEditor.EditorUtility.CopySerializedIfDifferent(pair.Value, pair.Key);
            DestroyImmediate(pair.Value);
        }
        backups.Clear();
#endif
    }
}
