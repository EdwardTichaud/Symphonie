Shader "Custom/BasicDissolve_Unlit"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _DissolveFade ("Dissolve Fade", Range(0, 1)) = 0.0
        _DissolveEdge ("Dissolve Edge Width", Range(0.01, 0.2)) = 0.05
        _DissolveColor ("Dissolve Edge Color", Color) = (1, 0.5, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "DissolvePass"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc" // 💡 nécessaire pour UnityObjectToClipPos et TRANSFORM_TEX

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;

            float _DissolveFade;
            float _DissolveEdge;
            float4 _DissolveColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float noise = tex2D(_NoiseTex, i.uv).r;
                float4 baseColor = tex2D(_MainTex, i.uv);

                float dissolveMask = step(_DissolveFade, noise);
                float edge = smoothstep(_DissolveFade - _DissolveEdge, _DissolveFade, noise);

                float3 finalColor = lerp(_DissolveColor.rgb, baseColor.rgb, dissolveMask);

                clip(dissolveMask - 0.01); // coupe physiquement les pixels

                return float4(finalColor, 1.0);
            }

            ENDCG
        }
    }

    FallBack "Diffuse"
}
