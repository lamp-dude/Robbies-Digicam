using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Memories.Rendering
{
    public sealed class DirectionalBloomRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;

        private Material material;
        private BloomPass extractPass;
        private BloomPass compositePass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            shader = shader != null ? shader : Shader.Find("Hidden/Memories/DirectionalBloom");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            extractPass = new BloomPass(material, false);
            compositePass = new BloomPass(material, true);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (material == null || (cameraType != CameraType.Game && cameraType != CameraType.SceneView)) return;

            var settings = VolumeManager.instance.stack.GetComponent<DirectionalBloomVolume>();
            if (settings == null || !settings.IsActive()) return;
            renderer.EnqueuePass(extractPass);
            renderer.EnqueuePass(compositePass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        private sealed class BloomPass : ScriptableRenderPass
        {
            private static readonly int KernelId = Shader.PropertyToID("_KernelTex");
            private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
            private static readonly int SoftKneeId = Shader.PropertyToID("_SoftKnee");
            private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
            private static readonly int VerticalRadiusId = Shader.PropertyToID("_VerticalRadius");
            private static readonly int HaloRadiusId = Shader.PropertyToID("_HaloRadius");
            private static readonly int StreakTintId = Shader.PropertyToID("_StripeTint");
            private static readonly int SunPointId = Shader.PropertyToID("_SunPoint");
            private static readonly int SunSourceId = Shader.PropertyToID("_SunSourceIntensity");
            private static readonly int StreakIntensityId = Shader.PropertyToID("_StreakIntensity");
            private static readonly int HaloIntensityId = Shader.PropertyToID("_HaloIntensity");
            private static readonly int StreakId = Shader.PropertyToID("_DigicamStreakTexture");
            private static readonly int HaloId = Shader.PropertyToID("_DigicamHaloTexture");

            private readonly Material material;
            private readonly bool compositeOnly;

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public Texture kernel;
                public Vector2 sourceSize;
                public Vector4 sunPoint;
                public Color tint;
                public float threshold;
                public float softKnee;
                public float verticalRadius;
                public float haloRadius;
                public float sunSourceIntensity;
                public float streakIntensity;
                public float haloIntensity;
            }

            public BloomPass(Material material, bool compositeOnly)
            {
                this.material = material;
                this.compositeOnly = compositeOnly;
                renderPassEvent = compositeOnly
                    ? RenderPassEvent.AfterRenderingPostProcessing
                    : RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
                ConfigureInput(compositeOnly
                    ? ScriptableRenderPassInput.Color
                    : ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var settings = VolumeManager.instance.stack.GetComponent<DirectionalBloomVolume>();
                if (settings == null || !settings.IsActive() || resources.isActiveTargetBackBuffer) return;

                var source = resources.activeColorTexture;
                var full = cameraData.cameraTargetDescriptor;
                full.msaaSamples = 1;
                full.depthBufferBits = 0;

                if (compositeOnly)
                {
                    var combined = UniversalRenderer.CreateRenderGraphTexture(renderGraph, full, "Digicam Bloom Combined", false);
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Bloom Composite", out var data))
                    {
                        data.source = source;
                        data.material = material;
                        data.streakIntensity = settings.intensity.value;
                        data.haloIntensity = settings.haloIntensity.value;
                        builder.UseTexture(source);
                        builder.UseGlobalTexture(StreakId, AccessFlags.Read);
                        builder.UseGlobalTexture(HaloId, AccessFlags.Read);
                        builder.SetRenderAttachment(combined, 0);
                        builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                        {
                            d.material.SetFloat(StreakIntensityId, d.streakIntensity);
                            d.material.SetFloat(HaloIntensityId, d.haloIntensity);
                            Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 4);
                        });
                    }
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Bloom Copy", out var data))
                    {
                        data.source = combined;
                        builder.UseTexture(combined);
                        builder.SetRenderAttachment(source, 0);
                        builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                            Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), 0, false));
                    }
                    return;
                }

                // HDR storage is necessary: the bright pass and sun source must not clamp to 1.
                var small = full;
                small.width = Mathf.Max(1, full.width / 8);
                small.height = Mathf.Max(1, full.height / 8);
                small.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;

                var bright = UniversalRenderer.CreateRenderGraphTexture(renderGraph, small, "Digicam Bloom Highlights", false, FilterMode.Bilinear);
                var streak = UniversalRenderer.CreateRenderGraphTexture(renderGraph, small, "Digicam Bloom PSF", false, FilterMode.Bilinear);
                var haloHorizontal = UniversalRenderer.CreateRenderGraphTexture(renderGraph, small, "Digicam Bloom Halo H", false, FilterMode.Bilinear);
                var halo = UniversalRenderer.CreateRenderGraphTexture(renderGraph, small, "Digicam Bloom Halo", false, FilterMode.Bilinear);
                var sourceSize = new Vector2(full.width, full.height);

                var sunPoint = Vector4.zero;
                var sun = RenderSettings.sun;
                if (sun != null && sun.enabled && sun.gameObject.activeInHierarchy)
                {
                    var camera = cameraData.camera;
                    var viewport = camera.WorldToViewportPoint(camera.transform.position - sun.transform.forward * 1000f);
                    if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
                        sunPoint = new Vector4(viewport.x, viewport.y, 1f, 0f);
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Bloom Extract", out var data))
                {
                    data.source = source;
                    data.material = material;
                    data.sourceSize = sourceSize;
                    data.sunPoint = sunPoint;
                    data.threshold = settings.threshold.value;
                    data.softKnee = settings.softKnee.value;
                    data.sunSourceIntensity = settings.sunStreakIntensity.value;
                    builder.UseTexture(source);
                    builder.SetRenderAttachment(bright, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        d.material.SetVector(SourceSizeId, d.sourceSize);
                        d.material.SetVector(SunPointId, d.sunPoint);
                        d.material.SetFloat(ThresholdId, d.threshold);
                        d.material.SetFloat(SoftKneeId, d.softKnee);
                        d.material.SetFloat(SunSourceId, d.sunSourceIntensity);
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Bloom 2D Convolution", out var data))
                {
                    data.source = bright;
                    data.material = material;
                    data.kernel = settings.kernel.value;
                    data.sourceSize = sourceSize;
                    data.verticalRadius = settings.verticalRadius.value;
                    data.tint = settings.stripeTint.value;
                    builder.UseTexture(bright);
                    builder.SetRenderAttachment(streak, 0);
                    builder.SetGlobalTextureAfterPass(streak, StreakId);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        d.material.SetTexture(KernelId, d.kernel);
                        d.material.SetVector(SourceSizeId, d.sourceSize);
                        d.material.SetFloat(VerticalRadiusId, d.verticalRadius);
                        d.material.SetColor(StreakTintId, d.tint);
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 1);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Bloom Halo Horizontal", out var data))
                {
                    data.source = bright;
                    data.material = material;
                    data.sourceSize = sourceSize;
                    data.haloRadius = settings.haloRadius.value;
                    builder.UseTexture(bright);
                    builder.SetRenderAttachment(haloHorizontal, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        d.material.SetVector(SourceSizeId, d.sourceSize);
                        d.material.SetFloat(HaloRadiusId, d.haloRadius);
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 2);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Digicam Bloom Halo Vertical", out var data))
                {
                    data.source = haloHorizontal;
                    data.material = material;
                    data.sourceSize = sourceSize;
                    data.haloRadius = settings.haloRadius.value;
                    builder.UseTexture(haloHorizontal);
                    builder.SetRenderAttachment(halo, 0);
                    builder.SetGlobalTextureAfterPass(halo, HaloId);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        d.material.SetVector(SourceSizeId, d.sourceSize);
                        d.material.SetFloat(HaloRadiusId, d.haloRadius);
                        Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 3);
                    });
                }
            }
        }
    }
}
