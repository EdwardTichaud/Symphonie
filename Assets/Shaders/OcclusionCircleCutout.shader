Shader "Custom/OcclusionCircleCutout"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MaskCenter ("Mask Center", Vector) = (0,0,0,0)
        _MaskRadius ("Mask Radius", Float) = 0.25
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float2 _MaskCenter;
            float _MaskRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float dist = distance(screenUV, _MaskCenter);
                if (dist < _MaskRadius)
                    discard; // On rend transparent l'intérieur du cercle
                return tex2D(_MainTex, i.uv);
            }
            ENDHLSL
        }
    }
}
