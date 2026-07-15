using UnityEngine;

namespace ICI.PlantGrowth
{
    [DisallowMultipleComponent]
    public sealed class WASDCameraController : MonoBehaviour
    {
        [SerializeField, Min(0.01f)]
        private float moveSpeed = 2.5f;

        [SerializeField, Min(0.01f)]
        private float fastMoveMultiplier = 3f;

        [SerializeField, Min(0.01f)]
        private float mouseSensitivity = 2f;

        [SerializeField]
        private bool lockCursorWhilePlaying = true;

        private float pitch;
        private float yaw;

        private void Start()
        {
            Vector3 eulerAngles = transform.eulerAngles;
            pitch = NormalizeAngle(eulerAngles.x);
            yaw = eulerAngles.y;

            if (lockCursorWhilePlaying)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (lockCursorWhilePlaying)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            RotateFromMouse();
            MoveFromKeyboard();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void RotateFromMouse()
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -85f, 85f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void MoveFromKeyboard()
        {
            Vector3 direction = Vector3.zero;

            if (Input.GetKey(KeyCode.W))
            {
                direction += transform.forward;
            }

            if (Input.GetKey(KeyCode.S))
            {
                direction -= transform.forward;
            }

            if (Input.GetKey(KeyCode.D))
            {
                direction += transform.right;
            }

            if (Input.GetKey(KeyCode.A))
            {
                direction -= transform.right;
            }

            if (Input.GetKey(KeyCode.UpArrow))
            {
                direction += Vector3.up;
            }

            if (Input.GetKey(KeyCode.DownArrow))
            {
                direction -= Vector3.up;
            }

            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            float speed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * fastMoveMultiplier : moveSpeed;
            transform.position += direction.normalized * speed * Time.deltaTime;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
