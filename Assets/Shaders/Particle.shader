Shader "Mine/Particle"
{
    // Mesh particles (block debris, poofs). Colour comes pre-lit from script through the vertex colour.
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                half fogFactor : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(input.normalOS);
                half shade = (half)(saturate(0.75 + 0.25 * n.y) * (1.0 - 0.2 * abs(n.x)));
                o.positionCS = p.positionCS;
                o.color = half4(input.color.rgb * shade, input.color.a);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 col = MixFog(i.color.rgb, i.fogFactor);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
}
