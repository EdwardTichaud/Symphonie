Shader "Unlit/BlackReplaceWithTexture"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "white" {}
        _ReplaceTex ("Replacement Texture", 2D) = "white" {}
        _Threshold ("Black Threshold", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _ReplaceTex;
            float _Threshold;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert (appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 videoCol = tex2D(_MainTex, i.uv);
                float luminance = dot(videoCol.rgb, float3(0.299, 0.587, 0.114));

                fixed4 replaceCol = tex2D(_ReplaceTex, i.uv);
                fixed4 finalCol = (luminance < _Threshold) ? replaceCol : videoCol;
                finalCol.a = 1;
                return finalCol;
            }
            ENDCG
        }
    }
}
