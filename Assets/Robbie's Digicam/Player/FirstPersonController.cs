using UnityEngine;
using UnityEngine.InputSystem;


//TS USES OLD PLAYER INPUT SYSTEM!!
[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 6.25f;
    [SerializeField] private float jumpHeight = 1.15f;
    [SerializeField] private float gravity = -24f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera != null && playerCamera.GetComponent<ProceduralCameraShake>() == null)
            playerCamera.gameObject.AddComponent<ProceduralCameraShake>();

        if (playerCamera != null)
            pitch = NormalizeAngle(playerCamera.transform.localEulerAngles.x);
    }

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            UnlockCursor();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            LockCursor();

        if (Cursor.lockState == CursorLockMode.Locked)
            Look();

        Move();
    }

    private void Look()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouseDelta.y, minPitch, maxPitch);
        transform.Rotate(Vector3.up * mouseDelta.x, Space.Self);

        if (playerCamera != null)
        {
            Quaternion aimRotation = Quaternion.Euler(pitch, 0f, 0f);
            playerCamera.transform.localRotation = aimRotation;
            playerCamera.GetComponent<ProceduralCameraShake>()?.SetAimRotation(aimRotation);
        }
    }

    private void Move()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (controller.isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        float speed = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed
            ? sprintSpeed
            : walkSpeed;
        Vector3 planarMovement = (transform.right * input.x + transform.forward * input.y) * speed;
        controller.Move((planarMovement + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
