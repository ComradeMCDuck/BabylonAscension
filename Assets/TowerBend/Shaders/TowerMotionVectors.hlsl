#ifndef TOWER_MOTION_VECTORS_INCLUDED
#define TOWER_MOTION_VECTORS_INCLUDED

// Object motion vectors for bent geometry. The previous position is bent with the
// previous frame's bend parameters, so the motion includes the "rotation" of the
// tower caused by the camera moving along flat X.
// Include after LitInput.hlsl (needs _BaseMap/_BaseColor/_Cutoff).

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

#define TOWER_NEEDS_POSITION_OLD
#define TOWER_PASS_BENDS_ITSELF
#include "TowerTessellation.hlsl"

struct Attributes
{
    float4 positionOS;
    float3 positionWS;      // flat
    float3 positionOld;
    float2 uv;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS                 : SV_POSITION;
    float4 positionCSNoJitter         : POSITION_CS_NO_JITTER;
    float4 previousPositionCSNoJitter : PREV_POSITION_CS_NO_JITTER;
#if defined(_ALPHATEST_ON)
    float2 uv                         : TEXCOORD0;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Attributes TowerToAttributes(TowerControlPoint cp)
{
    Attributes a = (Attributes)0;
    a.positionOS = cp.positionOS;
    a.positionWS = cp.positionWS;
    a.positionOld = cp.positionOld;
    a.uv = cp.uv0;
    UNITY_TRANSFER_INSTANCE_ID(cp, a);
    return a;
}

Varyings TowerMotionVectorVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // Same object-space round trip as the other passes, so depth matches exactly.
    float3 bentOS = TransformWorldToObject(TowerBendPosition(input.positionWS, _TowerBend));
    float4 bentWS = float4(TransformObjectToWorld(bentOS), 1.0);
    output.positionCS = TransformWorldToHClip(bentWS.xyz);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, bentWS);

    float3 prevOS = (unity_MotionVectorsParams.x == 1) ? input.positionOld : input.positionOS.xyz;
    float3 prevWS = mul(UNITY_PREV_MATRIX_M, float4(prevOS, 1.0)).xyz;
    prevWS = TowerBendPosition(prevWS, _TowerBendPrev);
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(prevWS, 1.0));

#if defined(_ALPHATEST_ON)
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
#endif
    return output;
}

#define TOWER_PASS_VERTEX TowerMotionVectorVertex
#include "TowerTessellationStages.hlsl"

float4 TowerMotionVectorFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_ALPHATEST_ON)
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
#endif
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif

    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0, 0);
}

#endif // TOWER_MOTION_VECTORS_INCLUDED
