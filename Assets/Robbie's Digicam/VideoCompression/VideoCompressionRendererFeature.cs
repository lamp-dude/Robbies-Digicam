using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Memories.Rendering
{
    public sealed class VideoCompressionRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;

        private Material material;
        private CompressionPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            shader = shader != null ? shader : Shader.Find("Hidden/Memories/VideoCompression");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new CompressionPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (material == null || (cameraType != CameraType.Game && cameraType != CameraType.SceneView)) return;

            var settings = VolumeManager.instance.stack.GetComponent<VideoCompressionVolume>();
            if (settings != null && settings.IsActive()) renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
        }

        private sealed class CompressionPass : ScriptableRenderPass
        {
            private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            private static readonly int BlockSizeId = Shader.PropertyToID("_BlockSize");
            private static readonly int BlockinessId = Shader.PropertyToID("_Blockiness");
            private static readonly int ChromaLossId = Shader.PropertyToID("_ChromaLoss");
            private static readonly int MosquitoNoiseId = Shader.PropertyToID("_MosquitoNoise");
            private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
            private static readonly int FrameId = Shader.PropertyToID("_Frame");
            private readonly Material material;

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public float intensity, blockSize, blockiness, chromaLoss, mosquitoNoise, frame;
                public Vector4 sourceSize;
            }

            public CompressionPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var settings = VolumeManager.instance.stack.GetComponent<VideoCompressionVolume>();
                if (settings == null || !settings.IsActive() || resourceData.isActiveTargetBackBuffer) return;

                var source = resourceData.activeColorTexture;
                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                var destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "Digicam Video Compression", false);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Video Compression", out var data))
                {
                    data.source = source;
                    data.material = material;
                    data.intensity = settings.intensity.value;
                    data.blockSize = settings.blockSize.value;
                    data.blockiness = settings.blockiness.value;
                    data.chromaLoss = settings.chromaLoss.value;
                    data.mosquitoNoise = settings.mosquitoNoise.value;
                    data.sourceSize = new Vector4(descriptor.width, descriptor.height, 1f / descriptor.width, 1f / descriptor.height);
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
                        d.material.SetFloat(BlockSizeId, d.blockSize);
                        d.material.SetFloat(BlockinessId, d.blockiness);
                        d.material.SetFloat(ChromaLossId, d.chromaLoss);
                        d.material.SetFloat(MosquitoNoiseId, d.mosquitoNoise);
                        d.material.SetVector(SourceSizeId, d.sourceSize);
                        d.material.SetFloat(FrameId, d.frame);
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Video Compression Copy", out var data))
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
