using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Memories.Rendering
{
    [Serializable]
    [VolumeComponentMenu("Post-processing/Digicam Washed Range")]
    public sealed class DigicamRangeVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Output black floor. Raise this to fade shadows toward grey.")]
        public ClampedFloatParameter low = new(0.11f, 0f, 1f);

        [Tooltip("Input white clip point. Lower this to blow out bright areas, including the sky.")]
        public ClampedFloatParameter high = new(0.72f, 0.01f, 1f);

        [Tooltip("Blend between the original image and the washed range.")]
        public ClampedFloatParameter strength = new(1f, 0f, 1f);

        public bool IsActive() => active && strength.value > 0f;
        public bool IsTileCompatible() => false;
    }
}
