#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes the current numbers of every asset in the BalanceRegistry to Assets/StreamingAssets/Balance/&lt;asset name&gt;.json,
/// as a starting point for editing. Only numbers, bools and Vector2 are exported. Prefab references, enums and text stay
/// in the assets, because balance JSON is meant for tuning values, not for wiring the game.
/// Run it in edit mode, so the file holds the asset values and not values patched by an earlier JSON.
/// </summary>
public static class BalanceExporter
{
    private const string MenuPath = "Tools/Balance/Export baseline JSON from assets";

    [MenuItem(MenuPath)]
    private static void Export()
    {
        BalanceRegistry registry = Resources.Load<BalanceRegistry>("BalanceRegistry");
        if (registry == null)
        {
            Debug.LogError("Balance: create Assets/Resources/BalanceRegistry.asset first (Create > Babylon Ascension > Balance Registry).");
            return;
        }

        string folder = Path.Combine(Application.dataPath, "StreamingAssets", registry.folder);
        Directory.CreateDirectory(folder);

        int existing = 0;
        foreach (ScriptableObject asset in registry.assets)
            if (asset != null && File.Exists(Path.Combine(folder, asset.name + ".json"))) existing++;

        bool overwrite = existing == 0 || EditorUtility.DisplayDialog(
            "Export baseline JSON",
            $"{existing} file(s) already exist. Overwrite them with the current asset values?\n(Cancel keeps the existing files and only writes the missing ones.)",
            "Overwrite", "Keep existing");

        int written = 0;
        foreach (ScriptableObject asset in registry.assets)
        {
            if (asset == null) continue;
            string path = Path.Combine(folder, asset.name + ".json");
            if (File.Exists(path) && !overwrite) continue;
            File.WriteAllText(path, BuildJson(asset), new UTF8Encoding(false));
            written++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"Balance: wrote {written} file(s) to {folder}");
    }

    [MenuItem(MenuPath, true)]
    private static bool ExportValidate()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static string BuildJson(ScriptableObject asset)
    {
        SerializedObject serialized = new SerializedObject(asset);
        SerializedProperty property = serialized.GetIterator();
        StringBuilder sb = new StringBuilder("{\n");
        bool first = true;

        // enterChildren only on the first call, so only the top-level fields are visited.
        for (bool enter = true; property.NextVisible(enter); enter = false)
        {
            string value = FormatValue(property);
            if (value == null) continue;
            if (!first) sb.Append(",\n");
            sb.Append("  \"").Append(property.name).Append("\": ").Append(value);
            first = false;
        }

        return sb.Append("\n}\n").ToString();
    }

    private static string FormatValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Float:
                return property.floatValue.ToString("R", CultureInfo.InvariantCulture);
            case SerializedPropertyType.Vector2:
                Vector2 v = property.vector2Value;
                return string.Format(CultureInfo.InvariantCulture, "{{\"x\": {0}, \"y\": {1}}}", v.x.ToString("R", CultureInfo.InvariantCulture), v.y.ToString("R", CultureInfo.InvariantCulture));
            default:
                return null;
        }
    }
}
#endif
