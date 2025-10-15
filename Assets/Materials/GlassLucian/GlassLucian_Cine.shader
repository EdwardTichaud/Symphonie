Shader "Symphonie/GlassLucian_Cine"
{
    Properties
    {
        _BaseColor ("Base Color (alpha = overall opacity)", Color) = (1,1,1,0.05)

        _IOR ("IOR (central)", Range(1.0, 2.0)) = 1.50
        _IOR_R ("IOR Red", Range(1.0, 2.0)) = 1.52
        _IOR_G ("IOR Green", Range(1.0, 2.0)) = 1.50
        _IOR_B ("IOR Blue", Range(1.0, 2.0)) = 1.48
        _DispersionAmount ("Dispersion Amount", Range(0,1)) = 0.35

        _AbsorptionColor ("Absorption Color (Beer-Lambert)", Color) = (0.65, 0.85, 1, 1)
        _AbsorptionDistance ("Absorption Distance (m)", Range(0.01, 5)) = 0.6
        _Thickness ("Thickness (m)", Range(0.0, 0.2)) = 0.03

        _RimColor ("Rim Color", Color) = (0.7, 0.9, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 10)) = 6.0
        _RimIntensity ("Rim Intensity (Emission)", Range(0, 2)) = 0.15

        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Intensity", Range(0, 2)) = 0.35

        _ScreenOffsetScale ("Screen UV Offset Scale", Range(0.0005, 0.02)) = 0.006
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="HDRenderPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "ForwardUnlit"
            // IMPORTANT en HDRP : utiliser SRPDefaultUnlit pour un pass unlit
            Tags{"LightMode"="SRPDefaultUnlit"}

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            // ---------- HDRP / Core includes (chemins mis à jour) ----------
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureXR.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            // ---------- Textures & uniforms ----------
            // Camera color buffer (opaque). En HDRP, TEXTURE2D_X nécessite TextureXR.hlsl
            TEXTURE2D_X(_CameraColorTexture);
            SamplerState s_linear_clamp_sampler;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _IOR, _IOR_R, _IOR_G, _IOR_B, _DispersionAmount;
                float4 _AbsorptionColor;
                float _AbsorptionDistance, _Thickness;
                float4 _RimColor;
                float _RimPower, _RimIntensity;
                float _NormalScale;
                float _ScreenOffsetScale;
            CBUFFER_END

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

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
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 tangentWS  : TEXCOORD2;
                float3 bitangentWS: TEXCOORD3;
                float2 uv         : TEXCOORD4;
                float3 viewDirWS  : TEXCOORD5;
                float4 screenPos  : TEXCOORD6; // for screen UV
            };

            void BuildTBN(float3 nWS, float4 tangentOS, float3x3 objectToWorld, out float3 tWS, out float3 bWS)
            {
                float3 t = normalize(mul((float3x3)objectToWorld, tangentOS.xyz));
                float3 b = normalize(cross(nWS, t) * tangentOS.w);
                tWS = t; bWS = b;
            }

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3x3 o2w      = (float3x3)unity_ObjectToWorld;

                float3 tWS, bWS;
                BuildTBN(normalWS, IN.tangentOS, o2w, tWS, bWS);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.positionWS = positionWS;
                OUT.normalWS   = normalWS;
                OUT.tangentWS  = tWS;
                OUT.bitangentWS= bWS;
                OUT.uv         = IN.uv;

                float3 camWS = GetCameraPositionWS();
                OUT.viewDirWS = normalize(camWS - positionWS);

                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            float3 ApplyNormalMap(float2 uv, float3 nWS, float3 tWS, float3 bWS)
            {
                float3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalScale);
                float3x3 TBN = float3x3(tWS, bWS, nWS);
                return normalize(mul(nTS, TBN));
            }

            float Fresnel(float3 n, float3 v, float power)
            {
                float f = 1.0 - saturate(dot(n, v));
                return pow(f, power);
            }

            float3 RefractDir(float3 V, float3 N, float eta)
            {
                float3 I = -normalize(V);
                return refract(I, normalize(N), 1.0 / eta);
            }

            float4 SampleCameraColor(float2 uv01)
            {
                // TEXTURE2D_X échantillonne en uv 0..1
                return SAMPLE_TEXTURE2D_X(_CameraColorTexture, s_linear_clamp_sampler, uv01);
            }

            float3 DispersionSample(float2 baseUV, float3 V, float3 N, float3 IORrgb, float offsetScale)
            {
                float3 Rdir = RefractDir(V, N, IORrgb.r);
                float3 Gdir = RefractDir(V, N, IORrgb.g);
                float3 Bdir = RefractDir(V, N, IORrgb.b);

                float2 uvR = baseUV + Rdir.xz * offsetScale;
                float2 uvG = baseUV + Gdir.xz * offsetScale;
                float2 uvB = baseUV + Bdir.xz * offsetScale;

                // Option: clamp pour éviter les bords noirs si l’offset sort de l’écran
                uvR = saturate(uvR); uvG = saturate(uvG); uvB = saturate(uvB);

                float3 colR = SampleCameraColor(uvR).rgb;
                float3 colG = SampleCameraColor(uvG).rgb;
                float3 colB = SampleCameraColor(uvB).rgb;

                return float3(colR.r, colG.g, colB.b);
            }

            float3 ApplyAbsorption(float3 color, float3 absorbColor, float thickness, float absorbDist)
            {
                float3 sigma = saturate(absorbColor);
                float3 transmittance = exp(-sigma * (thickness / max(absorbDist, 1e-3)));
                return color * transmittance;
            }

            float4 Frag (Varyings IN) : SV_Target
            {
                float3 nWS = ApplyNormalMap(IN.uv, IN.normalWS, IN.tangentWS, IN.bitangentWS);
                float3 vWS = normalize(IN.viewDirWS);

                float2 uv = IN.screenPos.xy / IN.screenPos.w;

                float3 IORrgb = float3(_IOR_R, _IOR_G, _IOR_B);
                float3 dispersed = DispersionSample(uv, vWS, nWS, IORrgb, _ScreenOffsetScale);

                // Central (moins coûteux) : couleur écran directe
                float3 central = SampleCameraColor(uv).rgb;

                float3 refracted = lerp(central, dispersed, saturate(_DispersionAmount));

                float3 absorbed = ApplyAbsorption(refracted * _BaseColor.rgb, _AbsorptionColor.rgb, _Thickness, _AbsorptionDistance);

                float rim = Fresnel(nWS, vWS, _RimPower) * _RimIntensity;
                float3 emission = _RimColor.rgb * rim;

                float alpha = saturate(_BaseColor.a);
                float3 color = absorbed + emission;

                return float4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
