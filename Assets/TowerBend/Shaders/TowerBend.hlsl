#ifndef TOWER_BEND_INCLUDED
#define TOWER_BEND_INCLUDED

// -----------------------------------------------------------------------------
// Tower bend: wraps the flat level (built on the XY plane, camera looking +Z)
// around a vertical cylinder. Physics, colliders and level authoring stay flat;
// only rendering is bent.
//
//   flat X  -> angle around the tower   a = (x - originX) / R
//   flat Z  -> radius                   r = R - (z - surfaceZ)
//   flat Y  -> unchanged
//
// All parameters are GLOBAL shader properties driven by TowerBendController.cs.
// Being global (not per-material) keeps the SRP Batcher compatible and guarantees
// that every pass (forward, shadow, depth, motion vectors) bends and tessellates
// identically, which avoids shadow acne and depth-prepass mismatches.
// -----------------------------------------------------------------------------

float4 _TowerBend;       // x: origin X (flat world)  y: radius  z: surface Z (gameplay plane)  w: 1 = bend enabled
float4 _TowerBendPrev;   // same as _TowerBend, previous frame (motion vectors)
float4 _TowerTess;       // x: target edge length (world units) at reference distance
                         // y: reference distance  z: max tess factor  w: max tess distance (0 = unlimited)
float4 _TowerTessCamera; // xyz: LOD camera position (bent space)  w: isotropy (0 = only flat X extent counts, 1 = full edge length)
float4 _TowerCull;       // x: cull angle in radians (0 = off)

float3 TowerBendPosition(float3 p, float4 bend)
{
    float R = max(bend.y, 1e-3);
    float a = (p.x - bend.x) / R;
    float r = R - (p.z - bend.z);
    float s, c;
    sincos(a, s, c);
    float3 bent = float3(bend.x + r * s, p.y, bend.z + R - r * c);
    return lerp(p, bent, bend.w);
}

// Tangent-like vectors: Jacobian of the bend = RotY(a) * diag(r/R, 1, 1).
float3 TowerBendDirection(float3 v, float3 flatPos, float4 bend)
{
    float R = max(bend.y, 1e-3);
    float a = (flatPos.x - bend.x) / R;
    float r = max(R - (flatPos.z - bend.z), 1e-3);
    float s, c;
    sincos(a, s, c);
    float3 t = float3(v.x * (r / R), v.y, v.z);
    float3 bent = float3(t.x * c - t.z * s, t.y, t.x * s + t.z * c);
    return lerp(v, bent, bend.w);
}

// Normals: inverse transpose of the Jacobian = RotY(a) * diag(R/r, 1, 1).
float3 TowerBendNormal(float3 n, float3 flatPos, float4 bend)
{
    float R = max(bend.y, 1e-3);
    float a = (flatPos.x - bend.x) / R;
    float r = max(R - (flatPos.z - bend.z), 1e-3);
    float s, c;
    sincos(a, s, c);
    float3 m = float3(n.x * (R / r), n.y, n.z);
    float3 bent = float3(m.x * c - m.z * s, m.y, m.x * s + m.z * c);
    return normalize(lerp(n, bent, bend.w));
}

// Tessellation factor for one edge, from FLAT world-space endpoints.
// Depends only on the two endpoints (order independent), so the two triangles
// sharing an edge always agree -> no cracks.
//
// The bend is linear in flat Y and flat Z; curvature exists only along flat X.
// With isotropy = 0 only the X extent of an edge is subdivided, which removes all
// triangles that would not change the silhouette (vertical edges stay at 1).
float TowerEdgeFactor(float3 a, float3 b)
{
    if (_TowerBend.w < 0.5)
        return 1.0;

    float3 d = abs(a - b);
    float w = _TowerTessCamera.w;
    float len = length(float3(d.x, d.y * w, d.z * w));

    float3 midBent = TowerBendPosition(0.5 * (a + b), _TowerBend);
    float dist = distance(midBent, _TowerTessCamera.xyz);

    // Uniform world-space density up to the reference distance, then the target
    // edge length grows linearly with distance -> constant density on screen.
    float target = max(_TowerTess.x, 1e-4) * max(1.0, dist / max(_TowerTess.y, 1e-4));
    float f = len / target;

    // Fade to no tessellation near the max distance.
    if (_TowerTess.w > 0.0)
        f = lerp(f, 1.0, smoothstep(0.75 * _TowerTess.w, _TowerTess.w, dist));

    return clamp(f, 1.0, max(_TowerTess.z, 1.0));
}

// Drop patches that lie entirely on the far side of the tower.
bool TowerPatchCulled(float3 p0, float3 p1, float3 p2)
{
    float limit = _TowerCull.x;
    if (limit <= 0.0 || _TowerBend.w < 0.5)
        return false;

    float invR = 1.0 / max(_TowerBend.y, 1e-3);
    float a0 = (p0.x - _TowerBend.x) * invR;
    float a1 = (p1.x - _TowerBend.x) * invR;
    float a2 = (p2.x - _TowerBend.x) * invR;
    return min(a0, min(a1, a2)) > limit || max(a0, max(a1, a2)) < -limit;
}

#endif // TOWER_BEND_INCLUDED
