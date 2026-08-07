using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICI.PlantGrowth.Phenology;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

[InitializeOnLoad]
public static class GardnerSimRuntimeVerifier
{
    private const string ScenePath = "Assets/Main.unity";
    private const string ActiveKey = "GardnerSimVerifier.Active";
    private const string PhaseKey = "GardnerSimVerifier.Phase";
    private const string ErrorKey = "GardnerSimVerifier.Errors";
    private const string FrameKey = "GardnerSimVerifier.Frames";
    private const string MapleDvsKey = "GardnerSimVerifier.MapleDvs";
    private const string SunflowerDvsKey = "GardnerSimVerifier.SunflowerDvs";
    private const string PauseVisualPrefix = "GardnerSimVerifier.PauseVisual.";
    private const string StalledVisualPrefix = "GardnerSimVerifier.StalledVisual.";
    private const string PreviousOptionsEnabledKey = "GardnerSimVerifier.PreviousOptionsEnabled";
    private const string PreviousOptionsKey = "GardnerSimVerifier.PreviousOptions";
    private const string ExitWhenFinishedKey =
        "GardnerSimVerifier.ExitWhenFinished";

    static GardnerSimRuntimeVerifier()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            EditorApplication.delayCall += ResumeAfterAssemblyReload;
        }
    }

    [MenuItem("Tools/Gardner sim/Verify Runtime")]
    public static void RunFromMenu()
    {
        BeginVerification(false);
    }

    public static void RunFromCommandLine()
    {
        BeginVerification(true);
    }

    private static void BeginVerification(bool exitWhenFinished)
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(ExitWhenFinishedKey, exitWhenFinished);
        SessionState.SetString(PhaseKey, "entering");
        SessionState.SetString(ErrorKey, string.Empty);
        SessionState.SetInt(FrameKey, 0);
        SessionState.SetBool(
            PreviousOptionsEnabledKey,
            EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt(
            PreviousOptionsKey,
            (int)EditorSettings.enterPlayModeOptions);

        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AttachCallbacks();
        EditorApplication.EnterPlaymode();
    }

    private static void ResumeAfterAssemblyReload()
    {
        if (!SessionState.GetBool(ActiveKey, false))
        {
            return;
        }

        string phase = SessionState.GetString(PhaseKey, "entering");
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (phase == "verified")
            {
                Finish(true);
                return;
            }
            if (phase == "failed")
            {
                Finish(false);
                return;
            }
        }

        AttachCallbacks();
        if (EditorApplication.isPlaying)
        {
            SessionState.SetString(PhaseKey, "running");
        }
    }

    private static void AttachCallbacks()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
    }

    private static void DetachCallbacks()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        Application.logMessageReceived -= CaptureLog;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false))
        {
            return;
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetString(PhaseKey, "running");
            SessionState.SetInt(FrameKey, 0);
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            string phase = SessionState.GetString(PhaseKey, string.Empty);
            Finish(phase == "verified");
        }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false)
            || !EditorApplication.isPlaying
            || EditorApplication.isPaused)
        {
            return;
        }

        int frameCount = SessionState.GetInt(FrameKey, 0) + 1;
        SessionState.SetInt(FrameKey, frameCount);
        try
        {
            if (frameCount == 12)
            {
                VerifyInitialExperienceAndStart();
            }
            else if (frameCount == 45)
            {
                CapturePreview();
            }
            else if (frameCount == 100)
            {
                BeginPauseFreezeCheck();
            }
            else if (frameCount == 130)
            {
                VerifyPauseAndStartStalledDevelopment();
            }
            else if (frameCount == 190)
            {
                CaptureStalledDevelopmentState();
            }
            else if (frameCount == 260)
            {
                VerifyStalledDevelopmentAndResume();
            }
            else if (frameCount == 420)
            {
                VerifyResumeAndFinish();
            }
        }
        catch (Exception exception)
        {
            AppendError(exception.ToString());
            SessionState.SetString(PhaseKey, "failed");
            EditorApplication.ExitPlaymode();
        }
    }

    private static void VerifyInitialExperienceAndStart()
    {
        GardnerSimController controller = UnityEngine.Object.FindObjectOfType<GardnerSimController>(true);
        Plant maple = UnityEngine.Object.FindObjectOfType<Plant>(true);
        PlantPhenologySimulation sunflower = UnityEngine.Object.FindObjectOfType<PlantPhenologySimulation>(true);
        Require(controller != null, "GardnerSimController is missing.");
        Require(maple != null, "Maple simulation is missing.");
        Require(sunflower != null, "Sunflower simulation is missing.");

        Canvas worldCanvas = UnityEngine.Object.FindObjectsOfType<Canvas>(true)
            .FirstOrDefault(canvas => canvas.name == "Gardner sim UI");
        Require(worldCanvas != null, "Gardner sim world-space canvas was not created.");
        Require(worldCanvas.renderMode == RenderMode.WorldSpace, "Gardner sim UI is not world-space.");
        Require(worldCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() != null,
            "Gardner sim UI is missing its tracked-device raycaster.");

        string allText = string.Join(
            "\n",
            UnityEngine.Object.FindObjectsOfType<TMP_Text>(true)
                .Select(text => text.text));
        Require(allText.Contains("Gardner sim"), "The product title is not visible in the UI.");
        Require(allText.Contains("DESPRE PROIECT"), "The project explanation area is missing.");
        Require(allText.Contains("CE VREI SĂ CREȘTI?"), "The plant chooser is missing.");
        Require(allText.Contains("biomasă"), "Per-plant biomass status is missing.");
        Require(allText.Contains("Anotimpurile sunt numai la arțar"),
            "The maple-only seasons explanation is missing.");

        EventSystem[] activeEventSystems = UnityEngine.Object
            .FindObjectsOfType<EventSystem>(true)
            .Where(item => item.enabled && item.gameObject.activeInHierarchy)
            .ToArray();
        Require(activeEventSystems.Length == 1,
            $"Expected one active EventSystem, found {activeEventSystems.Length}.");
        EventSystem eventSystem = activeEventSystems[0];
        Require(eventSystem != null, "EventSystem is missing.");
        XRUIInputModule xrInputModule = eventSystem.GetComponent<XRUIInputModule>();
        Require(xrInputModule != null, "XR UI input module is missing.");
        Require(xrInputModule.enableXRInput, "XR input is disabled on the UI module.");
        Require(xrInputModule.trackedDeviceDragThresholdMultiplier <= 0.25f,
            "The tracked-device drag threshold is too large for HoloLens sliders.");

        Slider[] sliders = UnityEngine.Object.FindObjectsOfType<Slider>(true);
        HoloLensSliderInput[] sliderInputs =
            UnityEngine.Object.FindObjectsOfType<HoloLensSliderInput>(true);
        Require(sliders.Length >= 6, "The Gardner sim environment sliders are missing.");
        Require(sliderInputs.Length == sliders.Length,
            "At least one slider is missing its HoloLens pointer handler.");
        VerifySliderPointerMapping(sliderInputs[0], sliders[0], eventSystem);
        VerifyTemperatureSliderClamping(sliders);

        BaseInputModule[] activeModules = UnityEngine.Object
            .FindObjectsOfType<BaseInputModule>(true)
            .Where(item => item.enabled && item.gameObject.activeInHierarchy)
            .ToArray();
        Require(activeModules.Length == 1 && activeModules[0] == xrInputModule,
            "A competing UI input module is still active.");

        HoloLensSimulationPanel legacyXrPanel =
            maple.GetComponent<HoloLensSimulationPanel>();
        Require(legacyXrPanel == null || !legacyXrPanel.enabled,
            "The old maple XR panel is still enabled.");
        MapleSimulationPanel legacyMaplePanel =
            maple.GetComponent<MapleSimulationPanel>();
        Require(legacyMaplePanel == null || !legacyMaplePanel.enabled,
            "The old maple desktop panel is still enabled.");
        Require(maple.GetComponentsInChildren<ParticleSystem>(true).Length == 0,
            "Maple seasons must not use particles or snow.");

        Transform gardenContent = controller.transform.Find("Garden Content");
        Require(gardenContent != null,
            "Garden Content is missing, so ArUco cannot anchor both plants.");
        Require(maple.transform.IsChildOf(gardenContent)
                && sunflower.transform.IsChildOf(gardenContent),
            "Both simulations must share the ArUco-anchored Garden Content.");
        Branch mapleStem = maple.GetComponentInChildren<Branch>(true);
        ICI.PlantGrowth.MixedCrops.MixedCropFieldController sunflowerField =
            sunflower.GetComponent<ICI.PlantGrowth.MixedCrops.MixedCropFieldController>();
        Require(mapleStem != null && sunflowerField != null,
            "Relative plant dimensions cannot be verified.");
        float mapleMatureHeight = mapleStem.maxHeight * 2f
            * (1f + mapleStem.matureLengthIncrease);
        float matureHeightRatio = mapleMatureHeight
            / sunflowerField.SunflowerMatureHeightMeters;
        Require(matureHeightRatio >= 2f && matureHeightRatio <= 4f,
            $"Maple/sunflower mature height ratio is implausible: {matureHeightRatio:0.00}.");

        controller.SelectSunflower();
        Require(!maple.gameObject.activeInHierarchy, "Maple remains visible after choosing sunflower.");
        Require(sunflower.gameObject.activeInHierarchy, "Sunflower is hidden after choosing sunflower.");
        controller.SelectMaple();
        Require(maple.gameObject.activeInHierarchy, "Maple is hidden after choosing maple.");
        Require(!sunflower.gameObject.activeInHierarchy, "Sunflower remains visible after choosing maple.");
        controller.SelectBoth();
        Require(maple.gameObject.activeInHierarchy && sunflower.gameObject.activeInHierarchy,
            "Both plants are not visible after choosing Ambele.");

        SessionState.SetFloat(MapleDvsKey, maple.DevelopmentStage);
        SessionState.SetFloat(SunflowerDvsKey, sunflower.CurrentDevelopmentStage);
        controller.StartSelectedPlants();
        Require(maple.SimulationRunning, "Maple did not start.");
        Require(sunflower.IsRunning, "Sunflower did not start.");
    }

    private static void VerifySliderPointerMapping(
        HoloLensSliderInput sliderInput,
        Slider slider,
        EventSystem eventSystem)
    {
        RectTransform track = sliderInput.transform as RectTransform;
        Require(track != null, "The HoloLens slider track is not a RectTransform.");

        float originalValue = slider.value;
        float targetNormalizedValue = 0.82f;
        Vector3 targetLocalPoint = new Vector3(
            Mathf.Lerp(track.rect.xMin, track.rect.xMax, targetNormalizedValue),
            track.rect.center.y,
            0f);
        TrackedDeviceEventData pointer = new TrackedDeviceEventData(eventSystem)
        {
            pointerCurrentRaycast = new RaycastResult
            {
                gameObject = track.gameObject,
                worldPosition = track.TransformPoint(targetLocalPoint)
            }
        };

        sliderInput.OnInitializePotentialDrag(pointer);
        Require(!pointer.useDragThreshold,
            "The HoloLens slider still uses the mouse drag threshold.");
        sliderInput.OnPointerDown(pointer);
        Require(Mathf.Abs(slider.normalizedValue - targetNormalizedValue) < 0.02f,
            "A tracked pointer press does not update the slider value.");
        slider.value = originalValue;
    }

    private static void VerifyTemperatureSliderClamping(Slider[] sliders)
    {
        Slider minimum = sliders.FirstOrDefault(
            item => item.name == "Temperatură minimă");
        Slider maximum = sliders.FirstOrDefault(
            item => item.name == "Temperatură maximă");
        Require(minimum != null && maximum != null,
            "The minimum/maximum temperature sliders are missing.");

        minimum.value = 10f;
        maximum.value = 12f;
        minimum.value = 20f;
        Require(Mathf.Abs(minimum.value - 12f) < 0.001f,
            "The minimum temperature UI does not reflect its clamped value.");
        TMP_Text minimumValueText = minimum.transform.Find("Value")
            ?.GetComponent<TMP_Text>();
        Require(minimumValueText != null
                && minimumValueText.text.Contains("12.0"),
            "The displayed minimum temperature differs from the applied value.");

        maximum.value = 30f;
        minimum.value = 18f;
    }

    private static void BeginPauseFreezeCheck()
    {
        GardnerSimController controller = UnityEngine.Object
            .FindObjectOfType<GardnerSimController>(true);
        Plant maple = UnityEngine.Object.FindObjectOfType<Plant>(true);
        PlantPhenologySimulation sunflower = UnityEngine.Object
            .FindObjectOfType<PlantPhenologySimulation>(true);
        Require(controller != null && maple != null && sunflower != null,
            "Integrated simulations disappeared during Play Mode.");
        Require(maple.DevelopmentStage
                >= SessionState.GetFloat(MapleDvsKey, float.MinValue),
            "Maple DVS moved backwards after starting.");
        Require(sunflower.CurrentDevelopmentStage
                >= SessionState.GetFloat(SunflowerDvsKey, float.MinValue),
            "Sunflower DVS moved backwards after starting.");

        controller.PauseSelectedPlants();
        Require(!maple.SimulationRunning, "Maple did not pause.");
        Require(!sunflower.IsRunning, "Sunflower did not pause.");
        SessionState.SetFloat(MapleDvsKey, maple.DevelopmentStage);
        SessionState.SetFloat(
            SunflowerDvsKey,
            sunflower.CurrentDevelopmentStage);
        SaveMapleVisualState(PauseVisualPrefix, maple);
    }

    private static void VerifyPauseAndStartStalledDevelopment()
    {
        GardnerSimController controller = UnityEngine.Object
            .FindObjectOfType<GardnerSimController>(true);
        Plant maple = UnityEngine.Object.FindObjectOfType<Plant>(true);
        PlantPhenologySimulation sunflower = UnityEngine.Object
            .FindObjectOfType<PlantPhenologySimulation>(true);
        Require(controller != null && maple != null && sunflower != null,
            "Integrated simulations disappeared during the pause check.");
        RequireMapleVisualStateUnchanged(
            PauseVisualPrefix,
            maple,
            "Maple geometry or leaf growth continued while paused.");
        Require(Mathf.Abs(maple.DevelopmentStage
                - SessionState.GetFloat(MapleDvsKey, float.MinValue)) < 0.000001f,
            "Maple DVS advanced while paused.");

        SetTemperatureSliders(-10f, -10f);
        controller.StartSelectedPlants();
        Require(maple.SimulationRunning, "Maple did not resume.");
        Require(sunflower.IsRunning, "Sunflower did not resume.");
        Require(maple.DevelopmentStage
                >= SessionState.GetFloat(MapleDvsKey, float.MinValue),
            "Maple DVS reset while resuming.");
        Require(sunflower.CurrentDevelopmentStage
                >= SessionState.GetFloat(SunflowerDvsKey, float.MinValue),
            "Sunflower DVS reset while resuming.");
    }

    private static void CaptureStalledDevelopmentState()
    {
        Plant maple = UnityEngine.Object.FindObjectOfType<Plant>(true);
        Require(maple != null && maple.SimulationRunning,
            "Maple is not running during the zero-DVS test.");
        SessionState.SetFloat(MapleDvsKey, maple.DevelopmentStage);
        SaveMapleVisualState(StalledVisualPrefix, maple);
    }

    private static void VerifyStalledDevelopmentAndResume()
    {
        Plant maple = UnityEngine.Object.FindObjectOfType<Plant>(true);
        Require(maple != null && maple.SimulationRunning,
            "Maple stopped unexpectedly during the zero-DVS test.");
        Require(Mathf.Abs(maple.DevelopmentStage
                - SessionState.GetFloat(MapleDvsKey, float.MinValue)) < 0.000001f,
            "Maple DVS advanced at or below its base temperature.");
        RequireMapleVisualStateUnchanged(
            StalledVisualPrefix,
            maple,
            "Maple animation continued even though DVS was stationary.");

        SetTemperatureSliders(18f, 30f);
    }

    private static void VerifyResumeAndFinish()
    {
        GardnerSimController controller = UnityEngine.Object
            .FindObjectOfType<GardnerSimController>(true);
        Plant maple = UnityEngine.Object.FindObjectOfType<Plant>(true);
        PlantPhenologySimulation sunflower = UnityEngine.Object
            .FindObjectOfType<PlantPhenologySimulation>(true);
        Require(controller != null && maple != null && sunflower != null,
            "Integrated simulations disappeared after resume.");
        Require(maple.DevelopmentStage
                > SessionState.GetFloat(MapleDvsKey, float.MinValue),
            "Maple DVS did not resume after restoring viable temperatures.");
        RequireMapleVisualStateChanged(
            StalledVisualPrefix,
            maple,
            "Maple animation did not resume when DVS started advancing.");
        Require(sunflower.CurrentDevelopmentStage
                >= SessionState.GetFloat(SunflowerDvsKey, float.MinValue),
            "Sunflower DVS moved backwards after resume.");

        controller.PauseSelectedPlants();
        Require(!maple.SimulationRunning, "Maple did not pause after resume.");
        Require(!sunflower.IsRunning, "Sunflower did not pause after resume.");

        SessionState.SetString(PhaseKey, "verified");
        EditorApplication.ExitPlaymode();
    }

    private static void SetTemperatureSliders(
        float minimumTemperature,
        float maximumTemperature)
    {
        Slider[] sliders = UnityEngine.Object.FindObjectsOfType<Slider>(true);
        Slider minimum = sliders.FirstOrDefault(
            item => item.name == "Temperatură minimă");
        Slider maximum = sliders.FirstOrDefault(
            item => item.name == "Temperatură maximă");
        Require(minimum != null && maximum != null,
            "The temperature sliders disappeared during Play Mode.");

        if (minimumTemperature <= minimum.value)
        {
            minimum.value = minimumTemperature;
            maximum.value = maximumTemperature;
        }
        else
        {
            maximum.value = maximumTemperature;
            minimum.value = minimumTemperature;
        }

        Require(Mathf.Abs(minimum.value - minimumTemperature) < 0.001f
                && Mathf.Abs(maximum.value - maximumTemperature) < 0.001f,
            "The temperature slider pair did not apply the requested values.");
    }

    private static void SaveMapleVisualState(string prefix, Plant maple)
    {
        Branch[] branches = maple.GetComponentsInChildren<Branch>(true);
        Leaf[] leaves = maple.GetComponentsInChildren<Leaf>(true);
        SessionState.SetInt(prefix + "BranchCount", branches.Length);
        SessionState.SetInt(prefix + "LeafCount", leaves.Length);
        SessionState.SetFloat(
            prefix + "StemScale",
            branches
                .Where(branch => branch != null && branch.modelCyl != null)
                .Sum(branch => branch.modelCyl.localScale.sqrMagnitude));
        SessionState.SetFloat(
            prefix + "LeafGrowth",
            leaves.Where(leaf => leaf != null)
                .Sum(leaf => leaf.VisualGrowthProgress));
        SessionState.SetFloat(prefix + "GrowthClock", maple.DvsGrowthClock);
    }

    private static void RequireMapleVisualStateUnchanged(
        string prefix,
        Plant maple,
        string message)
    {
        Require(!MapleVisualStateChanged(prefix, maple, true), message);
    }

    private static void RequireMapleVisualStateChanged(
        string prefix,
        Plant maple,
        string message)
    {
        Require(MapleVisualStateChanged(prefix, maple, false), message);
    }

    private static bool MapleVisualStateChanged(
        string prefix,
        Plant maple,
        bool includeGrowthClock)
    {
        Branch[] branches = maple.GetComponentsInChildren<Branch>(true);
        Leaf[] leaves = maple.GetComponentsInChildren<Leaf>(true);
        float stemScale = branches
            .Where(branch => branch != null && branch.modelCyl != null)
            .Sum(branch => branch.modelCyl.localScale.sqrMagnitude);
        float leafGrowth = leaves.Where(leaf => leaf != null)
            .Sum(leaf => leaf.VisualGrowthProgress);

        return branches.Length != SessionState.GetInt(prefix + "BranchCount", -1)
            || leaves.Length != SessionState.GetInt(prefix + "LeafCount", -1)
            || Mathf.Abs(stemScale
                - SessionState.GetFloat(prefix + "StemScale", float.MinValue))
                    > 0.00001f
            || Mathf.Abs(leafGrowth
                - SessionState.GetFloat(prefix + "LeafGrowth", float.MinValue))
                    > 0.00001f
            || (includeGrowthClock
                && Mathf.Abs(maple.DvsGrowthClock
                    - SessionState.GetFloat(
                        prefix + "GrowthClock",
                        float.MinValue)) > 0.00001f);
    }

    private static void CapturePreview()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = UnityEngine.Object.FindObjectOfType<Camera>(true);
        }
        Require(camera != null, "No camera is available for the Gardner sim preview.");

        const int width = 1440;
        const int height = 900;
        RenderTexture renderTexture = new RenderTexture(width, height, 24);
        Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();
            screenshot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(
                "/tmp/gardner-sim-preview.png",
                screenshot.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            AppendError(condition + "\n" + stackTrace);
        }
    }

    private static void AppendError(string error)
    {
        string existing = SessionState.GetString(ErrorKey, string.Empty);
        SessionState.SetString(
            ErrorKey,
            string.IsNullOrEmpty(existing) ? error : existing + "\n" + error);
    }

    private static void Finish(bool verified)
    {
        DetachCallbacks();
        EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(
            PreviousOptionsKey,
            (int)EnterPlayModeOptions.None);
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(
            PreviousOptionsEnabledKey,
            false);

        string errors = SessionState.GetString(ErrorKey, string.Empty);
        bool exitWhenFinished = SessionState.GetBool(
            ExitWhenFinishedKey,
            false);
        SessionState.SetBool(ActiveKey, false);
        if (verified && string.IsNullOrEmpty(errors))
        {
            Debug.Log("[Gardner sim] Runtime verification passed: UI, plant selection, shared environment and both DVS simulations are operational.");
            if (exitWhenFinished)
            {
                EditorApplication.Exit(0);
            }
        }
        else
        {
            string details = string.IsNullOrEmpty(errors)
                ? "Runtime verification did not complete."
                : string.Join("\n", errors.Split('\n').Distinct());
            Debug.LogError("[Gardner sim] Runtime verification failed:\n" + details);
            if (exitWhenFinished)
            {
                EditorApplication.Exit(1);
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
