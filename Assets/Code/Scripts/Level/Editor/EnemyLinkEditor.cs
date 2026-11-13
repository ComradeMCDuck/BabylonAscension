using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene view handles for EnemyLink: drag the Start and End points directly. They are stored relative to the link's
/// object, so moving the object moves the whole link. The arc and the snapped points are drawn by EnemyLink's gizmos.
/// </summary>
[CustomEditor(typeof(EnemyLink))]
public class EnemyLinkEditor : Editor
{
    private void OnSceneGUI()
    {
        var link = (EnemyLink)target;
        serializedObject.Update();

        DrawPointHandle(link, serializedObject.FindProperty("start"), "Start");
        DrawPointHandle(link, serializedObject.FindProperty("end"), "End");

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawPointHandle(EnemyLink link, SerializedProperty point, string label)
    {
        Transform transform = link.transform;
        Vector3 world = transform.TransformPoint(point.vector2Value);

        Handles.Label(world + Vector3.up * 0.4f, label);

        EditorGUI.BeginChangeCheck();
        float size = HandleUtility.GetHandleSize(world) * 0.12f;
        Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            moved.z = world.z;
            point.vector2Value = transform.InverseTransformPoint(moved);
        }
    }
}
