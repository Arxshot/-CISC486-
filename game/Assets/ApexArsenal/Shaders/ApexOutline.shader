// Ink outline for skinned characters (inverted hull along smooth normals). Used on a duplicate
// SkinnedMeshRenderer that shares the character's mesh and bones, so the original model is untouched.
Shader "ApexArsenal/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.08, 0.09, 0.11, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.018
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += SafeNormalize(TransformObjectToWorldNormal(input.normalOS)) * _OutlineWidth;
                return TransformWorldToHClip(positionWS);
            }

            half4 frag() : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
