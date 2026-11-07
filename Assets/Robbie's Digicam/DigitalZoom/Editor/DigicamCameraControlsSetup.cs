using Memories.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class DigicamCameraControlsSetup
{
    [MenuItem("Tools/Digicam/Add Digital Zoom and Autofocus")]
    public static void AddControls()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("No Main Camera was found.");
            return;
        }

        if (camera.GetComponent<DigicamCameraControls>() == null)
            camera.gameObject.AddComponent<DigicamCameraControls>();

        InstallRendererFeature();

        Volume volume = Object.FindFirstObjectByType<Volume>();
        if (volume == null || volume.sharedProfile == null)
        {
            Debug.LogError("A Global Volume with a profile is required for autofocus.");
            return;
        }

        if (!volume.sharedProfile.TryGet(out DigitalZoomVolume zoom))
            zoom = volume.sharedProfile.Add<DigitalZoomVolume>(true);
        zoom.zoom.overrideState = true;
        zoom.zoom.value = 1f;

        if (!volume.sharedProfile.TryGet(out DepthOfField dof))
            dof = volume.sharedProfile.Add<DepthOfField>(true);
        dof.active = true;
        dof.mode.overrideState = true;
        dof.mode.value = DepthOfFieldMode.Bokeh;
        dof.focusDistance.overrideState = true;
        dof.focusDistance.value = 16f;
        dof.focalLength.overrideState = true;
        dof.focalLength.value = 45f;
        dof.aperture.overrideState = true;
        dof.aperture.value = 2.8f;

        EditorUtility.SetDirty(volume.sharedProfile);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        EditorSceneManager.SaveScene(camera.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = camera.gameObject;
        Debug.Log("Added digital zoom and autofocus to the Main Camera. Use mouse wheel for 1x–3x digital zoom.");
    }

    private static void InstallRendererFeature()
    {
        const string rendererPath = "Assets/Settings/PC_Renderer.asset";
        ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
        if (rendererData == null)
        {
            Debug.LogError("Could not find the PC URP renderer data at " + rendererPath);
            return;
        }

        foreach (ScriptableRendererFeature existing in rendererData.rendererFeatures)
        {
            if (existing is DigitalZoomRendererFeature)
                return;
        }

        DigitalZoomRendererFeature feature = ScriptableObject.CreateInstance<DigitalZoomRendererFeature>();
        feature.name = "Digicam Digital Zoom";
        AssetDatabase.AddObjectToAsset(feature, rendererData);
        rendererData.rendererFeatures.Add(feature);
        EditorUtility.SetDirty(rendererData);
    }
}
