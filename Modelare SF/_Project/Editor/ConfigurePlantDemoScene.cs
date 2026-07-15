using ICI.PlantGrowth;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ICI.PlantGrowth.Editor
{
    public static class ConfigurePlantDemoScene
    {
        private const string MainScenePath = "Assets/_Project/Scenes/01_Main.unity";
        private const string RunRequestPath = "Assets/_Project/Editor/RunPlantDemo.request";
        private const string PrefabFolder = "Assets/_Project/TEST/Prefabs";
        private const string ArucoAnchorPrefabPath = "Assets/_Project/TEST/Prefabs/Aruco anchor.prefab";

        private static readonly string[] LeafPrefabPaths =
        {
            "Assets/_Project/TEST/Prefabs/TEST_SunflowerLeaf_01_Incipient.prefab",
            "Assets/_Project/TEST/Prefabs/TEST_SunflowerLeaf_02_Young.prefab",
            "Assets/_Project/TEST/Prefabs/TEST_SunflowerLeaf_03_Mature.prefab"
        };

        private static readonly string[] FlowerPrefabPaths =
        {
            "Assets/_Project/TEST/Prefabs/TEST_SunflowerFlower_01_Incipient.prefab",
            "Assets/_Project/TEST/Prefabs/TEST_SunflowerFlower_02_Young.prefab",
            "Assets/_Project/TEST/Prefabs/TEST_SunflowerFlower_03_Mature.prefab"
        };

        private static readonly string[] StemPrefabPaths =
        {
            $"{PrefabFolder}/TEST_SunflowerStem_01_Incipient.prefab",
            $"{PrefabFolder}/TEST_SunflowerStem_02_Young.prefab",
            $"{PrefabFolder}/TEST_SunflowerStem_03_Mature.prefab",
            $"{PrefabFolder}/TEST_SunflowerStem_04_Uniform90.prefab"
        };

        [MenuItem("Tools/Plant AR/Open and Configure Main Scene")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += Configure;
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            GameObject plantRoot = GameObject.Find("Plant_AR_Root");
            if (plantRoot == null)
            {
                plantRoot = new GameObject("Plant_AR_Root");
            }

            DisableOldStaticPlantRoots(scene);
            StemGrowthController growthController = ConfigureGrowthController(plantRoot.transform);
            ConfigureMainCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = growthController.gameObject;
            SceneView.FrameLastActiveSceneView();

            Debug.Log("Main plant demo configured. Press Play: anchor now, stem after 3 seconds; use WASD, mouse and Up/Down arrows.", growthController);
        }

        [InitializeOnLoadMethod]
        private static void RunRequestedDemoAfterReload()
        {
            if (!File.Exists(Path.GetFullPath(RunRequestPath)))
            {
                return;
            }

            AssetDatabase.DeleteAsset(RunRequestPath);
            EditorApplication.delayCall += WaitForEditModeAndRun;
        }

        private static void WaitForEditModeAndRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += WaitForEditModeAndRun;
                return;
            }

            Configure();
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        private static StemGrowthController ConfigureGrowthController(Transform plantRoot)
        {
            StemGrowthController controller = Object.FindObjectOfType<StemGrowthController>(true);
            if (controller == null)
            {
                var controllerObject = new GameObject("Sunflower Stem Growth");
                controller = controllerObject.AddComponent<StemGrowthController>();
            }

            controller.transform.SetParent(plantRoot, false);
            controller.transform.localPosition = Vector3.zero;
            controller.transform.localRotation = Quaternion.identity;
            controller.transform.localScale = Vector3.one;

            LeafGrowthController leafController = controller.GetComponent<LeafGrowthController>();
            if (leafController == null)
            {
                leafController = controller.gameObject.AddComponent<LeafGrowthController>();
            }

            FlowerGrowthController flowerController = controller.GetComponent<FlowerGrowthController>();
            if (flowerController == null)
            {
                flowerController = controller.gameObject.AddComponent<FlowerGrowthController>();
            }

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("arucoAnchorPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(ArucoAnchorPrefabPath);
            serializedController.FindProperty("secondsBeforeStemAppears").floatValue = 3f;
            serializedController.FindProperty("showBackdrop").boolValue = true;
            serializedController.FindProperty("backdropDistance").floatValue = 0.6f;
            serializedController.FindProperty("backdropSize").vector2Value = new Vector2(2.4f, 2.5f);
            serializedController.FindProperty("backdropCenterHeight").floatValue = 1.05f;
            serializedController.FindProperty("backdropColor").colorValue = new Color(0.68f, 0.7f, 0.66f, 1f);
            serializedController.FindProperty("unifiedStemColor").colorValue =
                new Color(0.08f, 0.32f, 0.09f, 1f);
            serializedController.FindProperty("startingStage").intValue = 1;
            serializedController.FindProperty("secondsBetweenGrowthSteps").floatValue = 3f;
            serializedController.FindProperty("scaleMultiplier").floatValue = 1.2f;
            serializedController.FindProperty("baseThicknessGrowthMultiplier").floatValue = 1.12f;
            serializedController.FindProperty("tipThicknessGrowthMultiplier").floatValue = 1.04f;
            serializedController.FindProperty("hideRoundedStemTips").boolValue = true;
            serializedController.FindProperty("growthStepsBeforeNextStage").intValue = 3;
            serializedController.FindProperty("stackNewStemAfterStageFour").boolValue = true;
            serializedController.FindProperty("secondsBeforeNextStackedStem").floatValue = 3f;
            serializedController.FindProperty("stackedStemOverlap").floatValue = 0.015f;
            serializedController.FindProperty("startAutomatically").boolValue = true;
            serializedController.FindProperty("leafGrowthController").objectReferenceValue = leafController;
            serializedController.FindProperty("flowerGrowthController").objectReferenceValue = flowerController;

            SerializedProperty stages = serializedController.FindProperty("stagePrefabs");
            stages.arraySize = StemPrefabPaths.Length;
            for (int index = 0; index < StemPrefabPaths.Length; index++)
            {
                stages.GetArrayElementAtIndex(index).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(StemPrefabPaths[index]);
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            var serializedLeaves = new SerializedObject(leafController);
            SerializedProperty leafPrefabs = serializedLeaves.FindProperty("leafStagePrefabs");
            leafPrefabs.arraySize = LeafPrefabPaths.Length;
            for (int index = 0; index < LeafPrefabPaths.Length; index++)
            {
                leafPrefabs.GetArrayElementAtIndex(index).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(LeafPrefabPaths[index]);
            }

            serializedLeaves.FindProperty("leavesPerStemSegment").intValue = 2;
            serializedLeaves.FindProperty("leafAngle").floatValue = 0f;
            serializedLeaves.FindProperty("verticalSpawnMargin").floatValue = 0.12f;
            serializedLeaves.FindProperty("leafConnectionHeightOffset").floatValue = -0.04f;
            serializedLeaves.FindProperty("leafPetioleOverlap").floatValue = 0.025f;
            serializedLeaves.FindProperty("leafPetioleThicknessMultiplier").floatValue = 1.15f;
            serializedLeaves.FindProperty("leafStemInsertionRatio").floatValue = 1f;
            serializedLeaves.FindProperty("leafConnectionBandFraction").floatValue = 0.08f;
            serializedLeaves.FindProperty("azimuthVariation").floatValue = 12f;
            serializedLeaves.FindProperty("maximumLeafCount").intValue = 12;
            serializedLeaves.FindProperty("delayBetweenLeaves").floatValue = 1f;
            serializedLeaves.FindProperty("leafGrowthDuration").floatValue = 3f;
            serializedLeaves.FindProperty("delayBetweenLeafStages").floatValue = 0.75f;
            serializedLeaves.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(leafController);

            var serializedFlower = new SerializedObject(flowerController);
            SerializedProperty flowerPrefabs = serializedFlower.FindProperty("flowerStagePrefabs");
            flowerPrefabs.arraySize = FlowerPrefabPaths.Length;
            for (int index = 0; index < FlowerPrefabPaths.Length; index++)
            {
                flowerPrefabs.GetArrayElementAtIndex(index).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(FlowerPrefabPaths[index]);
            }

            serializedFlower.FindProperty("secondsBetweenGrowthSteps").floatValue = 3f;
            serializedFlower.FindProperty("scaleMultiplier").floatValue = 1.2f;
            serializedFlower.FindProperty("growthStepsBeforeNextStage").intValue = 3;
            serializedFlower.FindProperty("flowerSizeMultiplier").floatValue = 1.5f;
            serializedFlower.FindProperty("peduncleStemOverlap").floatValue = 0.015f;
            serializedFlower.FindProperty("peduncleBaseBandFraction").floatValue = 0.06f;
            serializedFlower.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flowerController);
            return controller;
        }

        private static void ConfigureMainCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            if (camera.GetComponent<WASDCameraController>() == null)
            {
                camera.gameObject.AddComponent<WASDCameraController>();
            }

            camera.transform.position = new Vector3(0f, 0.55f, -1.4f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.45f, 0f) - camera.transform.position);
            camera.nearClipPlane = 0.03f;
            EditorUtility.SetDirty(camera.gameObject);
        }

        private static void DisableOldStaticPlantRoots(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Flower Stem"
                    || root.name.StartsWith("Flower_")
                    || root.name.StartsWith("SunflowerFlower_"))
                {
                    root.SetActive(false);
                    EditorUtility.SetDirty(root);
                }
            }

            GameObject oldStem = GameObject.Find("Stem_Main");
            if (oldStem != null)
            {
                oldStem.SetActive(false);
                EditorUtility.SetDirty(oldStem);
            }
        }
    }
}
