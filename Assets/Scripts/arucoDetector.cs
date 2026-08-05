using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows.WebCam;
using OpenCvSharp;
using OpenCvSharp.Aruco;

public class HoloLensArucoManager : MonoBehaviour
{
    [Serializable]
    public class MarkerObject
    {
        public int markerId;
        public GameObject markerPrefab;
    }

    [Header("Configurare Markere")]
    public List<MarkerObject> markers;
    public float markerLengthMeters = 0.097f;

    private Dictionary<int, GameObject> spawnedObjects = new Dictionary<int, GameObject>();
    private PhotoCapture photoCaptureObject = null;
    private Resolution cameraResolution;

    private OpenCvSharp.Aruco.Dictionary dictionary;
    private DetectorParameters detectorParameters;

    void Start()
    {
        dictionary = CvAruco.GetPredefinedDictionary(PredefinedDictionaryName.Dict6X6_250);
        detectorParameters = DetectorParameters.Create();

        var resolutions = PhotoCapture.SupportedResolutions.ToList();
        cameraResolution = resolutions.OrderByDescending(res => res.width).First();

        PhotoCapture.CreateAsync(false, captureObject => {
            photoCaptureObject = captureObject;
            CameraParameters c = new CameraParameters();
            c.hologramOpacity = 0.0f;
            c.cameraResolutionWidth = cameraResolution.width;
            c.cameraResolutionHeight = cameraResolution.height;
            c.pixelFormat = CapturePixelFormat.BGRA32;

            photoCaptureObject.StartPhotoModeAsync(c, result => {
                InvokeRepeating(nameof(CapturePhotoFrame), 0f, 0.2f);
            });
        });
    }

    void CapturePhotoFrame()
    {
        if (photoCaptureObject == null) return;

        photoCaptureObject.TakePhotoAsync((result, frame) => {
            if (!result.success) return;

            List<byte> buffer = new List<byte>();
            frame.CopyRawImageDataIntoBuffer(buffer);

            ProcessOpenCVFrame(buffer.ToArray(), cameraResolution.width, cameraResolution.height);
        });
    }

    void ProcessOpenCVFrame(byte[] data, int width, int height)
    {
        using (Mat rawMat = new Mat(height, width, MatType.CV_8UC4, data))
        using (Mat grayMat = new Mat())
        {
            Cv2.CvtColor(rawMat, grayMat, ColorConversionCodes.BGRA2GRAY);

            Point2f[][] corners;
            int[] ids;
            Point2f[][] rejected;

            CvAruco.DetectMarkers(grayMat, dictionary, out corners, out ids, detectorParameters, out rejected);

            List<int> visibleMarkers = new List<int>();

            if (ids != null && ids.Length > 0)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    int currentId = ids[i];
                    visibleMarkers.Add(currentId);

                    MarkerObject matchedMarker = markers.Find(m => m.markerId == currentId);

                    if (matchedMarker != null && matchedMarker.markerPrefab != null)
                    {
                        // Calculăm centrul markerului direct din cele 4 colțuri detectate în 2D
                        Point2f c1 = corners[i][0];
                        Point2f c2 = corners[i][1];
                        Point2f c3 = corners[i][2];
                        Point2f c4 = corners[i][3];

                        float centerXPixel = (c1.X + c2.X + c3.X + c4.X) / 4.0f;
                        float centerYPixel = (c1.Y + c2.Y + c3.Y + c4.Y) / 4.0f;

                        // Estimare simplificată a poziției relative față de cameră
                        float xPos = (centerXPixel - (width / 2.0f)) / width * 0.5f;
                        float yPos = -((centerYPixel - (height / 2.0f)) / height * 0.5f);
                        float zPos = 0.5f; // Distanța estimată de 50cm față de ochelari

                        Vector3 localPosition = new Vector3(xPos, yPos, zPos);

                        if (!spawnedObjects.ContainsKey(currentId))
                        {
                            GameObject newPlant = Instantiate(matchedMarker.markerPrefab);
                            spawnedObjects.Add(currentId, newPlant);
                        }

                        GameObject objToMove = spawnedObjects[currentId];
                        if (!objToMove.activeSelf) objToMove.SetActive(true);

                        if (Camera.main != null)
                        {
                            objToMove.transform.position = Camera.main.transform.TransformPoint(localPosition);
                            objToMove.transform.rotation = Camera.main.transform.rotation;
                        }
                    }
                }
            }

            foreach (var kvp in spawnedObjects)
            {
                if (!visibleMarkers.Contains(kvp.Key))
                {
                    kvp.Value.SetActive(false);
                }
            }
        }
    }

    void OnDestroy()
    {
        CancelInvoke();
        if (photoCaptureObject != null)
        {
            photoCaptureObject.StopPhotoModeAsync(result => {
                photoCaptureObject.Dispose();
                photoCaptureObject = null;
            });
        }
    }
}