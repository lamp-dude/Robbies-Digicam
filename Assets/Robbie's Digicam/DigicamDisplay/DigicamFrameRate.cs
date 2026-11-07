using UnityEngine;

namespace Memories.Rendering
{
    [DisallowMultipleComponent]
    public sealed class DigicamFrameRate : MonoBehaviour
    {
        private void Awake()
        {
            // vSync overrides targetFrameRate on desktop, so both settings matter.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;
        }
    }
}
