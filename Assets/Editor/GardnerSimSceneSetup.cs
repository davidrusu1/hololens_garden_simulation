using ICI.PlantGrowth.MixedCrops;
using ICI.PlantGrowth.Phenology;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class GardnerSimSceneSetup
{
    private const string MainScenePath = "Assets/Main.unity";
    private const string SunflowerPrefabPath =
        "Assets/Modelare SF/_Project/Prefabs/MixedCrops/SunflowerVisual.prefab";
    private const string SunflowerProfilePath =
        "Assets/Modelare SF/_Project/Data/SunflowerPhenologyProfile.asset";

    [MenuItem("Tools/Gardner sim/Configure Main Scene")]
    public static void ConfigureMainScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            MainScenePath,
            OpenSceneMode.Single);

        GameObject sunflowerPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(SunflowerPrefabPath);
        PlantPhenologyProfile sunflowerProfile =
            AssetDatabase.LoadAssetAtPath<PlantPhenologyProfile>(
                SunflowerProfilePath);
        if (sunflowerPrefab == null || sunflowerProfile == null)
        {
            throw new UnityException(
                "Gardner sim requires the latest sunflower prefab and phenology profile.");
        }

        Plant mapleSimulation = Object.FindObjectOfType<Plant>(true);
        if (mapleSimulation == null)
        {
            throw new UnityException(
                "Gardner sim could not find the maple Plant in Assets/Main.unity.");
        }

        GameObject appRoot = FindOrCreateRoot("Gardner sim");
        appRoot.transform.SetPositionAndRotation(
            Vector3.zero,
            Quaternion.identity);
        appRoot.transform.localScale = Vector3.one;
        GardnerSimController controller =
            EnsureComponent<GardnerSimController>(appRoot);

        Transform gardenContent = appRoot.transform.Find("Garden Content");
        if (gardenContent == null)
        {
            GameObject contentObject = new GameObject("Garden Content");
            gardenContent = contentObject.transform;
            gardenContent.SetParent(appRoot.transform, false);
        }
        gardenContent.localScale = Vector3.one * 0.38f;

        GameObject mapleRoot = mapleSimulation.gameObject;
        mapleRoot.name = "Maple Simulation";
        mapleRoot.transform.SetParent(gardenContent, false);
        mapleRoot.transform.localPosition = Vector3.zero;
        mapleRoot.transform.localRotation = Quaternion.identity;
        mapleRoot.transform.localScale = Vector3.one;
        mapleRoot.SetActive(true);

        ConfigureMaple(mapleRoot, mapleSimulation);

        Transform existingSunflower = gardenContent.Find("Sunflower Simulation");
        GameObject sunflowerRoot;
        if (existingSunflower == null)
        {
            sunflowerRoot = new GameObject("Sunflower Simulation");
            sunflowerRoot.transform.SetParent(gardenContent, false);
        }
        else
        {
            sunflowerRoot = existingSunflower.gameObject;
        }

        sunflowerRoot.transform.localPosition = Vector3.zero;
        sunflowerRoot.transform.localRotation = Quaternion.identity;
        sunflowerRoot.transform.localScale = Vector3.one;
        sunflowerRoot.SetActive(true);

        MixedCropFieldController sunflowerField =
            EnsureComponent<MixedCropFieldController>(sunflowerRoot);
        PlantPhenologySimulation sunflowerSimulation =
            EnsureComponent<PlantPhenologySimulation>(sunflowerRoot);

        ConfigureSunflowerField(sunflowerField, sunflowerPrefab);
        ConfigureSunflowerSimulation(
            sunflowerSimulation,
            sunflowerField,
            sunflowerProfile);

        ConfigureController(
            controller,
            gardenContent,
            mapleRoot,
            mapleSimulation,
            sunflowerRoot,
            sunflowerSimulation,
            sunflowerField);

        PlayerSettings.companyName = "ICI";
        PlayerSettings.productName = "Gardner sim";
        ConfigureHoloLensBuildSettings();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainScenePath, true)
        };

        EditorUtility.SetDirty(appRoot);
        EditorUtility.SetDirty(mapleRoot);
        EditorUtility.SetDirty(sunflowerRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[Gardner sim] Main scene configured with maple, sunflower, "
            + "comfortable world-space UI and HoloLens 2 build metadata.");
    }

    public static void ConfigureFromCommandLine()
    {
        try
        {
            ConfigureMainScene();
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ConfigureMaple(
        GameObject mapleRoot,
        Plant mapleSimulation)
    {
        SerializedObject plantObject = new SerializedObject(mapleSimulation);
        plantObject.FindProperty("startAutomatically").boolValue = false;
        plantObject.ApplyModifiedPropertiesWithoutUndo();

        HoloLensSimulationPanel oldHoloLensPanel =
            mapleRoot.GetComponent<HoloLensSimulationPanel>();
        if (oldHoloLensPanel != null)
        {
            oldHoloLensPanel.SetMenuVisible(false);
            oldHoloLensPanel.enabled = false;
            EditorUtility.SetDirty(oldHoloLensPanel);
        }

        MapleSimulationPanel oldMaplePanel =
            mapleRoot.GetComponent<MapleSimulationPanel>();
        if (oldMaplePanel != null)
        {
            oldMaplePanel.SetRuntimePanelVisible(false);
            oldMaplePanel.enabled = false;
            EditorUtility.SetDirty(oldMaplePanel);
        }

        HoloLensPlantPresentation presentation =
            mapleRoot.GetComponent<HoloLensPlantPresentation>();
        if (presentation != null)
        {
            SerializedObject presentationObject =
                new SerializedObject(presentation);
            presentationObject
                .FindProperty("placeRelativeToViewerOnStart")
                .boolValue = false;
            presentationObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureSunflowerField(
        MixedCropFieldController field,
        GameObject sunflowerPrefab)
    {
        SerializedObject fieldObject = new SerializedObject(field);
        fieldObject.FindProperty("sunflowerVisualPrefab").objectReferenceValue =
            sunflowerPrefab;
        fieldObject.FindProperty("sunflowerCount").intValue = 1;
        fieldObject.FindProperty("cassavaCount").intValue = 0;
        fieldObject.FindProperty("horizontalSpacing").floatValue = 1.25f;
        fieldObject.FindProperty("rowSpacing").floatValue = 1.3f;
        fieldObject.FindProperty("localOrigin").vector3Value = Vector3.zero;
        fieldObject.FindProperty("sunflowerMatureHeightMeters").floatValue =
            2.35f;
        fieldObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSunflowerSimulation(
        PlantPhenologySimulation simulation,
        MixedCropFieldController field,
        PlantPhenologyProfile profile)
    {
        SerializedObject simulationObject = new SerializedObject(simulation);
        simulationObject.FindProperty("profile").objectReferenceValue = profile;
        simulationObject.FindProperty("stemGrowthController").objectReferenceValue =
            null;
        simulationObject
            .FindProperty("mixedCropFieldController")
            .objectReferenceValue = field;
        simulationObject.FindProperty("requestedSunflowerCount").intValue = 1;
        simulationObject.FindProperty("requestedCassavaCount").intValue = 0;
        simulationObject.FindProperty("showRuntimePanel").boolValue = false;
        simulationObject.FindProperty("secondsPerSimulatedDay").floatValue = 0.5f;
        simulationObject.FindProperty("visualGrowthSpeed").floatValue = 1f;
        simulationObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureController(
        GardnerSimController controller,
        Transform gardenContent,
        GameObject mapleRoot,
        Plant mapleSimulation,
        GameObject sunflowerRoot,
        PlantPhenologySimulation sunflowerSimulation,
        MixedCropFieldController sunflowerField)
    {
        SerializedObject controllerObject = new SerializedObject(controller);
        controllerObject.FindProperty("mapleRoot").objectReferenceValue =
            mapleRoot;
        controllerObject.FindProperty("mapleSimulation").objectReferenceValue =
            mapleSimulation;
        controllerObject.FindProperty("sunflowerRoot").objectReferenceValue =
            sunflowerRoot;
        controllerObject
            .FindProperty("sunflowerSimulation")
            .objectReferenceValue = sunflowerSimulation;
        controllerObject.FindProperty("sunflowerField").objectReferenceValue =
            sunflowerField;
        controllerObject.FindProperty("gardenContent").objectReferenceValue =
            gardenContent;
        controllerObject.FindProperty("selectedPlant").enumValueIndex = 2;
        controllerObject.FindProperty("startAutomatically").boolValue = false;
        controllerObject.FindProperty("plantsDistance").floatValue = 2.25f;
        controllerObject.FindProperty("plantsBelowEyeLevel").floatValue = 0.95f;
        controllerObject.FindProperty("panelDistance").floatValue = 1.1f;
        controllerObject.FindProperty("panelHorizontalOffset").floatValue = -0.35f;
        controllerObject.FindProperty("panelVerticalOffset").floatValue = -0.05f;
        controllerObject.FindProperty("panelWorldScale").floatValue = 0.00055f;
        controllerObject.FindProperty("showcaseScale").floatValue = 0.38f;
        controllerObject.FindProperty("sideBySideSpacing").floatValue = 3.8f;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureHoloLensBuildSettings()
    {
        PlayerSettings.companyName = "ICI";
        PlayerSettings.productName = "Gardner sim";
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetArchitecture(BuildTargetGroup.WSA, 1);
        PlayerSettings.SetScriptingBackend(
            BuildTargetGroup.WSA,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApplicationIdentifier(
            BuildTargetGroup.WSA,
            "com.ici.gardnersim");
        PlayerSettings.SetGraphicsAPIs(
            BuildTarget.WSAPlayer,
            new[] { GraphicsDeviceType.Direct3D11 });
        PlayerSettings.WSA.packageName = "ICI.GardnerSim";
        PlayerSettings.WSA.applicationDescription =
            "DVS-driven maple and sunflower growth simulation for HoloLens 2.";
        PlayerSettings.WSA.transparentSwapchain = false;
        PlayerSettings.WSA.SetTargetDeviceFamily(
            PlayerSettings.WSATargetFamily.Desktop,
            false);
        PlayerSettings.WSA.SetTargetDeviceFamily(
            PlayerSettings.WSATargetFamily.Holographic,
            true);
        PlayerSettings.WSA.SetCapability(
            PlayerSettings.WSACapability.GazeInput,
            true);
        PlayerSettings.WSA.SetCapability(
            PlayerSettings.WSACapability.SpatialPerception,
            true);
        PlayerSettings.WSA.SetCapability(
            PlayerSettings.WSACapability.InternetClient,
            true);
        PlayerSettings.WSA.SetCapability(
            PlayerSettings.WSACapability.WebCam,
            true);
        PlayerSettings.WSA.SetCapability(
            PlayerSettings.WSACapability.Microphone,
            true);
    }

    private static GameObject FindOrCreateRoot(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
        {
            return existing;
        }

        return new GameObject(objectName);
    }

    private static T EnsureComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}
