using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Memories.Rendering
{
    public sealed class CCDColorNoiseRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;

        private Material material;
        private CCDColorNoisePass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            shader = shader != null ? shader : Shader.Find("Hidden/Memories/CCDColorNoise");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new CCDColorNoisePass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (material == null || (cameraType != CameraType.Game && cameraType != CameraType.SceneView)) return;

            var settings = VolumeManager.instance.stack.GetComponent<CCDColorNoiseVolume>();
            if (settings != null && settings.IsActive()) renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
        }

        private sealed class CCDColorNoisePass : ScriptableRenderPass
        {
            private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            private static readonly int ChromaId = Shader.PropertyToID("_Chroma");
            private static readonly int ShadowBoostId = Shader.PropertyToID("_ShadowBoost");
            private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
            private static readonly int TemporalBlendId = Shader.PropertyToID("_TemporalBlend");
            private static readonly int FrameId = Shader.PropertyToID("_Frame");
            private readonly Material material;

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public float intensity, chroma, shadowBoost, pixelSize, temporalBlend, frame;
            }

            public CCDColorNoisePass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var settings = VolumeManager.instance.stack.GetComponent<CCDColorNoiseVolume>();
                if (settings == null || !settings.IsActive() || resourceData.isActiveTargetBackBuffer) return;

                var source = resourceData.activeColorTexture;
                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                var destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "CCD Color Noise", false);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("CCD Color Noise", out var data))
                {
                    data.source = source;
                    data.material = material;
                    data.intensity = settings.intensity.value;
                    data.chroma = settings.chroma.value;
                    data.shadowBoost = settings.shadowBoost.value;
                    data.pixelSize = settings.pixelSize.value;
                    data.temporalBlend = settings.temporalBlend.value;
#if UNITY_EDITOR
                    data.frame = Application.isPlaying
                        ? Time.frameCount
                        : Mathf.Floor((float)UnityEditor.EditorApplication.timeSinceStartup * 30f);
#else
                    data.frame = Time.frameCount;
#endif
                    builder.UseTexture(source);
                    builder.SetRenderAttachment(destination, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        d.material.SetFloat(IntensityId, d.intensity);
                        d.material.SetFloat(ChromaId, d.chroma);
                        d.material.SetFloat(ShadowBoostId, d.shadowBoost);
                        d.material.SetFloat(PixelSizeId, d.pixelSize);
                        d.material.SetFloat(TemporalBlendId, d.temporalBlend);
                        d.material.SetFloat(FrameId, d.frame);
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("CCD Color Noise Copy", out var data))
                {
                    data.source = destination;
                    builder.UseTexture(destination);
                    builder.SetRenderAttachment(source, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), 0, false));
                }
            }
        }
    }
}
