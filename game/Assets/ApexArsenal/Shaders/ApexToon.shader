// Cartoon lab surface for URP: two-band lighting, soft rim, optional world-space floor grid,
// and a built-in ink outline pass (inverted hull) so every lab panel gets a chunky cartoon edge.
Shader "ApexArsenal/Toon"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Tint", Color) = (0.62, 0.68, 0.8, 1)
        _Threshold ("Light Threshold", Range(-1, 1)) = 0.05
        _Softness ("Band Softness", Range(0.001, 0.5)) = 0.04
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(1, 8)) = 4
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.3
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _GridColor ("Grid Color", Color) = (0, 0, 0, 1)
        _GridScale ("Grid Cell Size", Float) = 2
        _GridWidth ("Grid Line Width", Range(0, 0.2)) = 0.03
        _GridStrength ("Grid Strength", Range(0, 1)) = 0
        _OutlineColor ("Outline Color", Color) = (0.08, 0.09, 0.11, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.2)) = 0.035
        [Enum(Normal, 0, Position, 1)] _OutlineMode ("Outline Direction", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadeColor;
            half _Threshold;
            half _Softness;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _EmissionColor;
            half4 _GridColor;
            float _GridScale;
            half _GridWidth;
            half _GridStrength;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineMode;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half lit = smoothstep(_Threshold - _Softness, _Threshold + _Softness, dot(normal, light.direction));
                lit *= smoothstep(0.35, 0.65, light.shadowAttenuation);

                half3 baseColor = _BaseColor.rgb;
                // Lab floor tiles: thin world-space grid lines on upward-facing surfaces.
                float2 cell = abs(frac(input.positionWS.xz / _GridScale + 0.5) - 0.5) * _GridScale;
                half gridLine = 1 - smoothstep(_GridWidth, _GridWidth + 0.015, min(cell.x, cell.y));
                baseColor = lerp(baseColor, _GridColor.rgb, gridLine * _GridStrength * step(0.5, normal.y));

                half3 color = lerp(baseColor * _ShadeColor.rgb, baseColor, lit) * saturate(light.color + 0.15);
                color += SampleSH(normal) * baseColor * 0.3;
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1 - saturate(dot(normal, viewDir)), _RimPower) * _RimStrength;
                color += rim * _RimColor.rgb;
                color += _EmissionColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // Position mode pushes away from the pivot, which keeps hard-edged boxes closed at the corners.
                float3 direction = _OutlineMode > 0.5
                    ? TransformObjectToWorldDir(input.positionOS.xyz)
                    : TransformObjectToWorldNormal(input.normalOS);
                positionWS += SafeNormalize(direction) * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Lit"
}
