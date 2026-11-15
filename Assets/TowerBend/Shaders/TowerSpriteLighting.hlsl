#ifndef TOWER_SPRITE_LIGHTING_INCLUDED
#define TOWER_SPRITE_LIGHTING_INCLUDED

// Real-time 3D lighting for sprites drawn by the Universal Renderer (Forward / Forward+).
// Include after URP's Lighting.hlsl and after the UnityPerMaterial CBUFFER (needs _LightWrap).
//
// Sprites are flat cards, so plain Lambert turns them black whenever a light sits on
// the gameplay plane (a torch next to the player) or shines along it. _LightWrap fades
// the facing term out: 0 = Lambert, 1 = only light colour, distance and shadows count.

half3 TowerSpriteLightTerm(Light light, half3 normalWS)
{
    half facing = lerp(saturate(dot(normalWS, light.direction)), half(1.0), _LightWrap);
    return light.color * (light.distanceAttenuation * light.shadowAttenuation * facing);
}

// Returns the light arriving at the pixel (ambient + main light + additional lights);
// multiply the sprite colour by it.
half3 TowerSpriteLighting(float3 positionWS, half3 normalWS, float4 positionCS)
{
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);

    // Sprites are two-sided: light the side the camera sees.
    normalWS = normalize(normalWS);
    if (dot(normalWS, viewDirWS) < 0.0)
        normalWS = -normalWS;

    // The URP light-loop macros read these fields from a local named inputData.
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.positionCS = positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    // Per pixel, so shadow cascades work (transparents cannot use screen-space shadows).
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
#endif

    half4 shadowMask = inputData.shadowMask;

    // Ambient (Lighting window > Environment Lighting).
    half3 lighting = SampleSH(normalWS);

    Light mainLight = GetMainLight(inputData.shadowCoord, positionWS, shadowMask);
    lighting += TowerSpriteLightTerm(mainLight, normalWS);

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    // Forward+: extra directional lights are not in the clusters.
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        lighting += TowerSpriteLightTerm(light, normalWS);
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        lighting += TowerSpriteLightTerm(light, normalWS);
    LIGHT_LOOP_END
#endif

    return lighting;
}

#endif // TOWER_SPRITE_LIGHTING_INCLUDED
