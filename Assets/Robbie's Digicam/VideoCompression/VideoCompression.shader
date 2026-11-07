Shader "Hidden/Memories/VideoCompression"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "Digicam Video Compression"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            SamplerState sampler_BlitTexture;
            float _Intensity;
            float _BlockSize;
            float _Blockiness;
            float _ChromaLoss;
            float _MosquitoNoise;
            float4 _SourceSize;
            float _Frame;

            float3 ReadColor(float2 pixel)
            {
                float2 uv = saturate(pixel * _SourceSize.zw);
                return _BlitTexture.SampleBias(sampler_BlitTexture, uv, _GlobalMipBias.x).rgb;
            }

            float Luma(float3 color)
            {
                return dot(color, float3(0.25, 0.5, 0.25));
            }

            float2 Chroma(float3 color)
            {
                return float2(0.5 * (color.r - color.b),
                              0.5 * color.g - 0.25 * (color.r + color.b));
            }

            float Hash(float2 cell, float seed)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7)) + seed * 19.19) * 43758.5453);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 original = _BlitTexture.SampleBias(sampler_BlitTexture, input.texcoord, _GlobalMipBias.x);
                float2 pixel = input.texcoord * _SourceSize.xy;
                float blockSize = max(4.0, _BlockSize);
                float2 block = floor(pixel / blockSize);
                float2 blockOrigin = block * blockSize;

                // A 2x2 chroma footprint approximates 4:2:0 subsampling
                float2 chromaOrigin = floor(pixel * 0.5) * 2.0;
                float2 chromaSample = 0.25 * (
                    Chroma(ReadColor(chromaOrigin + float2(0.5, 0.5))) +
                    Chroma(ReadColor(chromaOrigin + float2(1.5, 0.5))) +
                    Chroma(ReadColor(chromaOrigin + float2(0.5, 1.5))) +
                    Chroma(ReadColor(chromaOrigin + float2(1.5, 1.5))));

                // Four low-frequency samples stand in for lost block detail.
                float2 quarter = blockSize * 0.25;
                float blockLuma = 0.25 * (
                    Luma(ReadColor(blockOrigin + quarter)) +
                    Luma(ReadColor(blockOrigin + float2(3.0 * quarter.x, quarter.y))) +
                    Luma(ReadColor(blockOrigin + float2(quarter.x, 3.0 * quarter.y))) +
                    Luma(ReadColor(blockOrigin + 3.0 * quarter)));

                float luma = Luma(original.rgb);
                float blockMix = _Blockiness * 0.38;
                luma = lerp(luma, blockLuma, blockMix);

                // Coarser chroma steps and occasional block-level quantizer drift.
                float quality = _Blockiness;
                float levels = lerp(256.0, 46.0, quality);
                float blockDrift = (Hash(block, floor(_Frame / 5.0)) - 0.5) * quality * 0.018;
                luma = floor(saturate(luma + blockDrift) * levels + 0.5) / levels;
                float2 chroma = lerp(Chroma(original.rgb), chromaSample, _ChromaLoss);
                float chromaLevels = lerp(128.0, 26.0, _ChromaLoss);
                chroma = floor(chroma * chromaLevels + 0.5) / chromaLevels;

                // Edge-localized crawling texture suggests mosquito artifacts.
                float edge = abs(luma - Luma(ReadColor(pixel + float2(1.5, 0.0))))
                           + abs(luma - Luma(ReadColor(pixel + float2(0.0, 1.5))));
                float crawl = Hash(floor(pixel), floor(_Frame / 3.0)) - 0.5;
                luma += crawl * saturate(edge * 3.0) * _MosquitoNoise * 0.12;

                float3 compressed = float3(luma + chroma.x - chroma.y,
                                           luma + chroma.y,
                                           luma - chroma.x - chroma.y);
                return float4(lerp(original.rgb, saturate(compressed), _Intensity), original.a);
            }
            ENDHLSL
        }
    }
}
