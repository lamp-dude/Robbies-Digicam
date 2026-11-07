using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FirstPersonPlayerSetup
{
    private const float EyeHeight = 1.6f;

    [MenuItem("Tools/Digicam/Create First Person Player")]
    public static void CreateOrUpdate()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("First Person Player setup needs a MainCamera-tagged camera.");
            return;
        }

        GameObject player = GameObject.Find("First Person Player");
        if (player == null)
            player = new GameObject("First Person Player");

        Vector3 originalCameraPosition = mainCamera.transform.position;
        float cameraPitch = NormalizeAngle(mainCamera.transform.eulerAngles.x);
        float cameraYaw = mainCamera.transform.eulerAngles.y;

        // Put the capsule feet on the first floor below the existing viewpoint.
        Vector3 playerPosition = originalCameraPosition - Vector3.up * EyeHeight;
        if (Physics.Raycast(originalCameraPosition, Vector3.down, out RaycastHit floorHit, 20f, ~0, QueryTriggerInteraction.Ignore))
            playerPosition.y = floorHit.point.y + 0.02f;

        player.transform.SetPositionAndRotation(playerPosition, Quaternion.Euler(0f, cameraYaw, 0f));

        CharacterController capsule = player.GetComponent<CharacterController>();
        if (capsule == null)
            capsule = player.AddComponent<CharacterController>();
        capsule.height = 1.8f;
        capsule.radius = 0.3f;
        capsule.center = new Vector3(0f, 0.9f, 0f);
        capsule.skinWidth = 0.03f;
        capsule.stepOffset = 0.3f;
        capsule.slopeLimit = 50f;

        mainCamera.transform.SetParent(player.transform, false);
        mainCamera.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
        mainCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);

        FirstPersonController controller = player.GetComponent<FirstPersonController>();
        if (controller == null)
            controller = player.AddComponent<FirstPersonController>();
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("playerCamera").objectReferenceValue = mainCamera;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
        Selection.activeGameObject = player;
        Debug.Log("Created First Person Player: invisible CharacterController capsule with the existing Main Camera as its child.");
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
