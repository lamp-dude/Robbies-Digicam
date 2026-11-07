Shader "Hidden/Memories/CCDColorNoise"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "CCD Color Noise"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            SamplerState sampler_BlitTexture;
            float _Intensity;
            float _Chroma;
            float _ShadowBoost;
            float _PixelSize;
            float _TemporalBlend;
            float _Frame;

            float Hash(float2 cell, float seed)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7)) + seed * 17.13) * 43758.5453);
            }

            float3 HashRGB(float2 cell, float seed)
            {
                return float3(
                    Hash(cell, seed + 3.1),
                    Hash(cell + 17.0, seed + 9.7),
                    Hash(cell + 47.0, seed + 21.3));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 source = _BlitTexture.SampleBias(sampler_BlitTexture, input.texcoord, _GlobalMipBias.x);
                float2 screenPixel = input.texcoord * GetScaledScreenParams().xy;
                float2 colorCell = floor(screenPixel / max(_PixelSize, 1.0));
                float2 finePixel = floor(screenPixel);

                float3 fixedPattern = HashRGB(colorCell, 0.0);
                float3 changingPattern = HashRGB(colorCell, _Frame + 1.0);
                float3 rgbNoise = lerp(fixedPattern, changingPattern, _TemporalBlend) - 0.5;
                float3 lumaWeights = float3(0.2126, 0.7152, 0.0722);
                float3 colorNoise = rgbNoise - dot(rgbNoise, lumaWeights);
                float fineNoise = Hash(finePixel, _Frame + 31.0) - 0.5;

                float luminance = dot(saturate(source.rgb), lumaWeights);
                float shadowAmount = 1.0 + _ShadowBoost * (1.0 - luminance) * (1.0 - luminance);
                float highlightMask = 1.0 - smoothstep(0.8, 1.0, luminance);
                float amount = _Intensity * shadowAmount * highlightMask;
                float3 noisy = saturate(source.rgb + amount * (_Chroma * colorNoise + 0.25 * fineNoise));
                return float4(noisy, source.a);
            }
            ENDHLSL
        }
    }
}
