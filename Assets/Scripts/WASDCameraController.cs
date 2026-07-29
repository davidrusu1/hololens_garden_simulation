using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class WASDCameraController : MonoBehaviour
{
    [SerializeField, Min(0.1f)]
    private float moveSpeed = 3f;

    [SerializeField, Min(1f)]
    private float sprintMultiplier = 2.5f;

    [SerializeField, Min(0.1f)]
    private float mouseSensitivity = 2f;

    private float yaw;
    private float pitch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToMainCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = FindObjectOfType<Camera>();
        }

        if (camera != null
            && camera.GetComponent<WASDCameraController>() == null)
        {
            camera.gameObject.AddComponent<WASDCameraController>();
        }
    }

    private void Awake()
    {
        Vector3 rotation = transform.eulerAngles;
        yaw = rotation.y;
        pitch = NormalizeAngle(rotation.x);
    }

    private void Update()
    {
        MoveCamera();
        RotateCameraWhileRightMouseIsHeld();
    }

    private void MoveCamera()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f
            ? forward.normalized
            : Vector3.forward;

        Vector3 right = transform.right;
        right.y = 0f;
        right = right.sqrMagnitude > 0.0001f
            ? right.normalized
            : Vector3.right;

        Vector3 direction = Vector3.zero;
        if (keyboard.wKey.isPressed) direction += forward;
        if (keyboard.sKey.isPressed) direction -= forward;
        if (keyboard.dKey.isPressed) direction += right;
        if (keyboard.aKey.isPressed) direction -= right;
        if (keyboard.upArrowKey.isPressed) direction += Vector3.up;
        if (keyboard.downArrowKey.isPressed) direction -= Vector3.up;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float speed = keyboard.leftShiftKey.isPressed
            ? moveSpeed * sprintMultiplier
            : moveSpeed;
        transform.position += direction.normalized * speed * Time.deltaTime;
    }

    private void RotateCameraWhileRightMouseIsHeld()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.isPressed)
        {
            return;
        }

        Vector2 mouseDelta = mouse.delta.ReadValue();
        yaw += mouseDelta.x * mouseSensitivity * 0.1f;
        pitch -= mouseDelta.y * mouseSensitivity * 0.1f;
        pitch = Mathf.Clamp(pitch, -85f, 85f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
