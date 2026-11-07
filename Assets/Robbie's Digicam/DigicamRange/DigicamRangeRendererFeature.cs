using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Memories.Rendering
{
    public sealed class DigicamRangeRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;

        private Material material;
        private DigicamRangePass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            shader = shader != null ? shader : Shader.Find("Hidden/Memories/DigicamRange");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new DigicamRangePass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (material == null || (cameraType != CameraType.Game && cameraType != CameraType.SceneView)) return;
            var settings = VolumeManager.instance.stack.GetComponent<DigicamRangeVolume>();
            if (settings != null && settings.IsActive()) renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
        }

        private sealed class DigicamRangePass : ScriptableRenderPass
        {
            private static readonly int LowId = Shader.PropertyToID("_Low");
            private static readonly int HighId = Shader.PropertyToID("_High");
            private static readonly int StrengthId = Shader.PropertyToID("_Strength");
            private readonly Material material;

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public float low, high, strength;
            }

            public DigicamRangePass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var settings = VolumeManager.instance.stack.GetComponent<DigicamRangeVolume>();
                if (settings == null || !settings.IsActive() || resourceData.isActiveTargetBackBuffer) return;

                var source = resourceData.activeColorTexture;
                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                var destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "Digicam Washed Range", false);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Washed Range", out var data))
                {
                    data.source = source;
                    data.material = material;
                    data.low = settings.low.value;
                    data.high = settings.high.value;
                    data.strength = settings.strength.value;
                    builder.UseTexture(source);
                    builder.SetRenderAttachment(destination, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        d.material.SetFloat(LowId, d.low);
                        d.material.SetFloat(HighId, d.high);
                        d.material.SetFloat(StrengthId, d.strength);
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Range Copy", out var data))
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
