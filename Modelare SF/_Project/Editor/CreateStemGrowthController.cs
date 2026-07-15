using ICI.PlantGrowth;
using UnityEditor;
using UnityEngine;

namespace ICI.PlantGrowth.Editor
{
    public static class CreateStemGrowthController
    {
        private const string PrefabFolder = "Assets/_Project/TEST/Prefabs";
        private const string ArucoAnchorPrefabPath = "Assets/_Project/TEST/Prefabs/Aruco anchor.prefab";

        private static readonly string[] StemPrefabPaths =
        {
            $"{PrefabFolder}/TEST_SunflowerStem_01_Incipient.prefab",
            $"{PrefabFolder}/TEST_SunflowerStem_02_Young.prefab",
            $"{PrefabFolder}/TEST_SunflowerStem_03_Mature.prefab",
            $"{PrefabFolder}/TEST_SunflowerStem_04_Uniform90.prefab"
        };

        [MenuItem("Tools/Plant AR/Create Stem Growth Controller")]
        public static void CreateController()
        {
            Transform selectedParent = Selection.activeTransform;
            var controllerObject = new GameObject("Sunflower Stem Growth");
            Undo.RegisterCreatedObjectUndo(controllerObject, "Create Stem Growth Controller");

            if (selectedParent != null)
            {
                Undo.SetTransformParent(controllerObject.transform, selectedParent, "Parent Stem Growth Controller");
                controllerObject.transform.localPosition = Vector3.zero;
                controllerObject.transform.localRotation = Quaternion.identity;
                controllerObject.transform.localScale = Vector3.one;
            }

            StemGrowthController controller = Undo.AddComponent<StemGrowthController>(controllerObject);
            var serializedController = new SerializedObject(controller);
            SerializedProperty arucoAnchorPrefab = serializedController.FindProperty("arucoAnchorPrefab");
            SerializedProperty prefabs = serializedController.FindProperty("stagePrefabs");

            GameObject anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArucoAnchorPrefabPath);
            arucoAnchorPrefab.objectReferenceValue = anchorPrefab;

            if (anchorPrefab == null)
            {
                Debug.LogWarning($"Aruco anchor prefab not found: {ArucoAnchorPrefabPath}");
            }

            prefabs.arraySize = StemPrefabPaths.Length;
            for (int index = 0; index < StemPrefabPaths.Length; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StemPrefabPaths[index]);
                prefabs.GetArrayElementAtIndex(index).objectReferenceValue = prefab;

                if (prefab == null)
                {
                    Debug.LogWarning($"Stem prefab not found: {StemPrefabPaths[index]}");
                }
            }

            serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            Selection.activeGameObject = controllerObject;

            Debug.Log("Stem Growth Controller created with stages 1-4. Press Play to test it.", controllerObject);
        }
    }
}
