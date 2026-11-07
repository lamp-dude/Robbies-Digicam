using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Memories.Rendering
{
    [Serializable]
    [VolumeComponentMenu("Post-processing/Digicam Convolution Bloom")]
    public sealed class DirectionalBloomVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Point-spread texture for the vertical purple glint.")]
        public TextureParameter kernel = new(null);

        [Tooltip("Brightness threshold for highlights that produce streaks.")]
        public ClampedFloatParameter threshold = new(0.35f, 0f, 8f);

        [Tooltip("Softens the highlight threshold.")]
        public ClampedFloatParameter softKnee = new(0.1f, 0.001f, 2f);

        [Tooltip("Strength of the kernel-shaped streak added to the image.")]
        public ClampedFloatParameter intensity = new(1f, 0f, 64f);

        [Tooltip("Strength of the soft surrounding bloom.")]
        public ClampedFloatParameter haloIntensity = new(0.05f, 0f, 8f);

        [Tooltip("Reach of the surrounding bloom in screen pixels.")]
        public ClampedFloatParameter haloRadius = new(80f, 0f, 400f);

        [Tooltip("Vertical reach of the narrow central stripe in screen pixels.")]
        public ClampedFloatParameter verticalRadius = new(700f, 0f, 1400f);

        [Tooltip("Purple hue of the central stripe.")]
        public ColorParameter stripeTint = new(new Color(0.8f, 0.08f, 1f));

        [Tooltip("HDR sun disc brightness added to the highlight image before convolution.")]
        public ClampedFloatParameter sunStreakIntensity = new(25f, 0f, 1000f);

        public bool IsActive() => active && kernel.value != null && (intensity.value > 0f || haloIntensity.value > 0f);
        public bool IsTileCompatible() => false;
    }
}
