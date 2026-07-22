using ICI.PlantGrowth;
using ICI.PlantGrowth.MixedCrops;
using ICI.PlantGrowth.Phenology;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ICI.PlantGrowth.Editor
{
    // Configures both the visual sunflower and its numerical phenology controls.
    public static class ConfigurePlantDemoScene
    {
        private const string MainScenePath = "Assets/_Project/Scenes/01_Main.unity";
        private const string RunRequestPath = "Assets/_Project/Editor/RunPlantDemo.request";
        private const string PrefabFolder = "Assets/_Project/TEST/Prefabs";
        private const string ArucoAnchorPrefabPath = "Assets/_Project/TEST/Prefabs/Aruco anchor.prefab";
        private const string DataFolder = "Assets/_Project/Data";
        private const string SunflowerPhenologyProfilePath =
            DataFolder + "/SunflowerPhenologyProfile.asset";
        private const string MixedCropPrefabFolder = "Assets/_Project/Prefabs/MixedCrops";
        private const string SunflowerVisualPrefabPath =
            MixedCropPrefabFolder + "/SunflowerVisual.prefab";

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
            GameObject sunflowerVisualPrefab = CreateSunflowerVisualPrefab(growthController);
            MixedCropFieldController mixedCropField = ConfigureMixedCropField(
                plantRoot.transform,
                sunflowerVisualPrefab);
            ConfigurePhenologySimulation(growthController, mixedCropField);
            ConfigureMainCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = growthController.gameObject;
            SceneView.FrameLastActiveSceneView();

            Debug.Log(
                "Main plant demo configured. Press Play, adjust the phenology sliders, then press Start Simulation. "
                + "Use WASD, Up/Down arrows and right-click + mouse.",
                growthController);
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
            serializedController.FindProperty("backdropDistance").floatValue = 4.6f;
            serializedController.FindProperty("backdropSize").vector2Value = new Vector2(7.5f, 5.2f);
            serializedController.FindProperty("backdropCenterHeight").floatValue = 2.15f;
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
            serializedController.FindProperty("startAutomatically").boolValue = false;
            serializedController.FindProperty("simulationSpeedMultiplier").floatValue = 1f;
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
            serializedLeaves.FindProperty("youngLeafUpwardAngle").floatValue = 28f;
            serializedLeaves.FindProperty("developingLeafUpwardAngle").floatValue = 10f;
            serializedLeaves.FindProperty("verticalSpawnMargin").floatValue = 0.12f;
            serializedLeaves.FindProperty("leafConnectionHeightOffset").floatValue = -0.04f;
            serializedLeaves.FindProperty("leafPetioleOverlap").floatValue = 0.025f;
            serializedLeaves.FindProperty("leafPetioleThicknessMultiplier").floatValue = 1.15f;
            serializedLeaves.FindProperty("leafStemInsertionRatio").floatValue = 1f;
            serializedLeaves.FindProperty("leafConnectionBandFraction").floatValue = 0.08f;
            serializedLeaves.FindProperty("azimuthVariation").floatValue = 5f;
            serializedLeaves.FindProperty("leafPhyllotaxisAngle").floatValue = 137.5f;
            serializedLeaves.FindProperty("lowerLeafSize").floatValue = 0.82f;
            serializedLeaves.FindProperty("middleLeafSize").floatValue = 1.12f;
            serializedLeaves.FindProperty("upperLeafSize").floatValue = 0.7f;
            serializedLeaves.FindProperty("maximumLeafCount").intValue = 12;
            serializedLeaves.FindProperty("delayBetweenLeaves").floatValue = 1f;
            serializedLeaves.FindProperty("leafGrowthDuration").floatValue = 3f;
            serializedLeaves.FindProperty("delayBetweenLeafStages").floatValue = 0.75f;
            serializedLeaves.FindProperty("simulationSpeedMultiplier").floatValue = 1f;
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
            serializedFlower.FindProperty("simulationSpeedMultiplier").floatValue = 1f;
            serializedFlower.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flowerController);
            return controller;
        }

        private static GameObject CreateSunflowerVisualPrefab(
            StemGrowthController sourceController)
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(MixedCropPrefabFolder);

            GameObject visual = Object.Instantiate(sourceController.gameObject);
            visual.name = "Sunflower Visual";
            PlantPhenologySimulation simulation =
                visual.GetComponent<PlantPhenologySimulation>();
            if (simulation != null)
            {
                Object.DestroyImmediate(simulation);
            }

            StemGrowthController visualController = visual.GetComponent<StemGrowthController>();
            var serializedVisual = new SerializedObject(visualController);
            serializedVisual.FindProperty("arucoAnchorPrefab").objectReferenceValue = null;
            serializedVisual.FindProperty("showBackdrop").boolValue = false;
            serializedVisual.FindProperty("startAutomatically").boolValue = false;
            serializedVisual.ApplyModifiedPropertiesWithoutUndo();
            visual.SetActive(false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                visual,
                SunflowerVisualPrefabPath);
            Object.DestroyImmediate(visual);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static MixedCropFieldController ConfigureMixedCropField(
            Transform plantRoot,
            GameObject sunflowerVisualPrefab)
        {
            Transform existing = plantRoot.Find("Mixed Crop Field");
            GameObject fieldObject = existing != null
                ? existing.gameObject
                : new GameObject("Mixed Crop Field");
            fieldObject.transform.SetParent(plantRoot, false);
            fieldObject.transform.localPosition = Vector3.zero;
            fieldObject.transform.localRotation = Quaternion.identity;
            fieldObject.transform.localScale = Vector3.one;

            MixedCropFieldController field =
                fieldObject.GetComponent<MixedCropFieldController>();
            if (field == null)
            {
                field = fieldObject.AddComponent<MixedCropFieldController>();
            }

            var serializedField = new SerializedObject(field);
            serializedField.FindProperty("sunflowerVisualPrefab").objectReferenceValue =
                sunflowerVisualPrefab;
            serializedField.FindProperty("sunflowerCount").intValue = 3;
            serializedField.FindProperty("cassavaCount").intValue = 3;
            serializedField.FindProperty("horizontalSpacing").floatValue = 1.25f;
            serializedField.FindProperty("rowSpacing").floatValue = 1.3f;
            serializedField.FindProperty("staggerAlternateRows").boolValue = true;
            serializedField.FindProperty("localOrigin").vector3Value = Vector3.zero;
            serializedField.FindProperty("sunflowerScale").floatValue = 0.72f;
            serializedField.FindProperty("cassavaScale").floatValue = 1f;
            serializedField.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(field);
            return field;
        }

        private static void ConfigurePhenologySimulation(
            StemGrowthController growthController,
            MixedCropFieldController mixedCropField)
        {
            PlantPhenologyProfile profile = CreateOrLoadSunflowerProfile();
            PlantPhenologySimulation simulation =
                growthController.GetComponent<PlantPhenologySimulation>();

            if (simulation == null)
            {
                simulation = growthController.gameObject.AddComponent<PlantPhenologySimulation>();
            }

            var serializedSimulation = new SerializedObject(simulation);
            serializedSimulation.FindProperty("profile").objectReferenceValue = profile;
            serializedSimulation.FindProperty("stemGrowthController").objectReferenceValue = growthController;
            serializedSimulation.FindProperty("mixedCropFieldController").objectReferenceValue =
                mixedCropField;
            serializedSimulation.FindProperty("requestedSunflowerCount").intValue = 3;
            serializedSimulation.FindProperty("requestedCassavaCount").intValue = 3;
            serializedSimulation.FindProperty("currentTemperature").floatValue = 24f;
            serializedSimulation.FindProperty("currentMinimumTemperature").floatValue = 18f;
            serializedSimulation.FindProperty("currentMaximumTemperature").floatValue = 30f;
            serializedSimulation.FindProperty("currentDayLength").floatValue = 14f;
            serializedSimulation.FindProperty("currentSolarRadiation").floatValue = 18f;
            serializedSimulation.FindProperty("soilWaterAvailability").floatValue = 1f;
            serializedSimulation.FindProperty("secondsPerSimulatedDay").floatValue = 0.5f;
            serializedSimulation.FindProperty("visualGrowthSpeed").floatValue = 1f;
            serializedSimulation.FindProperty("visualMorphologySpeedFactor").floatValue = 3f;
            serializedSimulation.FindProperty("showRuntimePanel").boolValue = true;
            serializedSimulation.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(simulation);
        }

        private static PlantPhenologyProfile CreateOrLoadSunflowerProfile()
        {
            PlantPhenologyProfile profile =
                AssetDatabase.LoadAssetAtPath<PlantPhenologyProfile>(
                    SunflowerPhenologyProfilePath);

            if (profile != null)
            {
                return profile;
            }

            if (!AssetDatabase.IsValidFolder(DataFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            }

            profile = ScriptableObject.CreateInstance<PlantPhenologyProfile>();
            AssetDatabase.CreateAsset(profile, SunflowerPhenologyProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
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

            WASDCameraController cameraController = camera.GetComponent<WASDCameraController>();
            if (cameraController == null)
            {
                cameraController = camera.gameObject.AddComponent<WASDCameraController>();
            }

            var serializedCameraController = new SerializedObject(cameraController);
            serializedCameraController.FindProperty("lockCursorWhilePlaying").boolValue = false;
            serializedCameraController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cameraController);

            camera.transform.position = new Vector3(1.6f, 2.45f, -7.8f);
            camera.transform.rotation = Quaternion.LookRotation(
                new Vector3(0.35f, 1.35f, 1.35f) - camera.transform.position);
            camera.nearClipPlane = 0.03f;
            EditorUtility.SetDirty(camera.gameObject);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void DisableOldStaticPlantRoots(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // Broken legacy prefab instances only add Console errors and
                // are not part of the procedural sunflower used by this scene.
                if (PrefabUtility.GetPrefabInstanceStatus(root)
                    == PrefabInstanceStatus.MissingAsset)
                {
                    Object.DestroyImmediate(root);
                    continue;
                }

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
