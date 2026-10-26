#ifndef TOWER_TESSELLATION_INCLUDED
#define TOWER_TESSELLATION_INCLUDED

// -----------------------------------------------------------------------------
// Tessellation structs and helpers. Include AFTER the URP pass include, and after
// declaring which vertex attributes the pass needs:
//
//   #define TOWER_NEEDS_NORMAL          normalOS   (bent as a normal)
//   #define TOWER_NEEDS_TANGENT         tangentOS  (bent as a direction)
//   #define TOWER_NEEDS_UV1             lightmap UV
//   #define TOWER_NEEDS_UV2             dynamic lightmap UV
//   #define TOWER_NEEDS_POSITION_OLD    previous-frame position (skinned motion vectors)
//   #define TOWER_NEEDS_COLOR           vertex color
//   #define TOWER_NEEDS_SKINNING        sprite skinning weights/indices (consumed in the vertex stage)
//   #define TOWER_HAS_PRE_VERTEX        pass implements void TowerPreVertex(inout TowerVertexInput v)
//   #define TOWER_PASS_BENDS_ITSELF     domain does not bend; pass vertex does it (motion vectors)
//
// Then implement   Attributes TowerToAttributes(TowerControlPoint cp)
// define           TOWER_PASS_VERTEX   (Varyings F(Attributes))
// and include      TowerTessellationStages.hlsl
// -----------------------------------------------------------------------------

#include "TowerBend.hlsl"

struct TowerVertexInput
{
    float4 positionOS   : POSITION;
#if defined(TOWER_NEEDS_NORMAL)
    float3 normalOS     : NORMAL;
#endif
#if defined(TOWER_NEEDS_TANGENT)
    float4 tangentOS    : TANGENT;
#endif
    float2 uv0          : TEXCOORD0;
#if defined(TOWER_NEEDS_UV1)
    float2 uv1          : TEXCOORD1;
#endif
#if defined(TOWER_NEEDS_UV2)
    float2 uv2          : TEXCOORD2;
#endif
#if defined(TOWER_NEEDS_POSITION_OLD)
    float3 positionOld  : TEXCOORD4;
#endif
#if defined(TOWER_NEEDS_COLOR)
    float4 color        : COLOR;
#endif
#if defined(TOWER_NEEDS_SKINNING)
    float4 weights      : BLENDWEIGHTS;
    uint4  indices      : BLENDINDICES;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TowerControlPoint
{
    float4 positionOS   : INTERNALTESSPOS;
    float3 positionWS   : TEXCOORD8;    // flat world position (bent after the domain bend)
#if defined(TOWER_NEEDS_NORMAL)
    float3 normalOS     : NORMAL;
#endif
#if defined(TOWER_NEEDS_TANGENT)
    float4 tangentOS    : TANGENT;
#endif
    float2 uv0          : TEXCOORD0;
#if defined(TOWER_NEEDS_UV1)
    float2 uv1          : TEXCOORD1;
#endif
#if defined(TOWER_NEEDS_UV2)
    float2 uv2          : TEXCOORD2;
#endif
#if defined(TOWER_NEEDS_POSITION_OLD)
    float3 positionOld  : TEXCOORD4;
#endif
#if defined(TOWER_NEEDS_COLOR)
    float4 color        : COLOR;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TowerTessFactors
{
    float edge[3] : SV_TessFactor;
    float inside  : SV_InsideTessFactor;
};

// Flat world position of the vertex currently being finished. Lets a pass look up
// data that lives in flat space (e.g. 2D light textures) after the bend.
static float3 _TowerFlatPositionWS;

// Instance must already be set up (UNITY_SETUP_INSTANCE_ID).
void TowerBendControlPoint(inout TowerControlPoint cp)
{
    float3 flatWS = cp.positionWS;
    float3 bentWS = TowerBendPosition(flatWS, _TowerBend);
    cp.positionOS = float4(TransformWorldToObject(bentWS), 1.0);

#if defined(TOWER_NEEDS_NORMAL)
    float3 normalWS = TransformObjectToWorldNormal(cp.normalOS);
    cp.normalOS = TransformWorldToObjectNormal(TowerBendNormal(normalWS, flatWS, _TowerBend));
#endif
#if defined(TOWER_NEEDS_TANGENT)
    float3 tangentWS = TransformObjectToWorldDir(cp.tangentOS.xyz);
    cp.tangentOS.xyz = TransformWorldToObjectDir(TowerBendDirection(tangentWS, flatWS, _TowerBend));
#endif

    cp.positionWS = bentWS;
}

#endif // TOWER_TESSELLATION_INCLUDED
