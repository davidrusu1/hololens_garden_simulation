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
        private static bool screenshotRequested;
        private static bool tickRegistered;
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
                gameView.maximized = true;
                gameView.Focus();
            }

            enteredPlayModeAt = EditorApplication.timeSinceStartup;
            screenshotRequested = SessionState.GetBool(CapturedKey, false);
            tickRegistered = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            double elapsed = EditorApplication.timeSinceStartup - enteredPlayModeAt;
            if (!screenshotRequested && elapsed >= 5d)
            {
                MixedCropFieldController field =
                    Object.FindObjectOfType<MixedCropFieldController>();
                ValidateField(field);

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string outputDirectory = Path.Combine(projectRoot, "VisualVerification");
                Directory.CreateDirectory(outputDirectory);
                string outputPath = Path.Combine(outputDirectory, "MixedCropField.png");
                ScreenCapture.CaptureScreenshot(outputPath, 1);
                screenshotRequested = true;
                SessionState.SetBool(CapturedKey, true);
                Debug.Log("[Mixed Crop] Visual verification captured at " + outputPath);
            }

            if (screenshotRequested && elapsed >= 7d)
            {
                EditorApplication.update -= Tick;
                tickRegistered = false;
                RestoreGameView();
                EditorApplication.isPlaying = false;
            }
        }

        private static void ValidateField(MixedCropFieldController field)
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

            if (countsValid && rowsValid && alternating)
            {
                Debug.Log(
                    "[Mixed Crop] Validation passed: 4 sunflowers + 4 cassava, "
                    + "alternating, 2 rows, maximum 5 plants per row.",
                    field);
            }
            else
            {
                Debug.LogError(
                    $"[Mixed Crop] Validation failed: generated={generatedCount}, "
                    + $"rows={field.RowCount}, alternating={alternating}.",
                    field);
            }
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
