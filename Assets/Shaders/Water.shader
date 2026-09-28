Shader "Mine/Water"
{
    Properties
    {
        _BlockTextures ("Block Textures", 2DArray) = "" {}
        _Alpha ("Alpha", Range(0, 1)) = 0.72
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MineCommon.hlsl"

            TEXTURE2D_ARRAY(_BlockTextures);
            SAMPLER(sampler_BlockTextures);

            CBUFFER_START(UnityPerMaterial)
                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
                half3 light : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = input.uv;
                o.light = MineLight(input.color);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 uvA = i.uv.xy + float2(t * 0.035, t * 0.05);
                float2 uvB = i.uv.xy * 0.75 + float2(-t * 0.03, t * 0.02) + 0.37;
                half3 a = SAMPLE_TEXTURE2D_ARRAY(_BlockTextures, sampler_BlockTextures, uvA, i.uv.z).rgb;
                half3 b = SAMPLE_TEXTURE2D_ARRAY(_BlockTextures, sampler_BlockTextures, uvB, i.uv.z).rgb;
                half3 col = lerp(a, b, 0.45h) * i.light;
                col = MixFog(col, i.fogFactor);
                return half4(col, _Alpha);
            }
            ENDHLSL
        }
    }
}
