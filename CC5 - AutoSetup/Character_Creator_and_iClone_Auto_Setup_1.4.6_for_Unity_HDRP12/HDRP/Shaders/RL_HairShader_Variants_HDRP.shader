// Shader simplifié pour remplacer la version ShaderGraph incompatible avec Unity 6.2.
Shader "Shader Graphs/RL_HairShader_Variants_HDRP"
{
    Properties
    {
        _BaseColor ("Couleur de base", Color) = (0.4, 0.3, 0.2, 1)
        _Smoothness ("Lissage", Range(0,1)) = 0.4
    }
    HLSLINCLUDE
    ENDHLSL
    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Name "ForwardOnly"
            Tags{ "LightMode" = "ForwardOnly" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 cameraPosWS = _WorldSpaceCameraPos;
                float3 viewDirWS = normalize(cameraPosWS - input.positionWS);

                // Éclairage Lambert simple : suffisant pour éviter les dépendances HDRP avancées.
                float ndv = saturate(dot(normalWS, viewDirWS));
                float3 diffuse = _BaseColor.rgb * (0.4 + 0.6 * ndv);

                return float4(diffuse, _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "HDRP/Lit"
}
