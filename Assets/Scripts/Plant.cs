using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Plant : MonoBehaviour
{
    [Header("WOFOST-inspired time step")]
    [SerializeField]
    private bool startAutomatically = true;

    [SerializeField, Min(0.02f)]
    private float secondsPerSimulatedDay = 0.18f;

    [SerializeField, Range(0.25f, 4f)]
    private float playbackSpeed = 1f;

    [Header("Weather and soil")]
    [SerializeField, Range(-10f, 45f)]
    private float averageTemperature = 18f;

    [SerializeField, Min(0f)]
    private float globalRadiation = 16f;

    [SerializeField, Range(0f, 1f)]
    private float relativeSoilWater = 0.78f;

    [SerializeField, Range(6f, 18f)]
    private float dayLengthHours = 14.5f;

    [Header("Maple phenology")]
    [SerializeField, Range(-5f, 15f)]
    private float baseDevelopmentTemperature = 5f;

    [SerializeField, Min(1f)]
    private float thermalTimeBudBurstToCrownExpansion = 520f;

    [SerializeField, Min(1f)]
    private float thermalTimeCrownExpansionToMaturity = 780f;

    [Header("Assimilation and respiration")]
    [SerializeField, Range(0.01f, 0.5f)]
    private float lightUseEfficiency = 0.075f;

    [SerializeField, Range(0.4f, 1.2f)]
    private float broadleafExtinctionCoefficient = 0.78f;

    [SerializeField, Range(0.001f, 0.08f)]
    private float maintenanceRespirationFraction = 0.018f;

    [SerializeField, Range(0.2f, 1f)]
    private float biomassConversionEfficiency = 0.72f;

    [Header("Initial biomass")]
    [SerializeField, Min(0.001f)]
    private float initialRootBiomass = 0.22f;

    [SerializeField, Min(0.001f)]
    private float initialLeafBiomass = 0.16f;

    [SerializeField, Min(0.001f)]
    private float initialWoodBiomass = 0.34f;

    [Header("Visual performance")]
    [SerializeField]
    private bool liteModeEnabled = true;

    [Header("Runtime WOFOST state")]
    [SerializeField]
    private bool simulationRunning;

    [SerializeField]
    private int simulatedDay;

    [SerializeField, Range(0f, 2f)]
    private float developmentStage;

    [SerializeField]
    private int structuralStage = 1;

    [SerializeField]
    private float rootBiomass;

    [SerializeField]
    private float leafBiomass;

    [SerializeField]
    private float woodBiomass;

    [SerializeField]
    private float senescedLeafBiomass;

    [SerializeField]
    private float leafAreaIndex;

    [SerializeField]
    private float interceptedRadiation;

    [SerializeField]
    private float grossAssimilation;

    [SerializeField]
    private float maintenanceRespiration;

    [SerializeField]
    private float netAssimilation;

    [SerializeField]
    private float dailyLeafGrowth;

    [SerializeField]
    private float dailyWoodGrowth;

    [SerializeField]
    private float dailyRootGrowth;

    private Coroutine simulationRoutine;

    public bool SimulationRunning => simulationRunning;
    public int SimulatedDay => simulatedDay;
    public float DevelopmentStage => developmentStage;
    public int StructuralStage => structuralStage;
    public float RootBiomass => rootBiomass;
    public float LeafBiomass => leafBiomass;
    public float WoodBiomass => woodBiomass;
    public float LeafAreaIndex => leafAreaIndex;
    public float NetAssimilation => netAssimilation;
    public float GrossAssimilation => grossAssimilation;
    public float MaintenanceRespiration => maintenanceRespiration;
    public float InterceptedRadiation => interceptedRadiation;
    public float AverageTemperature => averageTemperature;
    public float GlobalRadiation => globalRadiation;
    public float RelativeSoilWater => relativeSoilWater;
    public float DayLengthHours => dayLengthHours;
    public float SecondsPerSimulatedDay => secondsPerSimulatedDay;
    public float PlaybackSpeed => playbackSpeed;
    public bool LiteModeEnabled => liteModeEnabled;
    public float BiologicalStructuralGrowthRateMultiplier { get; private set; } = 1f;
    public float BiologicalLeafGrowthRateMultiplier { get; private set; } = 1f;
    public float StructuralGrowthRateMultiplier =>
        BiologicalStructuralGrowthRateMultiplier * playbackSpeed;
    public float LeafGrowthRateMultiplier =>
        BiologicalLeafGrowthRateMultiplier * playbackSpeed;

    private void Start()
    {
        ApplyVisualPerformanceMode();
        ResetSimulation();
        if (startAutomatically)
        {
            StartSimulation();
        }
    }

    public void StartSimulation()
    {
        if (simulationRoutine != null)
        {
            return;
        }

        simulationRunning = true;
        simulationRoutine = StartCoroutine(RunDailySimulation());
    }

    public void RestartNumericalSimulation()
    {
        ResetSimulation();
        StartSimulation();
    }

    public void RestartCompleteSimulation()
    {
        ApplyVisualPerformanceMode();
        Branch[] stems = GetComponentsInChildren<Branch>(true);
        for (int index = 0; index < stems.Length; index++)
        {
            if (stems[index] != null && stems[index].currGen == 0)
            {
                stems[index].RestartVisualGrowthPreservingShape();
                break;
            }
        }

        RestartNumericalSimulation();
    }

    public void SetLiteMode(bool enabled)
    {
        if (liteModeEnabled == enabled)
        {
            return;
        }

        liteModeEnabled = enabled;
        ApplyVisualPerformanceMode();
    }

    public void PauseSimulation()
    {
        simulationRunning = false;
        if (simulationRoutine != null)
        {
            StopCoroutine(simulationRoutine);
            simulationRoutine = null;
        }
    }

    public void ResetSimulation()
    {
        PauseSimulation();
        simulatedDay = 0;
        developmentStage = 0f;
        structuralStage = 1;
        rootBiomass = initialRootBiomass;
        leafBiomass = initialLeafBiomass;
        woodBiomass = initialWoodBiomass;
        senescedLeafBiomass = 0f;
        leafAreaIndex = Mathf.Max(0.05f, leafBiomass * 1.8f);
        interceptedRadiation = 0f;
        grossAssimilation = 0f;
        maintenanceRespiration = 0f;
        netAssimilation = 0f;
        dailyLeafGrowth = 0f;
        dailyWoodGrowth = 0f;
        dailyRootGrowth = 0f;
        BiologicalStructuralGrowthRateMultiplier = 0.45f;
        BiologicalLeafGrowthRateMultiplier = 0.55f;
    }

    public void SetEnvironment(
        float temperature,
        float radiation,
        float soilWater,
        float dayLength)
    {
        averageTemperature = Mathf.Clamp(temperature, -10f, 45f);
        globalRadiation = Mathf.Max(0f, radiation);
        relativeSoilWater = Mathf.Clamp01(soilWater);
        dayLengthHours = Mathf.Clamp(dayLength, 6f, 18f);
    }

    public void SetSecondsPerSimulatedDay(float seconds)
    {
        secondsPerSimulatedDay = Mathf.Max(0.02f, seconds);
    }

    public void SetPlaybackSpeed(float multiplier)
    {
        playbackSpeed = Mathf.Clamp(multiplier, 0.25f, 4f);
    }

    private void ApplyVisualPerformanceMode()
    {
        PlantVisualQuality.SetLiteMode(liteModeEnabled, gameObject);
    }

    public void SimulateOneDay()
    {
        simulatedDay++;
        UpdatePhenology();

        float temperatureResponse = CalculateTemperatureResponse();
        float waterStress = Mathf.SmoothStep(0.15f, 1f, relativeSoilWater);
        float photoperiodResponse = Mathf.Clamp01(
            (dayLengthHours - 8f) / 5f);
        float effectiveLeafArea = Mathf.Max(0.02f, leafAreaIndex);
        interceptedRadiation = globalRadiation
            * (1f - Mathf.Exp(
                -broadleafExtinctionCoefficient * effectiveLeafArea));
        grossAssimilation = interceptedRadiation
            * lightUseEfficiency
            * temperatureResponse
            * waterStress
            * photoperiodResponse;

        float livingBiomass = rootBiomass + leafBiomass + woodBiomass;
        float respirationTemperatureFactor = Mathf.Pow(
            2f,
            (averageTemperature - 20f) / 10f);
        maintenanceRespiration = Mathf.Min(
            grossAssimilation,
            livingBiomass
                * maintenanceRespirationFraction
                * Mathf.Max(0.25f, respirationTemperatureFactor));
        netAssimilation = Mathf.Max(
            0f,
            grossAssimilation - maintenanceRespiration);

        CalculatePartitioning(
            developmentStage,
            out float rootFraction,
            out float leafFraction,
            out float woodFraction);
        float structuralDryMatter = netAssimilation
            * biomassConversionEfficiency;
        dailyRootGrowth = structuralDryMatter * rootFraction;
        dailyLeafGrowth = structuralDryMatter * leafFraction;
        dailyWoodGrowth = structuralDryMatter * woodFraction;

        float ageSenescence = developmentStage > 1.55f
            ? Mathf.InverseLerp(1.55f, 2f, developmentStage) * 0.025f
            : 0f;
        float waterSenescence = relativeSoilWater < 0.25f
            ? (0.25f - relativeSoilWater) * 0.08f
            : 0f;
        float shadeSenescence = leafAreaIndex > 5.5f
            ? (leafAreaIndex - 5.5f) * 0.004f
            : 0f;
        float leafLoss = Mathf.Min(
            leafBiomass,
            leafBiomass
                * (ageSenescence + waterSenescence + shadeSenescence));

        rootBiomass += dailyRootGrowth;
        woodBiomass += dailyWoodGrowth;
        leafBiomass = Mathf.Max(
            0f,
            leafBiomass + dailyLeafGrowth - leafLoss);
        senescedLeafBiomass += leafLoss;

        float specificLeafArea = Mathf.Lerp(
            1.9f,
            1.15f,
            Mathf.Clamp01(developmentStage / 2f));
        leafAreaIndex = Mathf.Clamp(
            leafBiomass * specificLeafArea,
            0f,
            8f);

        float normalizedWoodGrowth = dailyWoodGrowth
            / Mathf.Max(0.01f, livingBiomass * 0.35f);
        BiologicalStructuralGrowthRateMultiplier = Mathf.Lerp(
            0.35f,
            1.65f,
            Mathf.Clamp01(normalizedWoodGrowth));
        float normalizedLeafGrowth = dailyLeafGrowth
            / Mathf.Max(0.01f, leafBiomass * 0.45f);
        BiologicalLeafGrowthRateMultiplier = Mathf.Lerp(
            0.4f,
            1.5f,
            Mathf.Clamp01(normalizedLeafGrowth));
    }

    private IEnumerator RunDailySimulation()
    {
        while (simulationRunning)
        {
            SimulateOneDay();
            if (developmentStage >= 2f)
            {
                simulationRunning = false;
                break;
            }

            float elapsedSimulationTime = 0f;
            while (simulationRunning
                && elapsedSimulationTime < secondsPerSimulatedDay)
            {
                elapsedSimulationTime += Time.unscaledDeltaTime
                    * playbackSpeed;
                yield return null;
            }
        }

        simulationRoutine = null;
    }

    private void UpdatePhenology()
    {
        float effectiveTemperature = Mathf.Max(
            0f,
            averageTemperature - baseDevelopmentTemperature);
        if (developmentStage < 1f)
        {
            developmentStage = Mathf.Min(
                1f,
                developmentStage
                    + effectiveTemperature
                    / thermalTimeBudBurstToCrownExpansion);
        }
        else if (developmentStage < 2f)
        {
            developmentStage = Mathf.Min(
                2f,
                developmentStage
                    + effectiveTemperature
                    / thermalTimeCrownExpansionToMaturity);
        }

        structuralStage = Mathf.Clamp(
            Mathf.FloorToInt(developmentStage * 2f) + 1,
            1,
            4);
    }

    private float CalculateTemperatureResponse()
    {
        const float OptimumTemperature = 23f;
        const float MaximumTemperature = 38f;
        if (averageTemperature <= baseDevelopmentTemperature
            || averageTemperature >= MaximumTemperature)
        {
            return 0f;
        }

        return averageTemperature <= OptimumTemperature
            ? Mathf.InverseLerp(
                baseDevelopmentTemperature,
                OptimumTemperature,
                averageTemperature)
            : 1f - Mathf.InverseLerp(
                OptimumTemperature,
                MaximumTemperature,
                averageTemperature);
    }

    private static void CalculatePartitioning(
        float dvs,
        out float rootFraction,
        out float leafFraction,
        out float woodFraction)
    {
        if (dvs < 1f)
        {
            rootFraction = Mathf.Lerp(0.24f, 0.20f, dvs);
            leafFraction = Mathf.Lerp(0.48f, 0.30f, dvs);
        }
        else
        {
            float matureProgress = Mathf.Clamp01(dvs - 1f);
            rootFraction = Mathf.Lerp(0.20f, 0.16f, matureProgress);
            leafFraction = Mathf.Lerp(0.30f, 0.12f, matureProgress);
        }

        woodFraction = Mathf.Max(
            0f,
            1f - rootFraction - leafFraction);
    }

    private void OnValidate()
    {
        secondsPerSimulatedDay = Mathf.Max(0.02f, secondsPerSimulatedDay);
        playbackSpeed = Mathf.Clamp(playbackSpeed, 0.25f, 4f);
        globalRadiation = Mathf.Max(0f, globalRadiation);
        thermalTimeBudBurstToCrownExpansion = Mathf.Max(
            1f,
            thermalTimeBudBurstToCrownExpansion);
        thermalTimeCrownExpansionToMaturity = Mathf.Max(
            1f,
            thermalTimeCrownExpansionToMaturity);
        initialRootBiomass = Mathf.Max(0.001f, initialRootBiomass);
        initialLeafBiomass = Mathf.Max(0.001f, initialLeafBiomass);
        initialWoodBiomass = Mathf.Max(0.001f, initialWoodBiomass);
    }
}
