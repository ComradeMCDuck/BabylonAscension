// URP 2D Sprite-Lit-Default with hardware tessellation + tower bend.
//  - Universal2D      (2D Renderer)        : bent + tessellated, 2D lights sampled in flat space
//  - NormalsRendering (2D Renderer)        : intentionally FLAT, to match the flat 2D light textures
//  - UniversalForward (Universal Renderer) : bent + tessellated, unlit like the URP original
Shader "TowerBend/Sprite-Lit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
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
                return CommonLitFragment(input, input.color);
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

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #pragma target 4.5
            #pragma require tessellation tessHW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex TowerTessVertex
            #pragma hull TowerTessHull
            #pragma domain TowerTessDomain
            #pragma fragment UnlitFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            #define TOWER_NEEDS_NORMAL
            #define TOWER_NEEDS_COLOR
            #if defined(SKINNED_SPRITE)
                #define TOWER_NEEDS_SKINNING
            #endif
            #define TOWER_HAS_PRE_VERTEX
            #include "TowerTessellation.hlsl"
            #include "TowerSprite.hlsl"

            Varyings TowerSpriteUnlitVertex(Attributes input)
            {
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            #define TOWER_PASS_VERTEX TowerSpriteUnlitVertex
            #include "TowerTessellationStages.hlsl"

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
}
