using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Memories.Rendering
{
    [Serializable]
    [VolumeComponentMenu("Post-processing/CCD Color Noise")]
    public sealed class CCDColorNoiseVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Overall sensor noise strength.")]
        public ClampedFloatParameter intensity = new(0.08f, 0f, 0.5f);

        [Tooltip("Amount of independent red, green, and blue noise.")]
        public ClampedFloatParameter chroma = new(1f, 0f, 2f);

        [Tooltip("Extra noise in dark parts of the image.")]
        public ClampedFloatParameter shadowBoost = new(1.5f, 0f, 4f);

        [Tooltip("Approximate size of a colored noise cell in screen pixels.")]
        public ClampedFloatParameter pixelSize = new(1.5f, 1f, 4f);

        [Tooltip("How much of the noise changes each frame. Zero keeps a fixed sensor pattern.")]
        public ClampedFloatParameter temporalBlend = new(0.8f, 0f, 1f);

        public bool IsActive() => active && intensity.value > 0f;
        public bool IsTileCompatible() => false;
    }
}
