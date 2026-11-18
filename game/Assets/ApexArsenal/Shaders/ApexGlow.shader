// Unlit neon for URP: light strips, lasers, glass, player rings and blob shadows.
// _Shape: 0 = solid, 1 = soft round blob (shadows), 2 = ring (player markers). Shapes use the mesh UVs (a Quad).
Shader "ApexArsenal/Glow"
{
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1.5
        _PulseSpeed ("Pulse Speed", Float) = 0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
        _EdgeGlow ("Edge Glow", Range(0, 1)) = 0
        [Enum(Solid, 0, Blob, 1, Ring, 2)] _Shape ("Shape", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                float _PulseSpeed;
                half _PulseAmount;
                half _EdgeGlow;
                float _Shape;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half pulse = 1 + sin(_Time.y * _PulseSpeed) * _PulseAmount * step(0.001, _PulseSpeed);
                half3 color = _BaseColor.rgb * _Intensity * pulse;
                half alpha = _BaseColor.a;

                float radius = length(input.uv - 0.5) * 2;
                if (_Shape > 1.5)
                    alpha *= smoothstep(0.62, 0.7, radius) * (1 - smoothstep(0.9, 1.0, radius));
                else if (_Shape > 0.5)
                    alpha *= 1 - smoothstep(0.2, 1.0, radius);

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half edge = pow(1 - saturate(abs(dot(normalize(input.normalWS), viewDir))), 3) * _EdgeGlow;
                color += edge * _BaseColor.rgb * 2;
                alpha = saturate(alpha + edge);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
