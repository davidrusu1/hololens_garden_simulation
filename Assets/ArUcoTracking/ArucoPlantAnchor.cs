using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ICI.ArucoTracking
{
    /// <summary>
    /// Places the existing garden content on a physical ArUco marker.
    /// The component is created automatically in a HoloLens/UWP player and does
    /// not require a scene edit.
    /// </summary>
    [DefaultExecutionOrder(3000)]
    [DisallowMultipleComponent]
    public sealed class ArucoPlantAnchor : MonoBehaviour
    {
        [Header("Physical marker")]
        [SerializeField, Min(0.02f)]
        private float markerSideLengthMeters = 0.08f;

        [SerializeField]
        private int expectedMarkerId;

        [Header("Plant content")]
        [SerializeField]
        [Tooltip("Optional explicit target. Auto mode prefers Garden Content, then Plant_AR_Root.")]
        private Transform contentToAnchor;

        [SerializeField]
        [Tooltip("Offset from marker center in marker-local metres: X=right, Y=normal/up, Z=printed top.")]
        private Vector3 markerToContentOffsetMeters = Vector3.zero;

        [SerializeField]
        private bool hideContentUntilFirstDetection;

        [Header("Tracking stability")]
        [SerializeField, Range(0.05f, 1f)]
        private float positionSmoothing = 0.36f;

        [SerializeField, Range(0.05f, 1f)]
        private float rotationSmoothing = 0.28f;

        [SerializeField, Min(0.2f)]
        private float markerLostTimeoutSeconds = 1.25f;

        [SerializeField]
        private bool keepLastPoseWhenMarkerIsLost = true;

        private ArucoPhotoCaptureSource captureSource;
        private bool hasPose;
        private bool markerVisible;
        private float lastDetectionTime = float.NegativeInfinity;
        private Vector3 filteredPosition;
        private Quaternion filteredRotation = Quaternion.identity;
        private string lastLoggedStatus;

        public bool HasPose => hasPose;
        public bool MarkerVisible => markerVisible;
        public float LastDetectionTime => lastDetectionTime;
        public string Status { get; private set; } = "Inițializare ArUco";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapOnHoloLens()
        {
#if UNITY_WSA && !UNITY_EDITOR
            if (FindObjectOfType<ArucoPlantAnchor>() != null)
            {
                return;
            }

            GameObject trackerObject = new GameObject("ArUco Plant Anchor");
            DontDestroyOnLoad(trackerObject);
            trackerObject.AddComponent<ArucoPlantAnchor>();
#endif
        }

        private void Awake()
        {
            expectedMarkerId = ArucoMarkerDetector.MarkerId;
            ResolveContentTarget();
            if (hideContentUntilFirstDetection && contentToAnchor != null)
            {
                contentToAnchor.gameObject.SetActive(false);
            }

            captureSource = GetComponent<ArucoPhotoCaptureSource>();
            if (captureSource == null)
            {
                captureSource = gameObject.AddComponent<ArucoPhotoCaptureSource>();
            }
            captureSource.Initialize(this);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            if (markerVisible
                && Time.unscaledTime - lastDetectionTime
                    > markerLostTimeoutSeconds)
            {
                markerVisible = false;
                SetStatus(
                    keepLastPoseWhenMarkerIsLost
                        ? "Marker pierdut; păstrez ultima poziție"
                        : "Marker pierdut");

                if (!keepLastPoseWhenMarkerIsLost
                    && contentToAnchor != null)
                {
                    contentToAnchor.gameObject.SetActive(false);
                }
            }
        }

        internal void ProcessCameraFrame(
            System.Collections.Generic.IReadOnlyList<byte> bgra,
            int width,
            int height,
            Matrix4x4 cameraToWorld,
            Matrix4x4 projection,
            bool reversePixelOrder,
            ArucoMarkerDetector detector)
        {
            if (!detector.TryDetect(
                    bgra,
                    width,
                    height,
                    reversePixelOrder,
                    out ArucoDetection detection))
            {
                return;
            }

            if (detection.MarkerId != expectedMarkerId)
            {
                return;
            }

            if (!TryEstimateWorldPose(
                    detection.Corners,
                    width,
                    height,
                    markerSideLengthMeters,
                    cameraToWorld,
                    projection,
                    out Vector3 position,
                    out Quaternion rotation))
            {
                SetStatus("Marker detectat, dar poziția 3D nu este validă");
                return;
            }

            ApplyPose(position, rotation, detection.Confidence);
        }

        internal void ReportCaptureStatus(string message)
        {
            SetStatus(message);
        }

        [ContextMenu("Simulate Marker In Front Of Camera")]
        public void SimulateMarkerInFrontOfCamera()
        {
            ResolveContentTarget();
            Camera camera = ResolveActiveCamera();
            Vector3 position;
            Quaternion rotation;
            if (camera != null)
            {
                Vector3 horizontalForward = Vector3.ProjectOnPlane(
                    camera.transform.forward,
                    Vector3.up);
                if (horizontalForward.sqrMagnitude < 0.001f)
                {
                    horizontalForward = Vector3.forward;
                }
                horizontalForward.Normalize();
                position = camera.transform.position
                    + horizontalForward * 1.6f
                    - Vector3.up * 0.85f;
                rotation = Quaternion.LookRotation(horizontalForward, Vector3.up);
            }
            else
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
            }

            ApplyPose(position, rotation, 1f);
            SetStatus("Poziție ArUco simulată în Editor");
        }

        private void ApplyPose(
            Vector3 measuredPosition,
            Quaternion measuredRotation,
            float confidence)
        {
            ResolveContentTarget();
            if (contentToAnchor == null)
            {
                SetStatus("Marker găsit, dar conținutul plantei lipsește");
                return;
            }

            if (!hasPose)
            {
                filteredPosition = measuredPosition;
                filteredRotation = measuredRotation;
                hasPose = true;
            }
            else
            {
                float confidenceWeight = Mathf.Lerp(0.55f, 1f, confidence);
                filteredPosition = Vector3.Lerp(
                    filteredPosition,
                    measuredPosition,
                    positionSmoothing * confidenceWeight);
                filteredRotation = Quaternion.Slerp(
                    filteredRotation,
                    measuredRotation,
                    rotationSmoothing * confidenceWeight);
            }

            Vector3 offset = filteredRotation * markerToContentOffsetMeters;
            contentToAnchor.SetPositionAndRotation(
                filteredPosition + offset,
                filteredRotation);
            if (!contentToAnchor.gameObject.activeSelf)
            {
                contentToAnchor.gameObject.SetActive(true);
            }

            markerVisible = true;
            lastDetectionTime = Time.unscaledTime;
            SetStatus("ArUco ID 0 detectat; planta este ancorată");
        }

        private void ResolveContentTarget()
        {
            if (contentToAnchor != null)
            {
                return;
            }

            string[] preferredNames =
            {
                "Garden Content",
                "Plant_AR_Root",
                "Sunflower Simulation",
                "Plant"
            };
            for (int index = 0; index < preferredNames.Length; index++)
            {
                GameObject candidate = GameObject.Find(preferredNames[index]);
                if (candidate != null)
                {
                    contentToAnchor = candidate.transform;
                    return;
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (contentToAnchor == null)
            {
                ResolveContentTarget();
            }
        }

        private void SetStatus(string message)
        {
            Status = message;
            if (lastLoggedStatus == message)
            {
                return;
            }

            lastLoggedStatus = message;
            Debug.Log("[ArUco] " + message, this);
        }

        private static Camera ResolveActiveCamera()
        {
            if (Camera.main != null
                && Camera.main.enabled
                && Camera.main.gameObject.activeInHierarchy)
            {
                return Camera.main;
            }

            Camera[] cameras = FindObjectsOfType<Camera>(true);
            for (int index = 0; index < cameras.Length; index++)
            {
                if (cameras[index].enabled
                    && cameras[index].gameObject.activeInHierarchy)
                {
                    return cameras[index];
                }
            }
            return null;
        }

        private static bool TryEstimateWorldPose(
            Vector2[] corners,
            int imageWidth,
            int imageHeight,
            float markerLength,
            Matrix4x4 cameraToWorld,
            Matrix4x4 projection,
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            if (corners == null || corners.Length != 4 || markerLength <= 0f)
            {
                return false;
            }

            float focalX = Mathf.Abs(projection.m00) * imageWidth * 0.5f;
            float focalY = Mathf.Abs(projection.m11) * imageHeight * 0.5f;
            float centerX = (1f - projection.m02) * imageWidth * 0.5f;
            float centerY = (1f + projection.m12) * imageHeight * 0.5f;
            if (focalX < 1f || focalY < 1f)
            {
                return false;
            }

            float half = markerLength * 0.5f;
            Vector2[] markerPlane =
            {
                new Vector2(-half, -half),
                new Vector2(half, -half),
                new Vector2(half, half),
                new Vector2(-half, half)
            };
            if (!TrySolveHomography(markerPlane, corners, out double[] h))
            {
                return false;
            }

            Vector3 firstColumn = new Vector3(
                (float)((h[0] - centerX * h[6]) / focalX),
                (float)((h[3] - centerY * h[6]) / focalY),
                (float)h[6]);
            Vector3 secondColumn = new Vector3(
                (float)((h[1] - centerX * h[7]) / focalX),
                (float)((h[4] - centerY * h[7]) / focalY),
                (float)h[7]);
            Vector3 translation = new Vector3(
                (float)((h[2] - centerX) / focalX),
                (float)((h[5] - centerY) / focalY),
                1f);

            float scale = 2f /
                Mathf.Max(1e-6f, firstColumn.magnitude + secondColumn.magnitude);
            firstColumn *= scale;
            secondColumn *= scale;
            translation *= scale;
            if (translation.z < 0f)
            {
                firstColumn = -firstColumn;
                secondColumn = -secondColumn;
                translation = -translation;
            }

            Vector3 markerRightCv = firstColumn.normalized;
            Vector3 markerDownCv = (
                secondColumn
                - Vector3.Dot(secondColumn, markerRightCv) * markerRightCv)
                .normalized;
            Vector3 markerNormalCv = Vector3.Cross(
                markerRightCv,
                markerDownCv).normalized;
            if (markerRightCv.sqrMagnitude < 0.9f
                || markerDownCv.sqrMagnitude < 0.9f
                || markerNormalCv.sqrMagnitude < 0.9f
                || translation.z < 0.12f
                || translation.z > 8f)
            {
                return false;
            }

            Vector3 cameraPosition = cameraToWorld.GetColumn(3);
            Vector3 cameraRight = cameraToWorld.GetColumn(0);
            Vector3 cameraUp = cameraToWorld.GetColumn(1);
            Vector3 cameraForward = -cameraToWorld.GetColumn(2);
            Vector3 CvToWorldDirection(Vector3 cvDirection)
            {
                return cameraRight * cvDirection.x
                    - cameraUp * cvDirection.y
                    + cameraForward * cvDirection.z;
            }

            worldPosition = cameraPosition + CvToWorldDirection(translation);
            Vector3 markerRightWorld = CvToWorldDirection(markerRightCv).normalized;
            Vector3 markerDownWorld = CvToWorldDirection(markerDownCv).normalized;
            Vector3 markerNormalWorld = CvToWorldDirection(markerNormalCv).normalized;

            // The physical marker is placed flat in/on the real pot. Choose the
            // face visible to the camera as the plant's local up direction.
            Vector3 markerToCamera = cameraPosition - worldPosition;
            Vector3 plantUp = Vector3.Dot(markerNormalWorld, markerToCamera) >= 0f
                ? markerNormalWorld
                : -markerNormalWorld;
            Vector3 plantForward = Vector3.ProjectOnPlane(
                -markerDownWorld,
                plantUp).normalized;
            if (plantForward.sqrMagnitude < 0.5f)
            {
                plantForward = Vector3.Cross(plantUp, markerRightWorld).normalized;
            }

            worldRotation = Quaternion.LookRotation(plantForward, plantUp);
            return IsFinite(worldPosition) && IsFinite(worldRotation);
        }

        private static bool TrySolveHomography(
            Vector2[] source,
            Vector2[] destination,
            out double[] solution)
        {
            var matrix = new double[8, 9];
            for (int index = 0; index < 4; index++)
            {
                double x = source[index].x;
                double y = source[index].y;
                double u = destination[index].x;
                double v = destination[index].y;
                int row = index * 2;
                matrix[row, 0] = x;
                matrix[row, 1] = y;
                matrix[row, 2] = 1.0;
                matrix[row, 6] = -u * x;
                matrix[row, 7] = -u * y;
                matrix[row, 8] = u;
                matrix[row + 1, 3] = x;
                matrix[row + 1, 4] = y;
                matrix[row + 1, 5] = 1.0;
                matrix[row + 1, 6] = -v * x;
                matrix[row + 1, 7] = -v * y;
                matrix[row + 1, 8] = v;
            }

            const int size = 8;
            solution = new double[size];
            for (int pivot = 0; pivot < size; pivot++)
            {
                int bestRow = pivot;
                double bestValue = Math.Abs(matrix[pivot, pivot]);
                for (int row = pivot + 1; row < size; row++)
                {
                    double value = Math.Abs(matrix[row, pivot]);
                    if (value > bestValue)
                    {
                        bestValue = value;
                        bestRow = row;
                    }
                }
                if (bestValue < 1e-10)
                {
                    return false;
                }

                if (bestRow != pivot)
                {
                    for (int column = pivot; column <= size; column++)
                    {
                        double swap = matrix[pivot, column];
                        matrix[pivot, column] = matrix[bestRow, column];
                        matrix[bestRow, column] = swap;
                    }
                }

                double divisor = matrix[pivot, pivot];
                for (int column = pivot; column <= size; column++)
                {
                    matrix[pivot, column] /= divisor;
                }
                for (int row = 0; row < size; row++)
                {
                    if (row == pivot)
                    {
                        continue;
                    }
                    double factor = matrix[row, pivot];
                    for (int column = pivot; column <= size; column++)
                    {
                        matrix[row, column] -= factor * matrix[pivot, column];
                    }
                }
            }

            for (int row = 0; row < size; row++)
            {
                solution[row] = matrix[row, size];
            }
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x)
                && float.IsFinite(value.y)
                && float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x)
                && float.IsFinite(value.y)
                && float.IsFinite(value.z)
                && float.IsFinite(value.w);
        }
    }
}
