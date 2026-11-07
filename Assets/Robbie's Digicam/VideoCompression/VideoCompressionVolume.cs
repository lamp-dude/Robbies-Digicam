using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Memories.Rendering
{
    [Serializable]
    [VolumeComponentMenu("Post-processing/Digicam Video Compression")]
    public sealed class VideoCompressionVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Overall strength of the simulated low-bitrate video compression.")]
        public ClampedFloatParameter intensity = new(0.65f, 0f, 1f);

        [Tooltip("Size of a simulated compression block in source pixels.")]
        public ClampedIntParameter blockSize = new(8, 4, 32);

        [Tooltip("Visible square block boundaries and loss of fine detail.")]
        public ClampedFloatParameter blockiness = new(0.65f, 0f, 1f);

        [Tooltip("Chroma subsampling and color bleeding, as in compressed video.")]
        public ClampedFloatParameter chromaLoss = new(0.8f, 0f, 1f);

        [Tooltip("Fine crawling artifacts beside high-contrast edges.")]
        public ClampedFloatParameter mosquitoNoise = new(0.4f, 0f, 1f);

        public bool IsActive() => active && intensity.value > 0f;
        public bool IsTileCompatible() => false;
    }
}
