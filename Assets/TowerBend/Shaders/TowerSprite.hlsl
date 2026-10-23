#ifndef TOWER_SPRITE_INCLUDED
#define TOWER_SPRITE_INCLUDED

// Shared sprite glue. Include after TowerTessellation.hlsl.
// Pass Attributes must be: COMMON_2D_INPUTS + half4 color : COLOR.

// Skinning and flipping happen on the original vertices, before tessellation.
void TowerPreVertex(inout TowerVertexInput v)
{
#if defined(SKINNED_SPRITE)
    v.positionOS.xyz = UnitySkinSprite(v.positionOS.xyz, v.indices, v.weights, unity_SpriteProps.z, 1.0f);
#endif
    SetUpSpriteInstanceProperties();
    v.positionOS.xyz = UnityFlipSprite(v.positionOS.xyz, unity_SpriteProps.xy);
}

Attributes TowerToAttributes(TowerControlPoint cp)
{
    Attributes a = (Attributes)0;
    a.positionOS = cp.positionOS.xyz;
    a.uv = cp.uv0;
    a.normal = cp.normalOS;
    a.color = cp.color;
    UNITY_TRANSFER_INSTANCE_ID(cp, a);
    return a;
}

// 2D Renderer lights are drawn from flat (unbent) Light2D geometry into screen-space
// light textures, so sample them at the vertex's FLAT screen position. Lights then
// stick to the sprites they illuminate. Near the screen edges the flat position can
// fall outside the light texture (clamped).
half2 TowerFlatLightingUV()
{
    float4 flatCS = TransformWorldToHClip(_TowerFlatPositionWS);
    return half2(ComputeScreenPos(flatCS / flatCS.w).xy);
}

#endif // TOWER_SPRITE_INCLUDED
