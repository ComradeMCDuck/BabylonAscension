using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves this transform into bent space while a camera renders and puts it back afterwards.
/// Outside rendering (gameplay, physics, editor) it stays at its flat position.
///
/// Use it for point/spot lights, reflection probes and objects rendered with shaders that
/// do not bend (rigid approximation). Do NOT use it on objects whose shaders already bend,
/// or on objects with colliders/rigidbodies.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class TowerBendTransform : MonoBehaviour
{
    static readonly List<TowerBendTransform> s_Instances = new List<TowerBendTransform>();

    [Tooltip("Also rotate with the tower surface (spot lights, probes facing the wall).")]
    public bool applyRotation = true;

    Vector3 m_FlatPosition;
    Quaternion m_FlatRotation;
    bool m_IsBent;

    void OnEnable() => s_Instances.Add(this);

    void OnDisable()
    {
        Restore();
        s_Instances.Remove(this);
    }

    internal static void BendAll(TowerBendParams p)
    {
        for (int i = 0; i < s_Instances.Count; i++)
            s_Instances[i].Bend(p);
    }

    internal static void RestoreAll()
    {
        for (int i = 0; i < s_Instances.Count; i++)
            s_Instances[i].Restore();
    }

    void Bend(TowerBendParams p)
    {
        if (m_IsBent) return;
        var t = transform;
        t.GetPositionAndRotation(out m_FlatPosition, out m_FlatRotation);
        Quaternion rotation = applyRotation ? p.BendRotation(m_FlatPosition) * m_FlatRotation : m_FlatRotation;
        t.SetPositionAndRotation(p.Bend(m_FlatPosition), rotation);
        m_IsBent = true;
    }

    void Restore()
    {
        if (!m_IsBent) return;
        transform.SetPositionAndRotation(m_FlatPosition, m_FlatRotation);
        m_IsBent = false;
    }
}
