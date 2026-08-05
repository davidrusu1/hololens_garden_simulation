using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

public static class HoloLensPlantSceneSetup
{
    private const string MainScenePath = "Assets/Main.unity";
    private const string InteractionSetupPrefabPath =
        "Assets/MRTemplateAssets/Prefabs/MRInteractionSetup.prefab";
    private const string XrGeneralSettingsPath =
        "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
    private const string OpenXrLoaderType =
        "UnityEngine.XR.OpenXR.OpenXRLoader";
    private const string CompanyName = "ICI";
    private const string ProductName = "Maple Growth Simulation";
    private const string PackageName = "ICI.MapleGrowthSimulation";
    private const string ApplicationIdentifier =
        "com.ici.maplegrowthsimulation";

    [MenuItem("Tools/Plant Simulation/Rebuild HoloLens Experience")]
    public static void ConfigureHoloLensScene()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Reconstruiește experiența HoloLens?",
            "Această acțiune reconfigurează scena Main, meniul, poziționarea "
            + "plantei și setările OpenXR pentru Windows.",
            "Reconstruiește",
            "Anulează");
        if (!confirmed)
        {
            return;
        }

        ApplyHoloLensConfiguration();
    }

    [MenuItem("Tools/Plant Simulation/Apply HoloLens 2 Settings")]
    public static void ApplyHoloLensSettings()
    {
        ApplyHoloLensConfiguration();
    }

    private static void ApplyHoloLensConfiguration()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ApplyHoloLensConfiguration;
            return;
        }

        Scene scene = ResolvePlantScene();
        if (!scene.IsValid())
        {
            Debug.LogError(
                "[HoloLens setup] Nu am putut deschide scena plantei.");
            return;
        }

        GameObject interactionSetup = FindSceneObject(
            scene,
            "MRInteractionSetup");
        if (interactionSetup == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                InteractionSetupPrefabPath);
            if (prefab == null)
            {
                Debug.LogError(
                    "[HoloLens setup] Lipsește prefab-ul MRInteractionSetup.");
                return;
            }

            interactionSetup = PrefabUtility.InstantiatePrefab(
                prefab,
                scene) as GameObject;
            if (interactionSetup == null)
            {
                Debug.LogError(
                    "[HoloLens setup] Prefab-ul MRInteractionSetup nu a "
                    + "putut fi instanțiat.");
                return;
            }

            interactionSetup.name = "MRInteractionSetup";
            interactionSetup.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
        }

        if (!AddHoloLensExperience(scene))
        {
            return;
        }

        DisableDesktopCameras(scene, interactionSetup.transform);
        if (!ConfigureXrCamera(interactionSetup.transform))
        {
            return;
        }

        if (!ConfigureXrEventSystem(interactionSetup.transform))
        {
            return;
        }

        ConfigureBuildScene();
        if (!ConfigureWindowsXr())
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MainScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[HoloLens setup] Scena plantei, OpenXR, mâinile și meniul "
            + "world-space sunt configurate pentru HoloLens.");
    }

    private static Scene ResolvePlantScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid()
            && activeScene.path == MainScenePath)
        {
            return activeScene;
        }

        return EditorSceneManager.OpenScene(
            MainScenePath,
            OpenSceneMode.Single);
    }

    private static bool AddHoloLensExperience(Scene scene)
    {
        Plant plant = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0;
            rootIndex < roots.Length && plant == null;
            rootIndex++)
        {
            plant = roots[rootIndex].GetComponentInChildren<Plant>(true);
        }

        if (plant == null)
        {
            Debug.LogError(
                "[HoloLens setup] Scena Main nu conține componenta Plant.");
            return false;
        }

        HoloLensSimulationPanel panel =
            plant.GetComponent<HoloLensSimulationPanel>();
        if (panel == null)
        {
            panel = Undo.AddComponent<HoloLensSimulationPanel>(
                plant.gameObject);
        }

        HoloLensPlantPresentation presentation =
            plant.GetComponent<HoloLensPlantPresentation>();
        if (presentation == null)
        {
            Undo.AddComponent<HoloLensPlantPresentation>(
                plant.gameObject);
        }

        SerializedObject serializedPanel = new SerializedObject(panel);
        SerializedProperty simulationProperty =
            serializedPanel.FindProperty("simulation");
        simulationProperty.objectReferenceValue = plant;
        serializedPanel.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static GameObject FindSceneObject(
        Scene scene,
        string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms =
                roots[rootIndex].GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == objectName)
                {
                    return transforms[index].gameObject;
                }
            }
        }

        return null;
    }

    private static void DisableDesktopCameras(
        Scene scene,
        Transform interactionSetup)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Camera[] cameras =
                roots[rootIndex].GetComponentsInChildren<Camera>(true);
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera.transform.IsChildOf(interactionSetup))
                {
                    continue;
                }

                camera.enabled = false;
                AudioListener listener =
                    camera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }

                WASDCameraController controller =
                    camera.GetComponent<WASDCameraController>();
                if (controller != null)
                {
                    controller.enabled = false;
                }

                if (!camera.name.Contains("disabled in XR"))
                {
                    camera.name += " (disabled in XR)";
                }
            }
        }
    }

    private static bool ConfigureXrCamera(Transform interactionSetup)
    {
        Camera xrCamera = null;
        Camera[] cameras =
            interactionSetup.GetComponentsInChildren<Camera>(true);
        for (int index = 0; index < cameras.Length; index++)
        {
            if (cameras[index].CompareTag("MainCamera"))
            {
                xrCamera = cameras[index];
                break;
            }
        }

        if (xrCamera == null && cameras.Length > 0)
        {
            xrCamera = cameras[0];
            xrCamera.tag = "MainCamera";
        }

        if (xrCamera == null)
        {
            Debug.LogError(
                "[HoloLens setup] MRInteractionSetup nu conține o cameră XR.");
            return false;
        }

        xrCamera.enabled = true;
        xrCamera.nearClipPlane = 0.05f;
        xrCamera.clearFlags = CameraClearFlags.SolidColor;
        xrCamera.backgroundColor = Color.clear;

        AudioListener listener = xrCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = true;
        }

        return true;
    }

    private static void ConfigureBuildScene()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainScenePath, true)
        };
    }

    private static bool ConfigureXrEventSystem(Transform interactionSetup)
    {
        EventSystem[] eventSystems =
            interactionSetup.GetComponentsInChildren<EventSystem>(true);
        EventSystem xrEventSystem = null;
        for (int index = 0; index < eventSystems.Length; index++)
        {
            if (eventSystems[index].GetComponent<XRUIInputModule>() != null)
            {
                xrEventSystem = eventSystems[index];
                break;
            }
        }

        if (xrEventSystem == null)
        {
            Debug.LogError(
                "[HoloLens setup] MRInteractionSetup nu conține "
                + "EventSystem cu XRUIInputModule.");
            return false;
        }

        xrEventSystem.enabled = true;
        xrEventSystem.pixelDragThreshold = 4;
        XRUIInputModule inputModule =
            xrEventSystem.GetComponent<XRUIInputModule>();
        inputModule.enabled = true;
        inputModule.enableXRInput = true;
        inputModule.trackedDeviceDragThresholdMultiplier = 0.25f;

        EventSystem[] allEventSystems =
            Object.FindObjectsOfType<EventSystem>(true);
        for (int index = 0; index < allEventSystems.Length; index++)
        {
            EventSystem duplicate = allEventSystems[index];
            if (duplicate != xrEventSystem)
            {
                duplicate.enabled = false;
                EditorUtility.SetDirty(duplicate);
            }
        }

        EditorUtility.SetDirty(xrEventSystem);
        EditorUtility.SetDirty(inputModule);
        return true;
    }

    private static bool ConfigureWindowsXr()
    {
        XRGeneralSettingsPerBuildTarget perTarget =
            AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                XrGeneralSettingsPath);
        if (perTarget == null)
        {
            Debug.LogError(
                "[HoloLens setup] Lipsește configurația XR generală.");
            return false;
        }

        if (!perTarget.HasManagerSettingsForBuildTarget(
                BuildTargetGroup.WSA))
        {
            perTarget.CreateDefaultManagerSettingsForBuildTarget(
                BuildTargetGroup.WSA);
        }

        XRGeneralSettings generalSettings =
            perTarget.SettingsForBuildTarget(BuildTargetGroup.WSA);
        generalSettings.InitManagerOnStart = true;
        XRPackageMetadataStore.AssignLoader(
            generalSettings.Manager,
            OpenXrLoaderType,
            BuildTargetGroup.WSA);
        EditorUtility.SetDirty(generalSettings);
        EditorUtility.SetDirty(perTarget);

        FeatureHelpers.RefreshFeatures(BuildTargetGroup.WSA);
        OpenXRSettings openXrSettings =
            OpenXRSettings.GetSettingsForBuildTargetGroup(
                BuildTargetGroup.WSA);
        if (openXrSettings == null)
        {
            Debug.LogError(
                "[HoloLens setup] Lipsește profilul OpenXR Metro/WSA. "
                + "Instalează modulul Windows Build Support și reaplică "
                + "setările HoloLens.");
            return false;
        }

        bool handFeaturesReady =
            EnableFeature<MicrosoftHandInteraction>(openXrSettings)
            & EnableFeature<HandInteractionProfile>(openXrSettings)
            & EnableFeature<HandTracking>(openXrSettings);
        EnableFeature<EyeGazeInteraction>(openXrSettings);
        EnableFeature<MicrosoftMotionControllerProfile>(openXrSettings);
        if (!handFeaturesReady)
        {
            Debug.LogError(
                "[HoloLens setup] Profilurile OpenXR de mână nu au putut "
                + "fi activate.");
            return false;
        }

        openXrSettings.depthSubmissionMode =
            OpenXRSettings.DepthSubmissionMode.Depth16Bit;
        EditorUtility.SetDirty(openXrSettings);

        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;
        // Required by Unity OpenXR on HoloLens when the legacy Microsoft
        // Mixed Reality OpenXR extension package is not installed.
        PlayerSettings.runInBackground = true;
        PlayerSettings.WSA.transparentSwapchain = false;
        PlayerSettings.WSA.packageName = PackageName;
        PlayerSettings.WSA.applicationDescription =
            "Real-time maple growth simulation for HoloLens 2.";
        PlayerSettings.WSA.SetTargetDeviceFamily(
            PlayerSettings.WSATargetFamily.Holographic,
            true);
        PlayerSettings.WSA.SetTargetDeviceFamily(
            PlayerSettings.WSATargetFamily.Desktop,
            false);
        PlayerSettings.WSA.SetCapability(
            PlayerSettings.WSACapability.InternetClient,
            true);
        PlayerSettings.WSA.SetCapability(
            PlayerSettings.WSACapability.WebCam,
            true);
        PlayerSettings.WSA.SetCapability(
            PlayerSettings.WSACapability.Microphone,
            true);
        PlayerSettings.WSA.SetCapability(
            PlayerSettings.WSACapability.SpatialPerception,
            true);
        PlayerSettings.WSA.SetCapability(
            PlayerSettings.WSACapability.GazeInput,
            true);
        PlayerSettings.SetApplicationIdentifier(
            BuildTargetGroup.WSA,
            ApplicationIdentifier);
        PlayerSettings.SetScriptingBackend(
            BuildTargetGroup.WSA,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.SetArchitecture(BuildTargetGroup.WSA, 1);
        PlayerSettings.SetGraphicsAPIs(
            BuildTarget.WSAPlayer,
            new[] { GraphicsDeviceType.Direct3D11 });
        return true;
    }

    private static bool EnableFeature<TFeature>(
        OpenXRSettings settings)
        where TFeature : UnityEngine.XR.OpenXR.Features.OpenXRFeature
    {
        TFeature feature = settings.GetFeature<TFeature>();
        if (feature == null)
        {
            Debug.LogWarning(
                $"[HoloLens setup] Funcția OpenXR {typeof(TFeature).Name} "
                + "nu există în profilul Windows.");
            return false;
        }

        feature.enabled = true;
        EditorUtility.SetDirty(feature);
        return true;
    }
}
