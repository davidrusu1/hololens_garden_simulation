using UnityEngine;
using OpenCvSharp;
using OpenCvSharp.Aruco;

public class ArucoDetector : MonoBehaviour
{
    [Header("Planta care apare pe marker")]
    public GameObject plantPrefab;

    private Dictionary dictionary;
    private DetectorParameters detectorParameters;
    private GameObject spawnedPlant;

    void Start()
    {
        // Initializam dicționarul ArUco 6x6
        dictionary = CvAruco.GetPredefinedDictionary(PredefinedDictionaryName.Dict6X6_250);
        detectorParameters = DetectorParameters.Create();
    }

    // Aceasta metoda va fi apelata cu un frame de la camera HoloLens
    public void ProcessFrame(Texture2D frame)
    {
        // Convertim textura in Mat OpenCV
        Mat mat = OpenCvSharp.Unity.TextureToMat(frame);

        // Facem imaginea grayscale
        Mat grayMat = new Mat();
        Cv2.CvtColor(mat, grayMat, ColorConversionCodes.BGR2GRAY);

        // Detectam markerii
        Point2f[][] corners;
        int[] ids;
        Point2f[][] rejected;

        CvAruco.DetectMarkers(grayMat, dictionary, out corners, out ids, 
                              detectorParameters, out rejected);

        // Daca am gasit cel putin un marker
        if (ids != null && ids.Length > 0)
        {
            Debug.Log("Marker detectat! ID: " + ids[0]);

            // Plasam planta daca nu e deja plasata
            if (spawnedPlant == null && plantPrefab != null)
            {
                spawnedPlant = Instantiate(plantPrefab, Vector3.zero, Quaternion.identity);
            }
        }
    }
}