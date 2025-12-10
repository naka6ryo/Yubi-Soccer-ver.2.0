Shader "Custom/ReinhardtHolographicShield"
{
    Properties
    {
        _MainTex ("Noise Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}

        _ShieldColor ("Shield Color", Color) = (0.0, 0.5, 1.0, 1.0)
        _EdgeColor ("Hex Pattern Color", Color) = (0.3, 0.8, 1.0, 1.0)

        _FresnelPower ("Fresnel Power", Float) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.3

        _TimeScale ("Time Scale", Float) = 1.0
        _HexScale ("Hexagon Scale", Float) = 10.0
        _HexEdgeSmooth ("Hex Edge Smoothness", Range(0.01, 0.5)) = 0.05
        _HexThickness ("Hex Line Thickness", Range(0.0, 0.5)) = 0.05

        [Header(Collision Effect)]
        // Note: arrays cannot be exposed directly in the Properties block.
        // These are updated via a collision manager script.
        // _CollisionPoints, _CollisionRadii, _CollisionIntensities, _CollisionStartTimes,
        // _NumCollisions, and _EffectDuration are set externally.
        
        [Header(General Settings)]
        _BaseAlpha ("Base Alpha", Range(0.0, 0.2)) = 0.05
        _Distortion ("Distortion Amount", Range(0, 50)) = 10

        [Header(Scan Effect)]
        _ScanSpeed ("Scan Speed", Float) = 1.0
        _ScanWidth ("Scan Width", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back
        ZWrite Off

        Pass
        {
            Name "ReinhardtShield"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Define maximum collisions supported.
            #define MAX_COLLISIONS 4

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 positionNDC: TEXCOORD3;
                float3 viewDirWS  : TEXCOORD4;
                float3x3 TBN      : TEXCOORD5;
            };

            // Texture declarations.
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NormalMap_ST;

                float4 _ShieldColor;
                float4 _EdgeColor;

                float _FresnelPower;
                float _FresnelIntensity;

                float _TimeScale;
                float _HexScale;
                float _HexEdgeSmooth;
                float _HexThickness;

                // Collision arrays.
                float4 _CollisionPoints[MAX_COLLISIONS];
                float _CollisionRadii[MAX_COLLISIONS];
                float _CollisionIntensities[MAX_COLLISIONS];
                float _CollisionStartTimes[MAX_COLLISIONS];
                int   _NumCollisions;
                float _EffectDuration;

                float _BaseAlpha;
                float _Distortion;

                float _ScanSpeed;
                float _ScanWidth;
            CBUFFER_END

            // Basic hexagon pattern: returns 1 inside hex border, 0 outside.
            float HexagonPattern(float2 p, float size, float smooth)
            {
                p = abs(p);
                float hex = max(p.x, dot(p, normalize(float2(0.5, 0.866))));
                return 1.0 - smoothstep(size - smooth, size, hex);
            }

            // Hexagon border.
            float HexagonBorder(float2 p, float size, float lineWidth, float smooth)
            {
                float outer = HexagonPattern(p, size, smooth);
                float inner = HexagonPattern(p, size - lineWidth, smooth);
                return outer - inner;
            }

            // Generates a hex grid pattern.
            float HexGrid(float2 uv, float scale, float lineWidth, float smooth)
            {
                float2 p = uv * scale;
                float2 grid = float2(1.0, 0.866);
                float2 gridUV = float2(p.x + 0.5 * floor(p.y / grid.y) % 2.0, p.y);
                float2 cellUV = frac(gridUV / grid) * 2.0 - 1.0;
                return HexagonBorder(cellUV, 0.95, lineWidth, smooth);
            }

            // Computes a collision mask for a single collision.
            // Also computes a fade factor based on the collision start time.
            float CollisionMask(float3 fragPos, float3 collisionPoint, float radius, float collisionStart, float effectDuration)
            {
                float baseMask = 1.0 - smoothstep(radius * 0.8, radius, distance(fragPos, collisionPoint));
                float fade = saturate(1.0 - ((_Time.y - collisionStart) / effectDuration));
                return baseMask * fade;
            }

            // Scan mask: produces a horizontal moving band that is always active.
            // Returns 1 inside the band, 0 outside.
            float ScanMask(float2 uv)
            {
                float scanPos = frac(_Time.y * _ScanSpeed);
                float mask = smoothstep(scanPos - _ScanWidth, scanPos, uv.y)
                           - smoothstep(scanPos, scanPos + _ScanWidth, uv.y);
                return mask;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.positionNDC = OUT.positionCS * 0.5f;
                OUT.positionNDC.xy = float2(OUT.positionNDC.x, OUT.positionNDC.y * _ProjectionParams.x)
                                     + float2(OUT.positionNDC.w, OUT.positionNDC.w);
                OUT.positionNDC.zw = OUT.positionCS.zw;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
                float3 bitangentWS = cross(OUT.normalWS, tangentWS) * IN.tangentOS.w;
                OUT.TBN = float3x3(tangentWS, bitangentWS, OUT.normalWS);
                OUT.viewDirWS = GetWorldSpaceViewDir(OUT.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Animation/distortion from noise.
                float2 noiseUV = IN.uv * 2.0 + _Time.y * _TimeScale * 0.1;
                float noise = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, noiseUV).r;
                float noise2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, noiseUV * 1.5 + float2(0.7, 0.3)).r;

                // Sample normal map for distortion.
                float3 normalMap = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, 
                                    IN.uv + float2(noise - 0.5, noise2 - 0.5) * 0.05));
                float3 normalTS = normalize(normalMap);
                float3 normalWS = normalize(mul(normalTS, IN.TBN));

                // Fresnel for subtle glow.
                float3 viewDir = normalize(IN.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);

                // Distort UV.
                float2 distortedUV = IN.uv + (normalTS.xy * _Distortion * 0.01);
                // Base hex pattern.
                float baseHex = HexGrid(distortedUV, _HexScale, _HexThickness, _HexEdgeSmooth);
                // Animated hex pattern (for scan).
                float timeOffset = _Time.y * _TimeScale;
                float animatedHex = baseHex * (0.8 + 0.2 * sin(timeOffset + IN.positionWS.y * 2.0));

                // Scan mask is computed from UV.
                float scanMask = ScanMask(IN.uv);
                float scanHex = animatedHex * scanMask;

                // Compute cumulative collision mask from all active collisions.
                float collisionHex = 0.0;
                for (int i = 0; i < _NumCollisions; i++)
                {
                    float mask = CollisionMask(IN.positionWS, _CollisionPoints[i].xyz, _CollisionRadii[i], _CollisionStartTimes[i], _EffectDuration);
                    mask *= _CollisionIntensities[i];
                    collisionHex = max(collisionHex, baseHex * mask);
                }

                // Combine scan and collision effects independently.
                float finalHex = saturate(max(scanHex, collisionHex));

                // Blend base shield color with hex (edge) color.
                float3 finalColor = lerp(_ShieldColor.rgb, _EdgeColor.rgb, finalHex);
                finalColor += _ShieldColor.rgb * fresnel * _FresnelIntensity * 0.5;

                float alpha = saturate(_BaseAlpha + finalHex);
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
