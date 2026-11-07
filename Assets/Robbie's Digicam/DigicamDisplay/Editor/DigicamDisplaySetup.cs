using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Memories.Rendering.Editor
{
    public static class DigicamDisplaySetup
    {
        [MenuItem("Tools/Digicam/Set Up 640x480 Display")]
        public static void Run()
        {
            var camera = Camera.main;
            if (camera == null)
                throw new System.InvalidOperationException("The scene needs a Main Camera.");

            const string texturePath = "Assets/Robbie's Digicam/DigicamDisplay/Digicam640x480.renderTexture";
            var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
            if (texture == null)
            {
                texture = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGBHalf)
                {
                    name = "Digicam 640x480 HDR",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                AssetDatabase.CreateAsset(texture, texturePath);
            }

            camera.allowHDR = true;
            camera.allowDynamicResolution = false;
            camera.targetTexture = texture;
            if (camera.GetComponent<DigicamFrameRate>() == null)
                camera.gameObject.AddComponent<DigicamFrameRate>();

            var old = GameObject.Find("Digicam Screen");
            if (old != null)
                Object.DestroyImmediate(old);

            var root = new GameObject("Digicam Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var bars = new GameObject("Black Bars", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bars.transform.SetParent(root.transform, false);
            var barsRect = bars.GetComponent<RectTransform>();
            barsRect.anchorMin = Vector2.zero;
            barsRect.anchorMax = Vector2.one;
            barsRect.offsetMin = Vector2.zero;
            barsRect.offsetMax = Vector2.zero;
            var barsImage = bars.GetComponent<Image>();
            barsImage.color = Color.black;
            barsImage.raycastTarget = false;

            var picture = new GameObject("640x480 Picture", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage), typeof(AspectRatioFitter));
            picture.transform.SetParent(bars.transform, false);
            var rect = picture.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            var raw = picture.GetComponent<RawImage>();
            raw.texture = texture;
            raw.color = Color.white;
            raw.raycastTarget = false;
            var fitter = picture.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 4f / 3f;

            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            EditorSceneManager.SaveScene(camera.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
    }
}
