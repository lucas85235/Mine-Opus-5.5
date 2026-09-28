Shader "Mine/Clouds"
{
    Properties
    {
        _CloudTex ("Cloud Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudTex_ST;
            CBUFFER_END

            half4 _MineCloudColor;
            half4 _MineSkyHorizon;
            float4 _MineCloudParams; // xy: scroll offset, z: world units per texture repeat, w: fade distance

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = (i.positionWS.xz + _MineCloudParams.xy) / _MineCloudParams.z;
                half a = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv).a;
                clip(a - 0.5);
                float dist = length(i.positionWS.xz - _WorldSpaceCameraPos.xz);
                float fade = 1.0 - smoothstep(_MineCloudParams.w * 0.45, _MineCloudParams.w, dist);
                half3 col = lerp(_MineSkyHorizon.rgb, _MineCloudColor.rgb, saturate(fade * 1.4));
                return half4(col, 0.82 * fade);
            }
            ENDHLSL
        }
    }
}
