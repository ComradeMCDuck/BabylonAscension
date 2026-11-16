using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural neck, "pulled out of a tube": the neck joint sprites sit along a curve from a point on the body
/// (Neck Base) to the head, at a fixed spacing measured back from the head. The head is moved by something else
/// (BiteAttack moves its hitbox, with the head sprite as a child). When the head shoots out, more joints slide out of
/// the body; when it pulls back, they slide back in. Joints that don't fit on the neck wait at the base, hidden.
/// In play mode, copies of the joint sprites are made as needed (cycling through them in order), so there are never
/// gaps however far the head reaches. Copies are only ever added, never destroyed, so it settles after the longest bite.
/// The curve is a cubic Bezier: it leaves the body along Base Direction and enters the head from behind (the side
/// opposite to where the boss faces), which gives the neck its S-shape. It is worked out in the joints' parent space
/// (the Visual Root, as if facing right), so it mirrors correctly when the boss turns around.
/// Runs in edit mode too, placing just the original joints, so moving the head in the prefab shows the neck.
/// </summary>
[ExecuteAlways]
public class BossNeck : MonoBehaviour
{
    // Points sampled along the curve to place the joints by length (a Bezier's own parameter bunches up at the bends).
    private const int CurveSamples = 24;
    // Safety cap, so a broken setup (a huge curve or a tiny spacing) can't keep creating joints.
    private const int MaxJoints = 64;

    [Tooltip("Where the neck leaves the body. Put it under the Visual Root (Appearance), so it turns with the body.")]
    [SerializeField] private Transform neckBase;
    [Tooltip("The head the neck follows, e.g. the BiteHead hitbox (moved by BiteAttack).")]
    [SerializeField] private Transform head;
    [Tooltip("Where the neck joins the head, relative to the head object (its local space, as if facing right). Usually a bit behind and below the head's center.")]
    [SerializeField] private Vector2 headAttachOffset = new Vector2(-0.6f, -0.2f);
    [Tooltip("Neck joint sprites in order from the body to the head. They must all share the same parent (e.g. Appearance). In play mode they are copied as needed, repeating this pattern back from the head.")]
    [SerializeField] private Transform[] joints;
    [Tooltip("Optional: the only joint that gets copied for the extra length (e.g. a plain middle segment). The original joints keep their order at the head end, and the copies follow behind them toward the body. Empty = repeat the whole joint pattern.")]
    [SerializeField] private Transform repeatJoint;

    [Header("Shape")]
    [Tooltip("Direction the neck leaves the body in, as if the boss faces right (e.g. (0.3, 1) = up and a little forward).")]
    [SerializeField] private Vector2 baseDirection = new Vector2(0.3f, 1f);
    [Tooltip("How strongly the neck keeps Base Direction before bending toward the head, as a fraction of the neck's length. 0 = straight from the body.")]
    [SerializeField, Range(0f, 1f)] private float baseBend = 0.4f;
    [Tooltip("How strongly the neck comes into the head from behind, as a fraction of the neck's length. 0 = straight into the head.")]
    [SerializeField, Range(0f, 1f)] private float headBend = 0.3f;

    [Header("Joints")]
    [Tooltip("Distance between neighbouring joints along the neck, in world units (at the Visual Root's scale). Smaller = more joints, more overlap.")]
    [SerializeField, Min(0.05f)] private float jointSpacing = 1f;
    [Tooltip("Distance from the head's attach point to the first joint, along the neck.")]
    [SerializeField, Min(0f)] private float headGap = 0f;
    [Tooltip("Hide the joints that don't fit on the neck (they wait at the base, inside the body). Play mode only. Turn off if the body sprite already covers them.")]
    [SerializeField] private bool hideTuckedJoints = true;
    [Tooltip("Turn each joint to follow the curve.")]
    [SerializeField] private bool rotateJoints = true;
    [Tooltip("Degrees added to each joint's rotation. 0 = the sprite's right side (+X) points along the neck toward the head. Change it if the joint sprites are drawn in a different direction.")]
    [SerializeField] private float rotationOffset = 0f;

    private readonly Vector2[] samplePoints = new Vector2[CurveSamples + 1];
    private readonly float[] sampleLengths = new float[CurveSamples + 1];

    // Play mode only: the joints ordered from the head back to the body (originals first, then copies).
    private readonly List<Transform> chain = new List<Transform>();
    private readonly List<Renderer[]> chainRenderers = new List<Renderer[]>();
    private readonly List<Transform> templates = new List<Transform>();
    private bool loggedCap;

    private void Awake()
    {
        if (!Application.isPlaying) return;

        if (neckBase == null || head == null || joints == null)
        {
            Debug.LogError($"{name}: BossNeck needs a Neck Base, a Head and at least one joint assigned. Disabling.", this);
            enabled = false;
            return;
        }

        Transform space = neckBase.parent;
        foreach (Transform joint in joints)
        {
            if (joint == null) continue;
            templates.Add(joint);
            if (joint.parent != space)
                Debug.LogWarning($"{name}: Neck joint {joint.name} should have the same parent as the Neck Base ({(space != null ? space.name : "none")}), or it will be placed wrongly.", this);
        }

        if (templates.Count == 0)
        {
            Debug.LogError($"{name}: BossNeck needs at least one joint assigned. Disabling.", this);
            enabled = false;
            return;
        }

        // The originals start the chain, the one nearest the head first.
        for (int i = templates.Count - 1; i >= 0; i--)
            AddToChain(templates[i]);

        // A repeat joint that isn't one of the joints is only a source for copies, so it mustn't show on its own.
        if (repeatJoint != null && !templates.Contains(repeatJoint))
            repeatJoint.gameObject.SetActive(false);
    }

    // LateUpdate: after BiteAttack (FixedUpdate) and any animation have placed the head this frame.
    private void LateUpdate()
    {
        if (neckBase == null || head == null || joints == null || joints.Length == 0) return;

        // Work in the joints' parent space: there the boss always faces right, whichever way it really faces.
        Transform space = neckBase.parent;
        Vector2 start = ToSpace(space, neckBase.position);
        Vector2 end = ToSpace(space, head.TransformPoint(headAttachOffset));

        float span = Vector2.Distance(start, end);
        if (span < 0.0001f) return;

        float length = SampleCurve(start, GetStartControl(start, span), GetEndControl(end, span), end);
        // Spacing is given in world units; the curve is in the Visual Root's local units.
        float spacing = jointSpacing / GetSpaceScale(space);
        float gap = headGap / GetSpaceScale(space);

        int count;
        if (Application.isPlaying)
        {
            // Every joint whose spot lies on the neck must exist; the rest wait at the base.
            EnsureChain(Mathf.FloorToInt(Mathf.Max(0f, length - gap) / spacing) + 1);
            count = chain.Count;
        }
        else
        {
            count = joints.Length;
        }

        for (int i = 0; i < count; i++)
        {
            Transform joint = GetJoint(i);
            if (joint == null) continue;

            float fromHead = gap + i * spacing;
            bool tucked = fromHead > length;

            GetPointAtLength(Mathf.Max(0f, length - fromHead), out Vector2 point, out Vector2 tangent);
            joint.localPosition = new Vector3(point.x, point.y, joint.localPosition.z);

            if (rotateJoints && tangent.sqrMagnitude > 0.000001f)
                joint.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg + rotationOffset);

            // Only in play mode: in edit mode it would save hidden renderers into the prefab.
            if (Application.isPlaying && hideTuckedJoints) SetVisible(i, !tucked);
        }
    }

    /// <summary>Play mode: the chain, head first. Edit mode: the original joints, head first.</summary>
    private Transform GetJoint(int indexFromHead) =>
        Application.isPlaying ? chain[indexFromHead] : joints[joints.Length - 1 - indexFromHead];

    /// <summary>Copies joints until the chain has at least count of them, repeating the original pattern back from the head.</summary>
    private void EnsureChain(int count)
    {
        if (count > MaxJoints)
        {
            if (!loggedCap)
            {
                Debug.LogWarning($"{name}: The neck needs more than {MaxJoints} joints. Raise Joint Spacing or check the Head and Neck Base.", this);
                loggedCap = true;
            }
            count = MaxJoints;
        }

        while (chain.Count < count)
        {
            // Either the one repeat joint, or the whole pattern: chain[i] uses the template i steps back from the
            // head-most one, wrapping around.
            Transform template = repeatJoint != null
                ? repeatJoint
                : templates[templates.Count - 1 - chain.Count % templates.Count];
            Transform copy = Instantiate(template, neckBase.parent);
            copy.gameObject.SetActive(true);
            copy.name = $"{template.name} ({chain.Count})";
            AddToChain(copy);
        }
    }

    private void AddToChain(Transform joint)
    {
        chain.Add(joint);
        chainRenderers.Add(joint.GetComponentsInChildren<Renderer>(true));
    }

    private void SetVisible(int index, bool visible)
    {
        foreach (Renderer r in chainRenderers[index])
            if (r != null && r.enabled != visible) r.enabled = visible;
    }

    private Vector2 GetStartControl(Vector2 start, float span) => start + baseDirection.normalized * (baseBend * span);

    // Enter the head from behind it, i.e. from the left in this facing-right space.
    private Vector2 GetEndControl(Vector2 end, float span) => end + Vector2.left * (headBend * span);

    /// <summary>Samples the curve and stores the running length at each sample. Returns the total length.</summary>
    private float SampleCurve(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        samplePoints[0] = p0;
        sampleLengths[0] = 0f;

        for (int i = 1; i <= CurveSamples; i++)
        {
            float t = i / (float)CurveSamples;
            float u = 1f - t;
            samplePoints[i] = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
            sampleLengths[i] = sampleLengths[i - 1] + Vector2.Distance(samplePoints[i - 1], samplePoints[i]);
        }

        return sampleLengths[CurveSamples];
    }

    /// <summary>Point and direction at a distance along the sampled curve.</summary>
    private void GetPointAtLength(float distance, out Vector2 point, out Vector2 tangent)
    {
        for (int i = 1; i <= CurveSamples; i++)
        {
            if (sampleLengths[i] < distance && i < CurveSamples) continue;

            float segment = sampleLengths[i] - sampleLengths[i - 1];
            float t = segment > 0.000001f ? Mathf.Clamp01((distance - sampleLengths[i - 1]) / segment) : 0f;
            point = Vector2.Lerp(samplePoints[i - 1], samplePoints[i], t);
            tangent = samplePoints[i] - samplePoints[i - 1];
            return;
        }

        point = samplePoints[CurveSamples];
        tangent = samplePoints[CurveSamples] - samplePoints[CurveSamples - 1];
    }

    /// <summary>World units per local unit of the joints' parent (its X scale, ignoring the facing flip).</summary>
    private static float GetSpaceScale(Transform space) =>
        space != null ? Mathf.Max(Mathf.Abs(space.lossyScale.x), 0.0001f) : 1f;

    private static Vector2 ToSpace(Transform space, Vector3 worldPoint) =>
        space != null ? (Vector2)space.InverseTransformPoint(worldPoint) : (Vector2)worldPoint;

    private static Vector3 ToWorld(Transform space, Vector2 localPoint) =>
        space != null ? space.TransformPoint(localPoint) : (Vector3)localPoint;

    private void OnDrawGizmosSelected()
    {
        if (neckBase == null || head == null) return;

        Transform space = neckBase.parent;
        Vector2 start = ToSpace(space, neckBase.position);
        Vector2 end = ToSpace(space, head.TransformPoint(headAttachOffset));
        float span = Vector2.Distance(start, end);

        float length = SampleCurve(start, GetStartControl(start, span), GetEndControl(end, span), end);

        Gizmos.color = new Color(0.2f, 1f, 0.4f);
        for (int i = 1; i <= CurveSamples; i++)
            Gizmos.DrawLine(ToWorld(space, samplePoints[i - 1]), ToWorld(space, samplePoints[i]));
        Gizmos.DrawWireSphere(ToWorld(space, start), 0.12f);

        // Where each joint goes at the current neck length.
        float spacing = jointSpacing / GetSpaceScale(space);
        float gap = headGap / GetSpaceScale(space);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
        for (float d = gap; d <= length && d < gap + MaxJoints * spacing; d += spacing)
        {
            GetPointAtLength(length - d, out Vector2 point, out _);
            Gizmos.DrawWireSphere(ToWorld(space, point), 0.06f);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ToWorld(space, end), 0.12f);
    }
}
