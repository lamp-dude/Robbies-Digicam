Shader "Hidden/Memories/DirectionalBloom"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        SamplerState sampler_BlitTexture;
        TEXTURE2D(_KernelTex);
        SAMPLER(sampler_KernelTex);
        TEXTURE2D_X(_DigicamStreakTexture);
        TEXTURE2D_X(_DigicamHaloTexture);

        float2 _SourceSize;
        float _Threshold;
        float _SoftKnee;
        float _VerticalRadius;
        float _HaloRadius;
        float _SunSourceIntensity;
        float _StreakIntensity;
        float _HaloIntensity;
        float4 _StripeTint;
        float4 _SunPoint;

        float3 SourceAt(float2 uv)
        {
            float2 inside = step(0.0, uv) * step(uv, 1.0);
            return max(_BlitTexture.SampleBias(sampler_BlitTexture, saturate(uv), _GlobalMipBias.x).rgb, 0.0)
                 * inside.x * inside.y;
        }

        float4 ExtractHighlights(Varyings input) : SV_Target
        {
            float3 source = SourceAt(input.texcoord);
            float2 pixel = rcp(_SourceSize);
            float3 candidate = SourceAt(input.texcoord + float2(4.0, 0.0) * pixel);
            if (max(candidate.r, max(candidate.g, candidate.b)) > max(source.r, max(source.g, source.b))) source = candidate;
            candidate = SourceAt(input.texcoord + float2(-4.0, 0.0) * pixel);
            if (max(candidate.r, max(candidate.g, candidate.b)) > max(source.r, max(source.g, source.b))) source = candidate;
            candidate = SourceAt(input.texcoord + float2(0.0, 4.0) * pixel);
            if (max(candidate.r, max(candidate.g, candidate.b)) > max(source.r, max(source.g, source.b))) source = candidate;
            candidate = SourceAt(input.texcoord + float2(0.0, -4.0) * pixel);
            if (max(candidate.r, max(candidate.g, candidate.b)) > max(source.r, max(source.g, source.b))) source = candidate;
            float brightness = max(source.r, max(source.g, source.b));
            float knee = max(_SoftKnee, 0.0001);
            float soft = clamp(brightness - _Threshold + knee, 0.0, 2.0 * knee);
            soft = soft * soft / (4.0 * knee);
            float fraction = max(brightness - _Threshold, soft) / max(brightness, 0.0001);
            float3 highlights = source * fraction;

            if (_SunPoint.z > 0.5 && _SunSourceIntensity > 0.0)
            {
                float rawDepth = SampleSceneDepth(_SunPoint.xy);
                #if UNITY_REVERSED_Z
                    float skyVisible = 1.0 - smoothstep(0.0001, 0.002, rawDepth);
                #else
                    float skyVisible = smoothstep(0.998, 0.9999, rawDepth);
                #endif
                float2 delta = (input.texcoord - _SunPoint.xy) * _SourceSize;
                float impulse = exp2(-0.5 * dot(delta, delta) / 64.0);
                highlights += impulse * _SunSourceIntensity * skyVisible;
            }
            return float4(highlights, 1.0);
        }

        // Direct 2D convolution of the highlight image with the kernel's
        // narrow horizontal profile and full vertical profile
        float4 ConvolvePSF(Varyings input) : SV_Target
        {
            float3 total = 0.0;
            float weightTotal = 0.0;
            [loop]
            for (int y = -64; y <= 64; y++)
            {
                float ty = y / 64.0;
                [loop]
                for (int x = -4; x <= 4; x++)
                {
                    float tx = x / 4.0;
                    float2 sourceUV = input.texcoord + float2(tx * 12.0 / _SourceSize.x,
                                                             ty * _VerticalRadius / _SourceSize.y);
                    float2 kernelUV = float2(0.5 + tx * 0.025, 0.5 + ty * 0.5);
                    float3 kernel = SAMPLE_TEXTURE2D(_KernelTex, sampler_KernelTex, kernelUV).rgb;
                    kernel *= smoothstep(0.0, 0.22, 1.0 - abs(ty));
                    float weight = dot(kernel, float3(0.2126, 0.7152, 0.0722));
                    total += SourceAt(sourceUV) * kernel;
                    weightTotal += weight;
                }
            }
            return float4(total / max(weightTotal, 0.0001) * _StripeTint.rgb, 1.0);
        }

        float4 HaloHorizontal(Varyings input) : SV_Target
        {
            float3 total = 0.0;
            float weightTotal = 0.0;
            [unroll]
            for (int i = -8; i <= 8; i++)
            {
                float t = i / 8.0;
                float weight = exp2(-4.0 * t * t);
                total += SourceAt(input.texcoord + float2(t * _HaloRadius / _SourceSize.x, 0.0)) * weight;
                weightTotal += weight;
            }
            return float4(total / weightTotal, 1.0);
        }

        float4 HaloVertical(Varyings input) : SV_Target
        {
            float3 total = 0.0;
            float weightTotal = 0.0;
            [unroll]
            for (int i = -8; i <= 8; i++)
            {
                float t = i / 8.0;
                float weight = exp2(-4.0 * t * t);
                total += SourceAt(input.texcoord + float2(0.0, t * _HaloRadius / _SourceSize.y)) * weight;
                weightTotal += weight;
            }
            return float4(total / weightTotal, 1.0);
        }

        float4 CompositeBloom(Varyings input) : SV_Target
        {
            float4 source = _BlitTexture.SampleBias(sampler_BlitTexture, input.texcoord, _GlobalMipBias.x);
            float3 streak = SAMPLE_TEXTURE2D_X(_DigicamStreakTexture, sampler_LinearClamp, input.texcoord).rgb;
            float3 halo = SAMPLE_TEXTURE2D_X(_DigicamHaloTexture, sampler_LinearClamp, input.texcoord).rgb;
            return float4(source.rgb + _StreakIntensity * streak + _HaloIntensity * halo, source.a);
        }
        ENDHLSL

        Pass
        {
            Name "Extract HDR Highlights"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ExtractHighlights
            ENDHLSL
        }
        Pass
        {
            Name "Convolve 2D Point Spread Function"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ConvolvePSF
            ENDHLSL
        }
        Pass
        {
            Name "Halo Horizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment HaloHorizontal
            ENDHLSL
        }
        Pass
        {
            Name "Halo Vertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment HaloVertical
            ENDHLSL
        }
        Pass
        {
            Name "Composite Convolution Bloom"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeBloom
            ENDHLSL
        }
    }
}
