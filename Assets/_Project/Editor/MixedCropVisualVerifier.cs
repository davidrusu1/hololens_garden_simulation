using System.IO;
using ICI.PlantGrowth.MixedCrops;
using ICI.PlantGrowth.Phenology;
using UnityEditor;
using UnityEngine;

namespace ICI.PlantGrowth.Editor
{
    /// <summary>
    /// Creates a repeatable Play-mode proof with two alternating rows (4 + 4)
    /// and captures the Game view after both plant types reach maturity.
    /// </summary>
    [InitializeOnLoad]
    public static class MixedCropVisualVerifier
    {
        private const string PendingKey = "ICI.MixedCrop.VisualVerification.Pending";
        private const string CapturedKey = "ICI.MixedCrop.VisualVerification.Captured";
        private static double enteredPlayModeAt;
        private static double screenshotRequestedAt;
        private static bool screenshotRequested;
        private static bool tickRegistered;
        private static bool checkedDaySixty;
        private static bool stemsGrowingAtDaySixty;
        private static bool checkedDvsOne;
        private static bool flowersStartedAtDvsOne;
        private static EditorWindow gameView;

        static MixedCropVisualVerifier()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += ResumePendingVerification;
        }

        [MenuItem("Tools/Plant AR/Verify Mixed Crop Field")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            ClearEditorConsole();
            ConfigurePlantDemoScene.Configure();
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(CapturedKey, false);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                StartVerificationTimer();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                tickRegistered = false;
                SessionState.SetBool(PendingKey, false);
                SessionState.SetBool(CapturedKey, false);
                RestoreGameView();
            }
        }

        private static void ResumePendingVerification()
        {
            if (SessionState.GetBool(PendingKey, false) && EditorApplication.isPlaying)
            {
                StartVerificationTimer();
            }
        }

        private static void StartVerificationTimer()
        {
            if (!SessionState.GetBool(PendingKey, false) || tickRegistered)
            {
                return;
            }

            PlantPhenologySimulation simulation =
                Object.FindObjectOfType<PlantPhenologySimulation>();
            if (simulation != null)
            {
                var serializedSimulation = new SerializedObject(simulation);
                serializedSimulation.FindProperty("requestedSunflowerCount").intValue = 4;
                serializedSimulation.FindProperty("requestedCassavaCount").intValue = 4;
                serializedSimulation.FindProperty("secondsPerSimulatedDay").floatValue = 0.1f;
                serializedSimulation.FindProperty("visualGrowthSpeed").floatValue = 4f;
                serializedSimulation.ApplyModifiedPropertiesWithoutUndo();
                simulation.StartSimulation();
            }

            System.Type gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null)
            {
                gameView = EditorWindow.GetWindow(gameViewType);
                gameView.Focus();
            }

            enteredPlayModeAt = EditorApplication.timeSinceStartup;
            screenshotRequested = SessionState.GetBool(CapturedKey, false);
            screenshotRequestedAt = screenshotRequested
                ? EditorApplication.timeSinceStartup
                : 0d;
            checkedDaySixty = false;
            stemsGrowingAtDaySixty = false;
            checkedDvsOne = false;
            flowersStartedAtDvsOne = false;
            tickRegistered = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            double elapsed = EditorApplication.timeSinceStartup - enteredPlayModeAt;
            MixedCropFieldController field =
                Object.FindObjectOfType<MixedCropFieldController>();
            PlantPhenologySimulation simulation =
                Object.FindObjectOfType<PlantPhenologySimulation>();

            if (!checkedDaySixty
                && field != null
                && simulation != null
                && simulation.CurrentDay >= 60)
            {
                checkedDaySixty = true;
                stemsGrowingAtDaySixty = field.SunflowerStemsStillGrowing;
                Debug.Log(
                    $"[Mixed Crop] Day 60 check: sunflower still growing="
                    + $"{stemsGrowingAtDaySixty}.",
                    field);
            }

            if (!checkedDvsOne
                && field != null
                && simulation != null
                && simulation.CurrentDevelopmentStage >= 1f)
            {
                checkedDvsOne = true;
                flowersStartedAtDvsOne = field.SunflowerFlowersStarted;
                Debug.Log(
                    $"[Mixed Crop] DVS 1 check on day {simulation.CurrentDay}: "
                    + $"flowers started={flowersStartedAtDvsOne}.",
                    field);
            }

            bool reachedComparisonDay = field != null
                && field.CurrentSimulationDay >= 120f;
            // The first HDRP run can spend more than ten seconds compiling
            // shaders while biological time is deliberately clamped. Wait for
            // the comparison day and keep a generous watchdog only for a truly
            // stalled verification.
            if (!screenshotRequested && (reachedComparisonDay || elapsed >= 60d))
            {
                ValidateField(field, simulation);

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string outputDirectory = Path.Combine(projectRoot, "VisualVerification");
                Directory.CreateDirectory(outputDirectory);
                string outputPath = Path.Combine(outputDirectory, "MixedCropField.png");
                ScreenCapture.CaptureScreenshot(outputPath, 1);
                screenshotRequested = true;
                screenshotRequestedAt = EditorApplication.timeSinceStartup;
                SessionState.SetBool(CapturedKey, true);
                Debug.Log("[Mixed Crop] Visual verification captured at " + outputPath);
            }

            if (screenshotRequested
                && EditorApplication.timeSinceStartup - screenshotRequestedAt >= 2d)
            {
                EditorApplication.update -= Tick;
                tickRegistered = false;
                RestoreGameView();
                EditorApplication.isPlaying = false;
            }
        }

        private static void ValidateField(
            MixedCropFieldController field,
            PlantPhenologySimulation simulation)
        {
            if (field == null)
            {
                Debug.LogError("[Mixed Crop] Validation failed: field controller is missing.");
                return;
            }

            Transform generated = field.transform.Find("Generated Mixed Crop Field");
            int generatedCount = generated != null ? generated.childCount : 0;
            bool countsValid = field.SunflowerCount == 4
                && field.CassavaCount == 4
                && generatedCount == 8;
            bool rowsValid = field.RowCount == 2;
            bool alternating = generated != null && IsAlternating(generated);
            bool realScaleValid = field.SunflowerMatureHeightMeters
                    > field.CassavaMatureHeightMeters
                && Mathf.Approximately(field.SunflowerMatureHeightMeters, 2.35f)
                && Mathf.Approximately(field.CassavaMatureHeightMeters, 1.85f);
            bool relativeTimeValid = field.CurrentSimulationDay >= 115f
                && field.CurrentSimulationDay <= 135f
                && field.CassavaCanopyProgress > 0.65f
                && field.CassavaCanopyProgress < 0.95f;
            MeasurePlantHeights(
                generated,
                out float measuredSunflowerHeight,
                out float measuredCassavaHeight);
            float dvsBeforeReapply = field.SunflowerDevelopmentStage;
            field.SetSunflowerDevelopmentStage(dvsBeforeReapply);
            MeasurePlantHeights(
                generated,
                out float repeatedSunflowerHeight,
                out float repeatedCassavaHeight);
            bool dvsClockValid = simulation != null
                && Mathf.Abs(
                    field.SunflowerDevelopmentStage
                    - simulation.CurrentDevelopmentStage) <= 0.01f
                && Mathf.Abs(
                    repeatedSunflowerHeight
                    - measuredSunflowerHeight) <= 0.001f
                && Mathf.Abs(
                    repeatedCassavaHeight
                    - measuredCassavaHeight) <= 0.001f;
            bool renderedScaleValid = Mathf.Abs(
                    measuredSunflowerHeight - field.SunflowerMatureHeightMeters) <= 0.15f
                && Mathf.Abs(
                    measuredCassavaHeight - field.CassavaMatureHeightMeters) <= 0.15f
                && measuredSunflowerHeight > measuredCassavaHeight;
            bool sunflowerTimelineValid = checkedDaySixty
                && stemsGrowingAtDaySixty
                && checkedDvsOne
                && flowersStartedAtDvsOne
                && field.SunflowerFlowersComplete;
            float expectedBiomassPerPlant = simulation != null
                ? simulation.TotalDryMatter
                    * 1000f
                    / Mathf.Max(1f, simulation.SunflowerPlantDensityPerHectare)
                : 0f;
            bool perPlantBiomassValid = simulation != null
                && Mathf.Approximately(
                    simulation.SunflowerPlantDensityPerHectare,
                    50000f)
                && simulation.TotalDryMatterPerPlantGrams > 0f
                && Mathf.Approximately(
                    simulation.TotalDryMatterPerPlantGrams,
                    expectedBiomassPerPlant);
            string visualMetrics = DescribeVisualMetrics(generated);

            if (countsValid
                && rowsValid
                && alternating
                && realScaleValid
                && renderedScaleValid
                && sunflowerTimelineValid
                && dvsClockValid
                && perPlantBiomassValid
                && relativeTimeValid)
            {
                Debug.Log(
                    "[Mixed Crop] Validation passed: 4 sunflowers + 4 cassava, "
                    + $"alternating, 2 rows, maximum 5 plants per row, day {field.CurrentSimulationDay:0}, "
                    + $"mature heights {field.SunflowerMatureHeightMeters:0.00} m / "
                    + $"{field.CassavaMatureHeightMeters:0.00} m, cassava canopy "
                    + $"{field.CassavaCanopyProgress * 100f:0}%, sunflower still growing "
                    + $"on day 60 and flowering started at DVS 1, sunflower biomass "
                    + $"{simulation.TotalDryMatterPerPlantGrams:0.0} g dry matter per plant. "
                    + "Sunflower animation is synchronized and stable at unchanged DVS. "
                    + visualMetrics,
                    field);
            }
            else
            {
                Debug.LogError(
                    $"[Mixed Crop] Validation failed: generated={generatedCount}, "
                    + $"rows={field.RowCount}, alternating={alternating}, "
                    + $"realScale={realScaleValid}, renderedScale={renderedScaleValid}, "
                    + $"sunflowerTimeline={sunflowerTimelineValid} "
                    + $"(day60={stemsGrowingAtDaySixty}, DVS1={flowersStartedAtDvsOne}, "
                    + $"flowerComplete={field.SunflowerFlowersComplete}), "
                    + $"dvsClock={dvsClockValid}, "
                    + $"biomassPerPlant={perPlantBiomassValid}, "
                    + $"relativeTime={relativeTimeValid}, "
                    + $"day={field.CurrentSimulationDay:0.0}, "
                    + $"cassavaCanopy={field.CassavaCanopyProgress:0.00}. {visualMetrics}",
                    field);
            }
        }

        private static void MeasurePlantHeights(
            Transform generated,
            out float sunflowerHeight,
            out float cassavaHeight)
        {
            sunflowerHeight = 0f;
            cassavaHeight = 0f;
            if (generated == null)
            {
                return;
            }

            for (int index = 0; index < generated.childCount; index++)
            {
                Transform plant = generated.GetChild(index);
                bool hasBounds = false;
                Bounds bounds = default;
                foreach (Renderer renderer in plant.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                if (!hasBounds)
                {
                    continue;
                }

                if (plant.name.Contains("Floarea-soarelui"))
                {
                    sunflowerHeight = Mathf.Max(sunflowerHeight, bounds.size.y);
                }
                else if (plant.name.Contains("Cassava"))
                {
                    cassavaHeight = Mathf.Max(cassavaHeight, bounds.size.y);
                }
            }
        }

        private static string DescribeVisualMetrics(Transform generated)
        {
            if (generated == null)
            {
                return "No generated field.";
            }

            Camera camera = Camera.main;
            int rendererCount = 0;
            Bounds fieldBounds = default;
            bool hasBounds = false;
            float sunflowerHeight = 0f;
            float cassavaHeight = 0f;

            for (int plantIndex = 0; plantIndex < generated.childCount; plantIndex++)
            {
                Transform plant = generated.GetChild(plantIndex);
                Bounds plantBounds = default;
                bool hasPlantBounds = false;
                foreach (Renderer renderer in plant.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }

                    rendererCount++;
                    if (!hasPlantBounds)
                    {
                        plantBounds = renderer.bounds;
                        hasPlantBounds = true;
                    }
                    else
                    {
                        plantBounds.Encapsulate(renderer.bounds);
                    }
                }

                if (!hasPlantBounds)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    fieldBounds = plantBounds;
                    hasBounds = true;
                }
                else
                {
                    fieldBounds.Encapsulate(plantBounds);
                }

                if (plant.name.Contains("Floarea-soarelui"))
                {
                    sunflowerHeight = Mathf.Max(sunflowerHeight, plantBounds.size.y);
                }
                else if (plant.name.Contains("Cassava"))
                {
                    cassavaHeight = Mathf.Max(cassavaHeight, plantBounds.size.y);
                }
            }

            Vector3 viewport = camera != null && hasBounds
                ? camera.WorldToViewportPoint(fieldBounds.center)
                : Vector3.zero;
            return $"Renderers={rendererCount}, visible heights="
                + $"{sunflowerHeight:0.00}/{cassavaHeight:0.00} m, "
                + $"camera viewport=({viewport.x:0.00},{viewport.y:0.00},{viewport.z:0.00}).";
        }

        private static bool IsAlternating(Transform generated)
        {
            bool? previousWasSunflower = null;
            for (int index = 0; index < generated.childCount; index++)
            {
                bool isSunflower = generated.GetChild(index).name.Contains("Floarea-soarelui");
                if (previousWasSunflower.HasValue
                    && previousWasSunflower.Value == isSunflower)
                {
                    return false;
                }

                previousWasSunflower = isSunflower;
            }

            return true;
        }

        private static void RestoreGameView()
        {
            if (gameView != null)
            {
                gameView.maximized = false;
                gameView = null;
            }
        }

        private static void ClearEditorConsole()
        {
            System.Type logEntries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor");
            System.Reflection.MethodInfo clearMethod = logEntries?.GetMethod(
                "Clear",
                System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public);
            clearMethod?.Invoke(null, null);
        }
    }
}
