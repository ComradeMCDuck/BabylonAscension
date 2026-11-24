// URP 2D Sprite-Lit-Default with hardware tessellation + tower bend.
//  - Universal2D      (2D Renderer)        : bent + tessellated, 2D lights sampled in flat space
//  - NormalsRendering (2D Renderer)        : intentionally FLAT, to match the flat 2D light textures
//  - UniversalForward (Universal Renderer) : bent + tessellated, lit by 3D lights, receives shadows
//    (sprites don't cast shadow maps; characters use a BlobShadow instead)
Shader "TowerBend/Sprite-Lit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
        _LightWrap("3D Light Wrap (0 = Lambert, 1 = ignore facing)", Range(0, 1)) = 1

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        // 0 = normal, 1 = solid white. Set per renderer by SpriteFlash (MaterialPropertyBlock).
        [HideInInspector] _FlashAmount("Flash Amount", Range(0, 1)) = 0
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma require tessellation tessHW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex TowerTessVertex
            #pragma hull TowerTessHull
            #pragma domain TowerTessDomain
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            // Must be identical in every pass (SRP Batcher).
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _LightWrap;
                half _FlashAmount;
            CBUFFER_END

            #define TOWER_NEEDS_NORMAL
            #define TOWER_NEEDS_COLOR
            #if defined(SKINNED_SPRITE)
                #define TOWER_NEEDS_SKINNING
            #endif
            #define TOWER_HAS_PRE_VERTEX
            #include "TowerTessellation.hlsl"
            #include "TowerSprite.hlsl"

            Varyings TowerSpriteLitVertex(Attributes input)
            {
                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.lightingUV = TowerFlatLightingUV();
                return o;
            }

            #define TOWER_PASS_VERTEX TowerSpriteLitVertex
            #include "TowerTessellationStages.hlsl"

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 color = CommonLitFragment(input, input.color);
                // White flash (SpriteFlash) after lighting, so it reads as solid white even in the dark.
                color.rgb = lerp(color.rgb, half3(1, 1, 1), _FlashAmount);
                return color;
            }
            ENDHLSL
        }

        // Flat on purpose: Light2D reads this buffer at the flat light positions.
        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            CBUFFER_START( UnityPerMaterial )
                half4 _Color;
                half _LightWrap;
                half _FlashAmount;
            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                SetUpSpriteInstanceProperties();
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        // Universal Renderer: lit by the scene's 3D lights (main + additional, Forward and Forward+).
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma require tessellation tessHW

            // Sprites draw in the transparent queue: sample the shadow map directly
            // instead of the screen-space shadow texture.
            #define _SURFACE_TYPE_TRANSPARENT 1

            // Receiving shadows (from the level geometry; sprites themselves don't cast).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            // Baked shadows of Mixed lights, read from the light probes' occlusion.
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING

            #pragma vertex TowerTessVertex
            #pragma hull TowerTessHull
            #pragma domain TowerTessDomain
            #pragma fragment LitForwardFragment

            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _LightWrap;
                half _FlashAmount;
            CBUFFER_END

            #define TOWER_NEEDS_NORMAL
            #define TOWER_NEEDS_COLOR
            #if defined(SKINNED_SPRITE)
                #define TOWER_NEEDS_SKINNING
            #endif
            #define TOWER_HAS_PRE_VERTEX
            #include "TowerTessellation.hlsl"
            #include "TowerSprite.hlsl"
            #include "TowerSpriteLighting.hlsl"

            Varyings TowerSpriteForwardVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                o.normalWS = TowerSpriteNormalWS();
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            #define TOWER_PASS_VERTEX TowerSpriteForwardVertex
            #include "TowerTessellationStages.hlsl"

            half4 LitForwardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 color = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                color.rgb *= TowerSpriteLighting(input.positionWS, input.normalWS, input.positionCS);
                // White flash (SpriteFlash) after lighting, so it reads as solid white even in the dark.
                color.rgb = lerp(color.rgb, half3(1, 1, 1), _FlashAmount);
                return color;
            }
            ENDHLSL
        }
    }
}
