using System.Collections.Generic;
using UnityEngine;

#if UNITY_WSA && !UNITY_EDITOR
using UnityEngine.Windows.WebCam;
#endif

namespace ICI.ArucoTracking
{
    /// <summary>
    /// HoloLens Photo/Video camera source. It captures a locatable frame at a
    /// modest rate, leaving the application responsive on HoloLens 2.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArucoPhotoCaptureSource : MonoBehaviour
    {
        private readonly ArucoMarkerDetector detector =
            new ArucoMarkerDetector();
        private ArucoPlantAnchor owner;

#if UNITY_WSA && !UNITY_EDITOR
        [SerializeField, Range(1f, 10f)]
        private float detectionsPerSecond = 4f;

        [SerializeField, Range(320, 1920)]
        private int preferredCaptureWidth = 896;

        private PhotoCapture photoCapture;
        private readonly List<byte> imageBuffer = new List<byte>(896 * 504 * 4);
        private Resolution captureResolution;
        private bool photoModeRunning;
        private bool captureInFlight;
        private bool shuttingDown;
        private float nextCaptureTime;
#endif

        public void Initialize(ArucoPlantAnchor anchorOwner)
        {
            owner = anchorOwner;
        }

        private void Start()
        {
#if UNITY_WSA && !UNITY_EDITOR
            owner?.ReportCaptureStatus("Pornesc camera HoloLens pentru ArUco");
            PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
#else
            owner?.ReportCaptureStatus(
                "Captura ArUco este activă numai în build-ul HoloLens/UWP");
#endif
        }

        private void Update()
        {
#if UNITY_WSA && !UNITY_EDITOR
            if (!photoModeRunning
                || captureInFlight
                || photoCapture == null
                || Time.unscaledTime < nextCaptureTime)
            {
                return;
            }

            captureInFlight = true;
            nextCaptureTime = Time.unscaledTime
                + 1f / Mathf.Max(1f, detectionsPerSecond);
            photoCapture.TakePhotoAsync(OnCapturedPhotoToMemory);
#endif
        }

        private void OnDestroy()
        {
#if UNITY_WSA && !UNITY_EDITOR
            shuttingDown = true;
            if (photoCapture == null)
            {
                return;
            }

            if (photoModeRunning)
            {
                photoCapture.StopPhotoModeAsync(OnPhotoModeStopped);
            }
            else
            {
                photoCapture.Dispose();
                photoCapture = null;
            }
#endif
        }

#if UNITY_WSA && !UNITY_EDITOR
        private void OnPhotoCaptureCreated(PhotoCapture createdCapture)
        {
            if (shuttingDown || createdCapture == null)
            {
                createdCapture?.Dispose();
                owner?.ReportCaptureStatus("Camera HoloLens nu a putut fi deschisă");
                return;
            }

            photoCapture = createdCapture;
            captureResolution = SelectCaptureResolution();
            if (captureResolution.width <= 0 || captureResolution.height <= 0)
            {
                owner?.ReportCaptureStatus("Camera nu oferă o rezoluție compatibilă");
                photoCapture.Dispose();
                photoCapture = null;
                return;
            }

            var parameters = new CameraParameters
            {
                hologramOpacity = 0f,
                cameraResolutionWidth = captureResolution.width,
                cameraResolutionHeight = captureResolution.height,
                pixelFormat = CapturePixelFormat.BGRA32
            };
            photoCapture.StartPhotoModeAsync(
                parameters,
                OnPhotoModeStarted);
        }

        private Resolution SelectCaptureResolution()
        {
            Resolution selected = default;
            float bestScore = float.PositiveInfinity;
            foreach (Resolution candidate in PhotoCapture.SupportedResolutions)
            {
                float widthDifference = Mathf.Abs(
                    candidate.width - preferredCaptureWidth);
                float aspect = candidate.width
                    / (float)Mathf.Max(1, candidate.height);
                float aspectPenalty = Mathf.Abs(aspect - 16f / 9f) * 220f;
                float score = widthDifference + aspectPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    selected = candidate;
                }
            }
            return selected;
        }

        private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
        {
            if (!result.success || shuttingDown)
            {
                owner?.ReportCaptureStatus(
                    "Camera este indisponibilă; verifică permisiunea WebCam și alte capturi active");
                photoCapture?.Dispose();
                photoCapture = null;
                return;
            }

            photoModeRunning = true;
            nextCaptureTime = Time.unscaledTime;
            owner?.ReportCaptureStatus(
                $"Caut ArUco ID 0 la {captureResolution.width}x{captureResolution.height}");
        }

        private void OnCapturedPhotoToMemory(
            PhotoCapture.PhotoCaptureResult result,
            PhotoCaptureFrame frame)
        {
            captureInFlight = false;
            if (shuttingDown || !result.success || frame == null)
            {
                frame?.Dispose();
                if (!shuttingDown)
                {
                    owner?.ReportCaptureStatus("Nu am putut citi cadrul camerei");
                }
                return;
            }

            try
            {
                if (!frame.hasLocationData
                    || !frame.TryGetCameraToWorldMatrix(
                        out Matrix4x4 cameraToWorld)
                    || !frame.TryGetProjectionMatrix(
                        0.05f,
                        20f,
                        out Matrix4x4 projection))
                {
                    owner?.ReportCaptureStatus(
                        "Cadrul camerei nu conține localizare spațială");
                    return;
                }

                imageBuffer.Clear();
                frame.CopyRawImageDataIntoBuffer(imageBuffer);
                owner?.ProcessCameraFrame(
                    imageBuffer,
                    captureResolution.width,
                    captureResolution.height,
                    cameraToWorld,
                    projection,
                    true,
                    detector);
            }
            finally
            {
                frame.Dispose();
            }
        }

        private void OnPhotoModeStopped(PhotoCapture.PhotoCaptureResult result)
        {
            photoModeRunning = false;
            photoCapture?.Dispose();
            photoCapture = null;
        }
#endif
    }
}
