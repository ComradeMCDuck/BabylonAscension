#ifndef TOWER_PARTICLES_INCLUDED
#define TOWER_PARTICLES_INCLUDED

// -----------------------------------------------------------------------------
// Tower bend for URP particle shaders.
//
// Every URP particle pass gets its world position/normal through
// GetParticleVertexPositionInputs / GetParticleVertexNormalInputs (this also covers
// GPU-instanced mesh particles, which bypass UNITY_MATRIX_M). The shader renames
// URP's versions before including the particle Input file:
//
//   #define GetParticleVertexPositionInputs TowerURP_GetParticleVertexPositionInputs
//   #define GetParticleVertexNormalInputs   TowerURP_GetParticleVertexNormalInputs
//   #include_with_pragmas ".../Particles/ParticlesXxxInput.hlsl"
//   #include "TowerParticles.hlsl"      // <- this file: bent versions under the original names
//   #include ".../Particles/ParticlesXxxPass.hlsl"
//
// The URP pass code is then used unchanged and calls the bent versions.
//
// No tessellation: particles are small quads, so per-vertex bending is enough.
// A billboard ends up rotated by its angle around the tower, which is within a few
// degrees of facing the camera across the screen. Trails and stretched billboards
// follow the curved surface, as they should.
// -----------------------------------------------------------------------------

#undef GetParticleVertexPositionInputs
#undef GetParticleVertexNormalInputs

#include "TowerBend.hlsl"

// Flat position of the vertex being processed; set by the position call, which every
// URP particle pass makes before the normal call.
static float3 _TowerParticleFlatWS;

VertexPositionInputs GetParticleVertexPositionInputs(float3 positionOS)
{
    VertexPositionInputs v = TowerURP_GetParticleVertexPositionInputs(positionOS);
    _TowerParticleFlatWS = v.positionWS;

    v.positionWS = TowerBendPosition(v.positionWS, _TowerBend);
    v.positionVS = TransformWorldToView(v.positionWS);
    v.positionCS = TransformWorldToHClip(v.positionWS);
    float4 ndc = v.positionCS * 0.5f;
    v.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    v.positionNDC.zw = v.positionCS.zw;
    return v;
}

VertexNormalInputs GetParticleVertexNormalInputs(float3 normalOS, float4 tangentOS)
{
    VertexNormalInputs tbn = TowerURP_GetParticleVertexNormalInputs(normalOS, tangentOS);
    tbn.normalWS = TowerBendNormal(tbn.normalWS, _TowerParticleFlatWS, _TowerBend);
    tbn.tangentWS = real3(SafeNormalize(TowerBendDirection(float3(tbn.tangentWS), _TowerParticleFlatWS, _TowerBend)));
    tbn.bitangentWS = real3(SafeNormalize(TowerBendDirection(float3(tbn.bitangentWS), _TowerParticleFlatWS, _TowerBend)));
    return tbn;
}

VertexNormalInputs GetParticleVertexNormalInputs(float3 normalOS)
{
    return GetParticleVertexNormalInputs(normalOS, float4(0, 0, 0, 1));
}

#endif // TOWER_PARTICLES_INCLUDED
