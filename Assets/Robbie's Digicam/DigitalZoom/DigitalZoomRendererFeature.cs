using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Memories.Rendering
{
    public sealed class DigitalZoomRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;

        private Material material;
        private DigitalZoomPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            shader ??= Shader.Find("Hidden/Memories/DigitalZoom");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new DigitalZoomPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || renderingData.cameraData.cameraType != CameraType.Game) return;
            DigitalZoomVolume settings = VolumeManager.instance.stack.GetComponent<DigitalZoomVolume>();
            if (settings != null && settings.IsActive()) renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        private sealed class DigitalZoomPass : ScriptableRenderPass
        {
            private static readonly int ZoomId = Shader.PropertyToID("_Zoom");
            private readonly Material material;

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public float zoom;
            }

            public DigitalZoomPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                DigitalZoomVolume settings = VolumeManager.instance.stack.GetComponent<DigitalZoomVolume>();
                if (settings == null || !settings.IsActive()) return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                TextureHandle source = resources.activeColorTexture;
                TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "Digital Zoom", false);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Digital Zoom", out PassData data))
                {
                    data.source = source;
                    data.material = material;
                    data.zoom = settings.zoom.value;
                    builder.UseTexture(source);
                    builder.SetRenderAttachment(destination, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        d.material.SetFloat(ZoomId, d.zoom);
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digital Zoom Copy", out PassData data))
                {
                    data.source = destination;
                    builder.UseTexture(destination);
                    builder.SetRenderAttachment(source, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), 0, false));
                }
            }
        }
    }
}
