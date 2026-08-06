using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MapleSeasonRuntimeVerifier
{
    private const string ActiveKey = "MapleSeasonVerifier.Active";
    private const string PhaseKey = "MapleSeasonVerifier.Phase";
    private const string ErrorKey = "MapleSeasonVerifier.Error";
    private const string FrameKey = "MapleSeasonVerifier.Frame";
    private const string PreviousOptionsEnabledKey =
        "MapleSeasonVerifier.PreviousOptionsEnabled";
    private const string PreviousOptionsKey =
        "MapleSeasonVerifier.PreviousOptions";

    private static GameObject testRoot;
    private static Leaf testLeaf;
    private static float leafStartHeight;
    private static float leafFallStartedAt;

    static MapleSeasonRuntimeVerifier()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            EditorApplication.delayCall += ResumeAfterAssemblyReload;
        }
    }

    [MenuItem("Tools/Maple/Verify DVS Seasons")]
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
        SessionState.SetBool(
            "MapleSeasonVerifier.ExitWhenFinished",
            exitWhenFinished);
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
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
        if (!EditorApplication.isPlaying
            && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Finish(phase == "verified");
            return;
        }

        AttachCallbacks();
    }

    private static void AttachCallbacks()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void DetachCallbacks()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
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
            Finish(SessionState.GetString(PhaseKey, string.Empty)
                == "verified");
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

        int frame = SessionState.GetInt(FrameKey, 0) + 1;
        SessionState.SetInt(FrameKey, frame);
        try
        {
            if (frame == 8)
            {
                VerifyDvsSeasonsAndStartLeafFall();
            }
            else if (testLeaf != null
                && Time.realtimeSinceStartup - leafFallStartedAt >= 0.35f)
            {
                VerifyVisibleLeafFallAndFinish();
            }
            else if (frame > 5000)
            {
                throw new InvalidOperationException(
                    "Timed out while waiting for the maple leaf to fall.");
            }
        }
        catch (Exception exception)
        {
            SessionState.SetString(ErrorKey, exception.ToString());
            SessionState.SetString(PhaseKey, "failed");
            EditorApplication.ExitPlaymode();
        }
    }

    private static void VerifyDvsSeasonsAndStartLeafFall()
    {
        testRoot = new GameObject("Maple Season Verification");
        Plant plant = testRoot.AddComponent<Plant>();
        plant.ResetSimulation();
        Require(plant.CurrentSeason == MapleSeason.Spring,
            "Maple does not start in spring.");

        GameObject leafObject = new GameObject("Verification Maple Leaf");
        leafObject.transform.SetParent(testRoot.transform, false);
        testLeaf = leafObject.AddComponent<Leaf>();
        testLeaf.Initialize(
            0.45f,
            0.36f,
            0.025f,
            0.05f,
            new Color(0.10f, 0.48f, 0.12f, 1f),
            new Color(0.18f, 0.30f, 0.08f, 1f));
        testLeaf.GetComponent<LeafLightExposure>()
            ?.PrepareForSeasonalFall();

        bool sawSummer = false;
        bool sawAutumn = false;
        int safety = 0;
        while (plant.DevelopmentStage < 1.82f && safety++ < 300)
        {
            plant.SimulateOneDay();
            sawSummer |= plant.CurrentSeason == MapleSeason.Summer;
            sawAutumn |= plant.CurrentSeason == MapleSeason.Autumn;
        }

        Require(sawSummer, "Maple skipped the summer DVS interval.");
        Require(sawAutumn, "Maple did not enter autumn at the configured DVS.");
        Require(plant.AutumnColorProgress > 0f,
            "Autumn did not change the leaf colour progress.");
        Renderer bladeRenderer = testLeaf.transform
            .Find("Leaf Blade")
            ?.GetComponent<Renderer>();
        Require(bladeRenderer != null,
            "The procedural maple leaf blade is missing.");
        MaterialPropertyBlock seasonalColor = new MaterialPropertyBlock();
        bladeRenderer.GetPropertyBlock(seasonalColor);
        Color autumnColor = seasonalColor.GetColor(
            Shader.PropertyToID("_BaseColor"));
        Require(autumnColor.r > autumnColor.g * 2f,
            "The maple leaf did not become visibly red in autumn.");

        while (plant.DevelopmentStage < 2f && safety++ < 500)
        {
            plant.SimulateOneDay();
        }

        Require(plant.DevelopmentStage >= 2f,
            "Maple did not reach the winter DVS during verification.");
        Require(plant.CurrentSeason == MapleSeason.Winter,
            "Maple did not enter winter.");
        Require(plant.LeafFallProgress >= 0.999f,
            "Leaf fall did not complete at DVS 2.");
        Require(testLeaf != null && testLeaf.SeasonalFallStarted,
            "The maple leaf did not begin falling.");
        Require(testLeaf.transform.parent == null,
            "The falling leaf is still attached to the maple branch.");
        leafStartHeight = testLeaf.transform.position.y;
        leafFallStartedAt = Time.realtimeSinceStartup;
    }

    private static void VerifyVisibleLeafFallAndFinish()
    {
        Require(testLeaf != null,
            "The falling leaf disappeared before its animation was visible.");
        Require(testLeaf.transform.position.y < leafStartHeight - 0.01f,
            "The red maple leaf did not move downward during its fall.");

        if (testRoot != null)
        {
            UnityEngine.Object.Destroy(testRoot);
        }
        if (testLeaf != null)
        {
            UnityEngine.Object.Destroy(testLeaf.gameObject);
        }

        SessionState.SetString(PhaseKey, "verified");
        EditorApplication.ExitPlaymode();
    }

    private static void Finish(bool verified)
    {
        DetachCallbacks();
        EditorSettings.enterPlayModeOptions =
            (EnterPlayModeOptions)SessionState.GetInt(
                PreviousOptionsKey,
                (int)EnterPlayModeOptions.None);
        EditorSettings.enterPlayModeOptionsEnabled =
            SessionState.GetBool(PreviousOptionsEnabledKey, false);

        string error = SessionState.GetString(ErrorKey, string.Empty);
        bool exitWhenFinished = SessionState.GetBool(
            "MapleSeasonVerifier.ExitWhenFinished",
            false);
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetString(PhaseKey, string.Empty);

        if (verified && string.IsNullOrEmpty(error))
        {
            Debug.Log(
                "[Maple seasons] Runtime verification passed: DVS advances "
                + "spring, summer, autumn and winter; leaves redden and fall.");
            if (exitWhenFinished)
            {
                EditorApplication.Exit(0);
            }
            return;
        }

        Debug.LogError("[Maple seasons] Runtime verification failed:\n" + error);
        if (exitWhenFinished)
        {
            EditorApplication.Exit(1);
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
