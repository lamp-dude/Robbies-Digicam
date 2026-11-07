using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Memories.Rendering
{
    /// <summary>Mouse-wheel digital zoom and center-ray autofocus for the existing game camera.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class DigicamCameraControls : MonoBehaviour
    {
        [Header("Digital zoom")]
        [SerializeField] private float zoomStep = 0.25f;
        [SerializeField] private float maxZoom = 3f;
        [SerializeField] private float zoomResponse = 12f;

        [Header("Autofocus")]
        [SerializeField] private float minimumFocusDistance = 0.25f;
        [SerializeField] private float fallbackFocusDistance = 16f;
        [SerializeField] private float maximumFocusDistance = 60f;
        [SerializeField] private float focusResponse = 10f;

        private readonly RaycastHit[] focusHits = new RaycastHit[16];
        private Camera cameraComponent;
        private DigitalZoomVolume digitalZoom;
        private DepthOfField depthOfField;
        private float targetZoom = 1f;
        private float currentFocusDistance;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            Volume volume = FindFirstObjectByType<Volume>();
            if (volume == null)
            {
                Debug.LogWarning("Digicam autofocus needs a scene Volume.");
                enabled = false;
                return;
            }

            VolumeProfile profile = volume.profile;
            if (!profile.TryGet(out digitalZoom))
                digitalZoom = profile.Add<DigitalZoomVolume>(true);
            digitalZoom.zoom.overrideState = true;

            if (!profile.TryGet(out depthOfField))
                depthOfField = profile.Add<DepthOfField>(true);
            ConfigureDepthOfField(depthOfField);
            currentFocusDistance = depthOfField.focusDistance.value;
        }

        private void Update()
        {
            UpdateZoom();
            UpdateFocus();
        }

        private void UpdateZoom()
        {
            if (Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                    targetZoom = Mathf.Clamp(targetZoom + Mathf.Sign(wheel) * zoomStep, 1f, maxZoom);
            }

            digitalZoom.zoom.value = Mathf.Lerp(digitalZoom.zoom.value, targetZoom, 1f - Mathf.Exp(-zoomResponse * Time.deltaTime));
        }

        private void UpdateFocus()
        {
            Ray ray = cameraComponent.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float targetDistance = fallbackFocusDistance;
            int hitCount = Physics.RaycastNonAlloc(ray, focusHits, maximumFocusDistance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Transform playerRoot = transform.root;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = focusHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(playerRoot) || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
            }

            if (nearest < float.PositiveInfinity)
                targetDistance = Mathf.Max(minimumFocusDistance, nearest);

            currentFocusDistance = Mathf.Lerp(currentFocusDistance, targetDistance, 1f - Mathf.Exp(-focusResponse * Time.deltaTime));
            depthOfField.focusDistance.value = currentFocusDistance;
        }

        private static void ConfigureDepthOfField(DepthOfField dof)
        {
            dof.active = true;
            dof.mode.overrideState = true;
            dof.mode.value = DepthOfFieldMode.Bokeh;
            dof.focusDistance.overrideState = true;
            dof.focalLength.overrideState = true;
            dof.focalLength.value = 45f;
            dof.aperture.overrideState = true;
            dof.aperture.value = 2.8f;
        }
    }
}
