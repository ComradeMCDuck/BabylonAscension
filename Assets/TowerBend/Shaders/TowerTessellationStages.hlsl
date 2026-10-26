#ifndef TOWER_TESSELLATION_STAGES_INCLUDED
#define TOWER_TESSELLATION_STAGES_INCLUDED

// Vertex -> Hull -> Domain stages. Requires TowerTessellation.hlsl,
// Attributes TowerToAttributes(TowerControlPoint) and TOWER_PASS_VERTEX.
//
//   #pragma vertex TowerTessVertex
//   #pragma hull   TowerTessHull
//   #pragma domain TowerTessDomain

#if !defined(TOWER_PASS_VERTEX)
    #error "Define TOWER_PASS_VERTEX before including TowerTessellationStages.hlsl"
#endif

TowerControlPoint TowerTessVertex(TowerVertexInput v)
{
    TowerControlPoint cp = (TowerControlPoint)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, cp);

#if defined(TOWER_HAS_PRE_VERTEX)
    TowerPreVertex(v);
#endif

    cp.positionOS = v.positionOS;
    cp.positionWS = TransformObjectToWorld(v.positionOS.xyz);
#if defined(TOWER_NEEDS_NORMAL)
    cp.normalOS = v.normalOS;
#endif
#if defined(TOWER_NEEDS_TANGENT)
    cp.tangentOS = v.tangentOS;
#endif
    cp.uv0 = v.uv0;
#if defined(TOWER_NEEDS_UV1)
    cp.uv1 = v.uv1;
#endif
#if defined(TOWER_NEEDS_UV2)
    cp.uv2 = v.uv2;
#endif
#if defined(TOWER_NEEDS_POSITION_OLD)
    cp.positionOld = v.positionOld;
#endif
#if defined(TOWER_NEEDS_COLOR)
    cp.color = v.color;
#endif
    return cp;
}

TowerTessFactors TowerTessPatchConstant(InputPatch<TowerControlPoint, 3> patch)
{
    TowerTessFactors f;
    float3 p0 = patch[0].positionWS;
    float3 p1 = patch[1].positionWS;
    float3 p2 = patch[2].positionWS;

    if (TowerPatchCulled(p0, p1, p2))
    {
        f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 0.0;
        return f;
    }

    // Edge i is opposite vertex i.
    f.edge[0] = TowerEdgeFactor(p1, p2);
    f.edge[1] = TowerEdgeFactor(p2, p0);
    f.edge[2] = TowerEdgeFactor(p0, p1);
    f.inside  = max(f.edge[0], max(f.edge[1], f.edge[2]));
    return f;
}

[domain("tri")]
[partitioning("fractional_odd")]     // smooth LOD transitions, no popping
[outputtopology("triangle_cw")]
[patchconstantfunc("TowerTessPatchConstant")]
[outputcontrolpoints(3)]
[maxtessfactor(64.0)]
TowerControlPoint TowerTessHull(InputPatch<TowerControlPoint, 3> patch, uint id : SV_OutputControlPointID)
{
    return patch[id];
}

// Bends an (interpolated or original) control point and runs the URP pass vertex
// function on it. Also usable as a plain vertex shader path without tessellation.
Varyings TowerFinishVertex(TowerControlPoint cp)
{
    UNITY_SETUP_INSTANCE_ID(cp);
    _TowerFlatPositionWS = cp.positionWS;
#if !defined(TOWER_PASS_BENDS_ITSELF)
    TowerBendControlPoint(cp);
#endif
    Attributes a = TowerToAttributes(cp);
    return TOWER_PASS_VERTEX(a);
}

#define TOWER_BARY(field) cp.field = patch[0].field * bary.x + patch[1].field * bary.y + patch[2].field * bary.z

[domain("tri")]
Varyings TowerTessDomain(TowerTessFactors factors, OutputPatch<TowerControlPoint, 3> patch, float3 bary : SV_DomainLocation)
{
    TowerControlPoint cp = (TowerControlPoint)0;
    UNITY_TRANSFER_INSTANCE_ID(patch[0], cp);

    TOWER_BARY(positionOS);
    TOWER_BARY(positionWS);
#if defined(TOWER_NEEDS_NORMAL)
    TOWER_BARY(normalOS);
#endif
#if defined(TOWER_NEEDS_TANGENT)
    TOWER_BARY(tangentOS);
#endif
    TOWER_BARY(uv0);
#if defined(TOWER_NEEDS_UV1)
    TOWER_BARY(uv1);
#endif
#if defined(TOWER_NEEDS_UV2)
    TOWER_BARY(uv2);
#endif
#if defined(TOWER_NEEDS_POSITION_OLD)
    TOWER_BARY(positionOld);
#endif
#if defined(TOWER_NEEDS_COLOR)
    TOWER_BARY(color);
#endif

    return TowerFinishVertex(cp);
}

#undef TOWER_BARY

// Non-tessellated path (same bend, original vertex density): #pragma vertex TowerBendVertex
Varyings TowerBendVertex(TowerVertexInput v)
{
    return TowerFinishVertex(TowerTessVertex(v));
}

#endif // TOWER_TESSELLATION_STAGES_INCLUDED
