Shader "Mine/Sky"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            half4 _MineSkyTop;
            half4 _MineSkyHorizon;
            half4 _MineSunsetColor;     // rgb colour, a strength
            float4 _MineSunDir;         // xyz: direction towards the sun
            float4 _MineSunAxis;        // xyz: rotation axis of the sun path
            half _MineStars;            // 0..1 star visibility
            float _MineStarAngle;       // radians, rotates the star field with the sky

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float3 RotateAround(float3 v, float3 axis, float angle)
            {
                float s = sin(angle), c = cos(angle);
                return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
            }

            // Square celestial body (sun / moon) centred on direction `center`, edges kept parallel to the horizon.
            float2 BodyCoords(float3 d, float3 center, out float facing)
            {
                facing = dot(d, center);
                float3 right = normalize(cross(float3(0.0, 1.0, 0.0), center));
                float3 up = cross(center, right);
                return float2(dot(d, right), dot(d, up)) / max(facing, 1e-4);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                // Base gradient.
                float t = saturate(h / 0.42);
                t = 1.0 - (1.0 - t) * (1.0 - t);
                half3 col = lerp(_MineSkyHorizon.rgb, _MineSkyTop.rgb, t);
                if (h < 0.0) col = lerp(_MineSkyHorizon.rgb, _MineSkyHorizon.rgb * 0.45, saturate(-h * 3.0));

                float3 sunDir = normalize(_MineSunDir.xyz);
                float3 axis = normalize(_MineSunAxis.xyz);

                // Sunrise / sunset glow, strongest towards the sun and near the horizon.
                float2 dh = normalize(d.xz + 1e-5);
                float2 sh = normalize(sunDir.xz + 1e-5);
                float toward = pow(saturate(dot(dh, sh) * 0.5 + 0.5), 3.0);
                float band = exp(-abs(h - 0.02) * 5.0);
                col += _MineSunsetColor.rgb * (_MineSunsetColor.a * toward * band);

                // Stars (rotate with the sky).
                if (_MineStars > 0.001 && h > -0.05)
                {
                    float3 sd = RotateAround(d, axis, _MineStarAngle);
                    float3 sp = sd * 140.0;
                    float3 cell = floor(sp);
                    float hs = Hash31(cell);
                    if (hs > 0.992)
                    {
                        float3 starPos = cell + 0.5 + (float3(Hash31(cell + 11.1), Hash31(cell + 23.7), Hash31(cell + 37.3)) - 0.5) * 0.5;
                        float dist = length(sp - starPos);
                        float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.0 + hs * 7.0) + hs * 91.0);
                        float s = saturate(1.0 - dist / 0.32) * twinkle;
                        col += s * _MineStars * saturate(h * 6.0 + 0.3);
                    }
                }

                // Sun: square disc plus soft glow.
                float facing;
                float2 q = BodyCoords(d, sunDir, facing);
                if (facing > 0.0)
                {
                    float m = max(abs(q.x), abs(q.y));
                    half3 sunCol = half3(1.0, 0.95, 0.78);
                    col += sunCol * exp(-m * 9.0) * 0.35;
                    if (m < 0.075) col = lerp(col, sunCol * 1.6, 1.0);
                }

                // Moon on the opposite side.
                float3 moonDir = -sunDir;
                q = BodyCoords(d, moonDir, facing);
                if (facing > 0.0)
                {
                    float m = max(abs(q.x), abs(q.y));
                    col += half3(0.6, 0.7, 0.9) * exp(-m * 12.0) * 0.12;
                    if (m < 0.05)
                    {
                        float2 cellM = floor((q + 0.05) / 0.1 * 4.0);
                        float crater = Hash31(float3(cellM, 3.0)) > 0.7 ? 0.8 : 1.0;
                        col = half3(0.86, 0.88, 0.92) * crater;
                    }
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
