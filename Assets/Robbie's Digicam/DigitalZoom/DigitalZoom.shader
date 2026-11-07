Shader "Hidden/Memories/DigitalZoom"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            SamplerState sampler_BlitTexture;
            float _Zoom;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = (input.texcoord - 0.5f) / max(_Zoom, 1.0f) + 0.5f;
                return _BlitTexture.SampleBias(sampler_BlitTexture, saturate(uv), _GlobalMipBias.x);
            }
            ENDHLSL
        }
    }
}
