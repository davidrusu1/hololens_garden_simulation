using ICI.PlantGrowth;
using UnityEditor;
using UnityEngine;

namespace ICI.PlantGrowth.Editor
{
    public static class AddWASDCameraController
    {
        [MenuItem("Tools/Plant AR/Add WASD Camera Movement")]
        public static void AddToMainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("No Main Camera found. Select a camera and add WASDCameraController manually.");
                return;
            }

            if (mainCamera.GetComponent<WASDCameraController>() == null)
            {
                Undo.AddComponent<WASDCameraController>(mainCamera.gameObject);
            }

            Selection.activeGameObject = mainCamera.gameObject;
            EditorUtility.SetDirty(mainCamera.gameObject);
            Debug.Log("WASD Camera Movement added to Main Camera.", mainCamera.gameObject);
        }
    }
}
