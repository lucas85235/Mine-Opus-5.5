Shader "Mine/Overlay"
{
    // Multiplies the block underneath by a crack texture from the block texture array (break progress).
    Properties
    {
        _BlockTextures ("Block Textures", 2DArray) = "" {}
        _Slice ("Slice", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_BlockTextures);
            SAMPLER(sampler_BlockTextures);

            CBUFFER_START(UnityPerMaterial)
                float _Slice;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D_ARRAY(_BlockTextures, sampler_BlockTextures, i.uv, _Slice);
                return half4(lerp(half3(1.0h, 1.0h, 1.0h), c.rgb * 1.2h, c.a), 1.0h);
            }
            ENDHLSL
        }
    }
}
