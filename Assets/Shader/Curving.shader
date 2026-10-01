Shader "Custom/Curving"
{
    Properties
    {
        _Curve ("Side Curve", Range(-1, 1)) = 0.3
        _Swirl ("Swirl", Range(-3, 3)) = 0.0
        _Zoom  ("Zoom", Range(0.5, 1.5)) = 0.9
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "CurveSidesPass"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            float _Curve;
            float _Swirl;
            float _Zoom;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 c = uv - 0.5;

                // Aspect-correct so the swirl is circular
                float aspect = _ScreenParams.x / _ScreenParams.y;
                c.x *= aspect;

                // Swirl: rotate more the farther from center
                float angle = _Swirl * dot(c, c);
                float s, co;
                sincos(angle, s, co);
                c = float2(c.x * co - c.y * s, c.x * s + c.y * co);

                // Side curve: sides sample from higher/lower the farther out in X
                c.y += _Curve * c.x * c.x;

                c.x /= aspect;
                c *= _Zoom;                 // zoom in to hide edge stretching
                float2 srcUV = c + 0.5;

                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, srcUV);
            }
            ENDHLSL
        }
    }
}
