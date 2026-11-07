Shader "Hidden/Memories/DigicamRange"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "Digicam Washed Range"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            SamplerState sampler_BlitTexture;
            float _Low;
            float _High;
            float _Strength;

            float4 Frag(Varyings input) : SV_Target
            {
                float4 source = _BlitTexture.SampleBias(sampler_BlitTexture, input.texcoord, _GlobalMipBias.x);
                float3 color = max(source.rgb, 0.0);

                // Lift the black point and bring the white point down in display space.
                // This is independent of the HDR auto-exposure pass.
                float whitePoint = max(_High, 0.001);
                float3 washed = lerp(float3(_Low, _Low, _Low), float3(1.0, 1.0, 1.0), saturate(color / whitePoint));
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                float clippedHighlight = smoothstep(whitePoint * 0.8, whitePoint, luminance);
                washed = lerp(washed, float3(1.0, 1.0, 1.0), clippedHighlight);
                return float4(lerp(source.rgb, washed, _Strength), source.a);
            }
            ENDHLSL
        }
    }
}
