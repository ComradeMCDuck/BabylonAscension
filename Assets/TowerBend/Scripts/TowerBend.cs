using UnityEngine;

/// <summary>
/// CPU mirror of TowerBend.hlsl. Use it to convert between flat (gameplay/physics) space
/// and bent (rendered) space, e.g. to place UI over a bent object or to pick with the mouse.
/// </summary>
public struct TowerBendParams
{
    public float originX;
    public float radius;
    public float surfaceZ;
    public bool enabled;

    public Vector4 ToShaderVector() => new Vector4(originX, Mathf.Max(radius, 1e-3f), surfaceZ, enabled ? 1f : 0f);

    /// <summary>Angle around the tower (radians) of a flat-space point.</summary>
    public float Angle(Vector3 flat) => (flat.x - originX) / Mathf.Max(radius, 1e-3f);

    public Vector3 Bend(Vector3 flat)
    {
        if (!enabled) return flat;
        float R = Mathf.Max(radius, 1e-3f);
        float a = (flat.x - originX) / R;
        float r = R - (flat.z - surfaceZ);
        return new Vector3(originX + r * Mathf.Sin(a), flat.y, surfaceZ + R - r * Mathf.Cos(a));
    }

    public Vector3 Unbend(Vector3 bent)
    {
        if (!enabled) return bent;
        float R = Mathf.Max(radius, 1e-3f);
        float dx = bent.x - originX;
        float dz = surfaceZ + R - bent.z;
        float a = Mathf.Atan2(dx, dz);
        float r = Mathf.Sqrt(dx * dx + dz * dz);
        return new Vector3(originX + a * R, bent.y, surfaceZ + R - r);
    }

    /// <summary>Rotation that bent space applies at a flat point (around world Y).</summary>
    public Quaternion BendRotation(Vector3 flat)
    {
        if (!enabled) return Quaternion.identity;
        return Quaternion.AngleAxis(-Angle(flat) * Mathf.Rad2Deg, Vector3.up);
    }
}
