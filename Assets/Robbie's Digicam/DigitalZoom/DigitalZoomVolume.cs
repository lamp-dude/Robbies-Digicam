using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Memories.Rendering
{
    [Serializable]
    [VolumeComponentMenu("Post-processing/Digicam Digital Zoom")]
    public sealed class DigitalZoomVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Image-space crop magnification. 1 is normal; values above 1 digitally enlarge the center of the frame.")]
        public ClampedFloatParameter zoom = new(1f, 1f, 4f);

        public bool IsActive() => active && zoom.value > 1.001f;
        public bool IsTileCompatible() => false;
    }
}
