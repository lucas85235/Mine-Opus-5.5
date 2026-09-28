Shader "Mine/Voxel"
{
    Properties
    {
        _BlockTextures ("Block Textures", 2DArray) = "" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaTest ("Alpha Test", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "MineCommon.hlsl"

        TEXTURE2D_ARRAY(_BlockTextures);
        SAMPLER(sampler_BlockTextures);

        CBUFFER_START(UnityPerMaterial)
            half _Cutoff;
            half _AlphaTest;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite [_ZWrite]
            ZTest [_ZTest]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _ALPHATEST_ON

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
                half4 albedo = SAMPLE_TEXTURE2D_ARRAY(_BlockTextures, sampler_BlockTextures, i.uv.xy, i.uv.z);
                #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
                #endif
                half3 col = albedo.rgb * i.light;
                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma shader_feature_local _ALPHATEST_ON

            struct AttributesD
            {
                float4 positionOS : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct VaryingsD
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
            };

            VaryingsD vertDepth(AttributesD input)
            {
                VaryingsD o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half fragDepth(VaryingsD i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                half a = SAMPLE_TEXTURE2D_ARRAY(_BlockTextures, sampler_BlockTextures, i.uv.xy, i.uv.z).a;
                clip(a - _Cutoff);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }
}
