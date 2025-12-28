Shader "Hidden/Symphonie/EdgeBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "EdgeBlur"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassFullscreen.hlsl"

            TEXTURE2D_X(_InputTexture);
            SAMPLER(sampler_InputTexture);

            float _EdgeBlurAmount;

            float4 SampleInput(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_InputTexture, sampler_InputTexture, uv);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 baseColor = SampleInput(uv);

                float amount = saturate(_EdgeBlurAmount);
                if (amount <= 0.0001f)
                    return baseColor;

                float2 centered = uv - 0.5f;
                centered.x *= _ScreenSize.x / _ScreenSize.y;
                float radial = saturate(length(centered) * 2.0f);
                float mask = smoothstep(1.0f - amount, 1.0f, radial);

                float2 texel = _ScreenSize.zw;
                float blurScale = lerp(0.0f, 4.0f, mask);
                float2 offset = texel * blurScale;

                float4 sum = baseColor;
                sum += SampleInput(uv + float2(offset.x, 0.0f));
                sum += SampleInput(uv + float2(-offset.x, 0.0f));
                sum += SampleInput(uv + float2(0.0f, offset.y));
                sum += SampleInput(uv + float2(0.0f, -offset.y));
                sum += SampleInput(uv + offset);
                sum += SampleInput(uv - offset);
                sum += SampleInput(uv + float2(offset.x, -offset.y));
                sum += SampleInput(uv + float2(-offset.x, offset.y));
                float4 blurred = sum / 9.0f;

                return lerp(baseColor, blurred, mask);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
