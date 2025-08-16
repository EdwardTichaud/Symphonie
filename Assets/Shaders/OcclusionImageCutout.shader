Shader "Custom/OcclusionImageCutout"
{
    Properties
    {
        _MainTex        ("Texture", 2D) = "white" {}
        _MaskCenter     ("Mask Center (0..1, xy)", Vector) = (0.5, 0.5, 0, 0)
        _MaskRadius     ("Mask Radius (X, 0..1)", Float)   = 0.25
        _MaskRadiusXY   ("Mask Radius XY (0..1)", Vector) = (0, 0, 0, 0)

        _MaskTex        ("Mask Texture (alpha)", 2D) = "white" {}
        _MaskUseTex     ("Use Mask Texture (0/1)", Float) = 0
        _MaskTexCutoff  ("Mask Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        LOD 100

        Cull Back
        ZWrite On
        Blend One Zero
        AlphaToMask On

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;

            float4 _MaskCenter;
            float  _MaskRadius;
            float4 _MaskRadiusXY;

            sampler2D _MaskTex;
            float  _MaskUseTex;
            float  _MaskTexCutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv       : TEXCOORD0;
                float4 vertex   : SV_POSITION;
                float4 screenPos: TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex    = UnityObjectToClipPos(v.vertex);
                o.uv        = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                #if UNITY_UV_STARTS_AT_TOP
                    screenUV.y = 1.0 - screenUV.y;
                #endif

                float2 delta = screenUV - _MaskCenter.xy;

                // --- Mode texture ---
                if (_MaskUseTex > 0.5)
                {
                    float2 r = max(_MaskRadiusXY.xy, float2(1e-5,1e-5));
                    float2 uvMask = delta / r * 0.5 + 0.5;
                    if (any(uvMask < 0.0) || any(uvMask > 1.0))
                        return tex2D(_MainTex, i.uv);

                    float a = tex2D(_MaskTex, uvMask).a;
                    if (a > _MaskTexCutoff) discard;
                    return tex2D(_MainTex, i.uv);
                }

                // --- Fallback cercle/ellipse ---
                if (_MaskRadiusXY.x > 0 && _MaskRadiusXY.y > 0)
                {
                    float2 r = max(_MaskRadiusXY.xy, float2(1e-5,1e-5));
                    float d2 = dot(delta / r, delta / r);
                    if (d2 < 1.0) discard;
                }
                else
                {
                    float d = length(delta);
                    if (d < _MaskRadius) discard;
                }

                return tex2D(_MainTex, i.uv);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
