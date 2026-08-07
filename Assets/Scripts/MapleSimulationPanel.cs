using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class MapleSimulationPanel : MonoBehaviour
{
    private const float MinimumTemperature = -10f;
    private const float MaximumTemperature = 45f;
    private const float ReferenceSecondsPerDay = 0.18f;

    [SerializeField]
    private Plant simulation;

    [SerializeField]
    private bool showRuntimePanel = true;

    private float minimumTemperature;
    private float maximumTemperature;
    private float dayLength;
    private float solarRadiation;
    private float soilWater;
    private float secondsPerDay;
    private float calendarSpeedMultiplier = 1f;
    private bool initialized;
    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle valueStyle;
    private GUIStyle statusStyle;
    private GUIStyle buttonStyle;
    private Vector2 panelScrollPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachPanel()
    {
        if (FindObjectOfType<GardnerSimController>(true) != null)
        {
            return;
        }

        Plant plant = FindObjectOfType<Plant>();
        if (plant != null
            && plant.GetComponent<MapleSimulationPanel>() == null)
        {
            MapleSimulationPanel panel =
                plant.gameObject.AddComponent<MapleSimulationPanel>();
            panel.simulation = plant;
        }
    }

    private void Awake()
    {
        ResolveSimulation();
        InitializeValues();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (simulation != null
            && keyboard != null
            && keyboard.spaceKey.wasPressedThisFrame)
        {
            simulation.RestartCompleteSimulation();
        }
    }

    private void ResolveSimulation()
    {
        if (simulation == null)
        {
            simulation = GetComponent<Plant>();
        }

        if (simulation == null)
        {
            simulation = FindObjectOfType<Plant>();
        }
    }

    private void InitializeValues()
    {
        if (initialized || simulation == null)
        {
            return;
        }

        minimumTemperature = simulation.AverageTemperature - 6f;
        maximumTemperature = simulation.AverageTemperature + 6f;
        dayLength = simulation.DayLengthHours;
        solarRadiation = simulation.GlobalRadiation;
        soilWater = simulation.RelativeSoilWater;
        secondsPerDay = simulation.SecondsPerSimulatedDay;
        calendarSpeedMultiplier = simulation.PlaybackSpeed;
        initialized = true;
    }

    private void ApplyPanelValues()
    {
        if (simulation == null)
        {
            return;
        }

        NormalizeTemperatureRange();
        float averageTemperature =
            (minimumTemperature + maximumTemperature) * 0.5f;
        simulation.SetEnvironment(
            averageTemperature,
            solarRadiation,
            soilWater,
            dayLength);
        simulation.SetSecondsPerSimulatedDay(
            secondsPerDay);
        simulation.SetPlaybackSpeed(calendarSpeedMultiplier);
    }

    private void OnGUI()
    {
        if (!showRuntimePanel)
        {
            return;
        }

        ResolveSimulation();
        InitializeValues();
        if (simulation == null)
        {
            return;
        }

        EnsureGuiStyles();
        Matrix4x4 previousMatrix = GUI.matrix;
        float uiScale = Mathf.Clamp(Screen.height / 900f, 0.85f, 1.35f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

        float availablePanelHeight = Mathf.Max(
            300f,
            Screen.height / uiScale - 36f);
        Rect panelRect = new Rect(
            18f,
            18f,
            420f,
            Mathf.Min(700f, availablePanelHeight));
        GUILayout.BeginArea(panelRect, GUI.skin.window);
        GUILayout.BeginVertical();
        panelScrollPosition = GUILayout.BeginScrollView(
            panelScrollPosition,
            false,
            true,
            GUILayout.ExpandHeight(true));
        GUILayout.Label("SIMULARE FENOLOGICĂ", titleStyle);
        GUILayout.Label("Arțar (Acer) · model WOFOST-inspired", sectionStyle);
        GUILayout.Space(8f);

        DrawPerformanceModeControl();
        GUILayout.Space(8f);

        GUILayout.Label("MEDIU ȘI VITEZĂ", sectionStyle);
        minimumTemperature = DrawSlider(
            "Temperatură minimă",
            minimumTemperature,
            MinimumTemperature,
            MaximumTemperature,
            "°C");
        maximumTemperature = DrawSlider(
            "Temperatură maximă",
            maximumTemperature,
            MinimumTemperature,
            MaximumTemperature,
            "°C");
        NormalizeTemperatureRange();
        GUILayout.Label(
            $"Temperatură medie folosită: {(minimumTemperature + maximumTemperature) * 0.5f:0.0} °C",
            statusStyle);
        dayLength = DrawSlider("Durata zilei", dayLength, 6f, 18f, "h");
        solarRadiation = DrawSlider(
            "Radiație solară",
            solarRadiation,
            0f,
            35f,
            "MJ/m²");
        soilWater = DrawSlider(
            "Apă disponibilă în sol",
            soilWater,
            0f,
            1f,
            "");
        secondsPerDay = DrawSlider(
            "Secunde / zi simulată",
            secondsPerDay,
            0.1f,
            3f,
            "s");
        calendarSpeedMultiplier = DrawSlider(
            "Multiplicator viteză",
            calendarSpeedMultiplier,
            0.25f,
            4f,
            "x");
        ApplyPanelValues();
        GUILayout.Label(
            $"Viteză calendar: {calendarSpeedMultiplier * ReferenceSecondsPerDay / Mathf.Max(0.1f, secondsPerDay):0.00}x   Morfologie: {calendarSpeedMultiplier:0.00}x",
            valueStyle);

        GUILayout.Space(8f);
        GUILayout.Label("STARE", sectionStyle);
        string state = simulation.DevelopmentStage >= 2f
            ? "Maturitate atinsă."
            : simulation.SimulationRunning
                ? "Dezvoltarea arțarului este în curs."
                : "Simularea numerică este oprită.";
        GUILayout.Label(state, statusStyle, GUILayout.MinHeight(38f));
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Ziua: {simulation.SimulatedDay}", valueStyle);
        GUILayout.Label(
            $"Anotimp: {simulation.CurrentSeasonLabel}",
            valueStyle);
        GUILayout.Label($"DVS: {simulation.DevelopmentStage:0.000}", valueStyle);
        GUILayout.EndHorizontal();
        GUILayout.Label(
            $"Stadiu structural: {simulation.StructuralStage}/4",
            valueStyle);

        GUILayout.Space(4f);
        GUILayout.Label("CREȘTERE WOFOST-LITE", sectionStyle);
        float totalBiomass = simulation.RootBiomass
            + simulation.LeafBiomass
            + simulation.WoodBiomass;
        float interceptedFraction = simulation.GlobalRadiation > 0.0001f
            ? simulation.InterceptedRadiation / simulation.GlobalRadiation
            : 0f;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Biomasă: {totalBiomass:0.00}", valueStyle);
        GUILayout.Label($"LAI: {simulation.LeafAreaIndex:0.00}", valueStyle);
        GUILayout.EndHorizontal();
        GUILayout.Label(
            $"Rădăcină {simulation.RootBiomass:0.00} · Lemn {simulation.WoodBiomass:0.00} · Frunze {simulation.LeafBiomass:0.00}",
            statusStyle);
        GUILayout.Label(
            $"Creștere netă: {simulation.NetAssimilation:0.000}/zi · Lumină interceptată: {interceptedFraction * 100f:0}%",
            statusStyle);
        GUILayout.Label(
            $"Asimilație: {simulation.GrossAssimilation:0.000} · Respirație: {simulation.MaintenanceRespiration:0.000}",
            statusStyle);
        GUILayout.Label(
            "Viteza modifică uniform calendarul, trunchiul, ramurile și frunzele, fără să schimbe regulile de formare.",
            statusStyle);
        GUILayout.EndScrollView();

        if (GUILayout.Button(
                "RESTART SIMULATION",
                buttonStyle,
                GUILayout.Height(46f)))
        {
            simulation.RestartCompleteSimulation();
        }

        GUILayout.Label(
            "M: setup nou/vechi · Space: restart · WASD: mișcare · ↑/↓: vertical",
            statusStyle);
        GUILayout.EndVertical();
        GUILayout.EndArea();
        GUI.matrix = previousMatrix;
    }

    public void SetRuntimePanelVisible(bool visible)
    {
        showRuntimePanel = visible;
    }

    private void DrawPerformanceModeControl()
    {
        GUILayout.Label("PERFORMANȚĂ", sectionStyle);
        bool liteModeWasEnabled = simulation.LiteModeEnabled;
        bool requestedLiteMode = GUILayout.Toggle(
            liteModeWasEnabled,
            liteModeWasEnabled
                ? "MOD LITE VIZUAL: ACTIV"
                : "MOD LITE VIZUAL: OPRIT",
            buttonStyle,
            GUILayout.Height(42f));
        if (requestedLiteMode != liteModeWasEnabled)
        {
            simulation.SetLiteMode(requestedLiteMode);
        }

        GUILayout.Label(
            requestedLiteMode
                ? "Lite: aceeași plantă, fără efectele vizuale costisitoare."
                : "Complet: aceeași plantă, cu toate efectele vizuale active.",
            statusStyle);
    }

    private float DrawSlider(
        string label,
        float value,
        float minimum,
        float maximum,
        string unit)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, valueStyle, GUILayout.Width(235f));
        GUILayout.Label($"{value:0.0} {unit}", valueStyle, GUILayout.Width(95f));
        GUILayout.EndHorizontal();
        return GUILayout.HorizontalSlider(value, minimum, maximum);
    }

    private void NormalizeTemperatureRange()
    {
        minimumTemperature = Mathf.Clamp(
            minimumTemperature,
            MinimumTemperature,
            MaximumTemperature);
        maximumTemperature = Mathf.Clamp(
            maximumTemperature,
            MinimumTemperature,
            MaximumTemperature);
        if (minimumTemperature > maximumTemperature)
        {
            maximumTemperature = minimumTemperature;
        }
    }

    private void EnsureGuiStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.12f, 0.34f, 0.12f) }
        };
        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.15f, 0.27f, 0.15f) }
        };
        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = Color.black }
        };
        statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true,
            normal = { textColor = new Color(0.12f, 0.12f, 0.12f) }
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.05f, 0.2f, 0.05f) }
        };
    }
}
