using ICI.PlantGrowth.MixedCrops;
using UnityEngine;

namespace ICI.PlantGrowth.Phenology
{
    /// <summary>
    /// Runs the numerical sunflower simulation and starts the existing visual
    /// growth controller when the accumulated temperature reaches emergence.
    /// It also draws the runtime control panel used in Play Mode.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class PlantPhenologySimulation : MonoBehaviour
    {
        private const float MinimumTemperature = -10f;
        private const float MaximumTemperature = 45f;
        private const float MinimumDayLength = 0f;
        private const float MaximumDayLength = 24f;
        private const float MinimumSolarRadiation = 0f;
        private const float MaximumSolarRadiation = 35f;
        private const float MinimumSecondsPerDay = 0.1f;
        private const float MaximumSecondsPerDay = 3f;
        private const float ReferenceSecondsPerSimulatedDay = 0.5f;
        private const float MinimumVisualGrowthSpeed = 0.25f;
        private const float MaximumVisualGrowthSpeed = 4f;

        [Header("Phenology")]
        [SerializeField]
        private PlantPhenologyProfile profile;

        [SerializeField]
        private StemGrowthController stemGrowthController;

        [SerializeField]
        private MixedCropFieldController mixedCropFieldController;

        [Header("Mixed crop field")]
        [SerializeField, Range(0, MixedCropFieldController.MaxPlantsPerType)]
        private int requestedSunflowerCount = 3;

        [SerializeField, Range(0, MixedCropFieldController.MaxPlantsPerType)]
        private int requestedCassavaCount = 3;

        [Header("Environment")]
        [SerializeField, HideInInspector]
        private float currentTemperature = 24f;

        [SerializeField, Range(MinimumTemperature, MaximumTemperature)]
        [Tooltip("Daily minimum air temperature used to calculate the average crop temperature.")]
        private float currentMinimumTemperature = 18f;

        [SerializeField, Range(MinimumTemperature, MaximumTemperature)]
        [Tooltip("Daily maximum air temperature used to calculate the average crop temperature.")]
        private float currentMaximumTemperature = 30f;

        [SerializeField, Range(MinimumDayLength, MaximumDayLength)]
        private float currentDayLength = 14f;

        [SerializeField, Range(MinimumSolarRadiation, MaximumSolarRadiation)]
        [Tooltip("Daily global solar radiation in MJ per square metre.")]
        private float currentSolarRadiation = 18f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Available soil water: 0 is severe drought, 1 is no water stress.")]
        private float soilWaterAvailability = 1f;

        [Header("Simulation speed")]
        [SerializeField, Range(MinimumSecondsPerDay, MaximumSecondsPerDay)]
        [Tooltip("Playback duration of one simulated day. It changes only how fast the simulation runs, never the calculations performed for that day.")]
        private float secondsPerSimulatedDay = ReferenceSecondsPerSimulatedDay;

        [SerializeField, Range(MinimumVisualGrowthSpeed, MaximumVisualGrowthSpeed)]
        [Tooltip("Additional playback multiplier applied equally to the numerical days and to the visual plant growth.")]
        private float visualGrowthSpeed = 1f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Calibration between real-time prefab animation and biological time. It does not change simulated days or crop calculations.")]
        private float visualMorphologySpeedFactor = 3f;

        [Header("Runtime panel")]
        [SerializeField]
        private bool showRuntimePanel = true;

        private bool isRunning;
        private bool hasEmerged;
        private bool hasFlowered;
        private bool hasMatured;
        private int currentDay;
        private float currentDevelopmentStage;
        private float currentTemperatureSum;
        private float currentVernalization;
        private float currentDevelopmentRate;
        private float dayLengthFactor;
        private float vernalizationFactor;
        private float dayTimer;
        private float appliedVisualGrowthSpeed = -1f;
        private string statusMessage;
        private float totalDryMatter;
        private float rootDryMatter;
        private float stemDryMatter;
        private float leafDryMatter;
        private float storageDryMatter;
        private float leafAreaIndex;
        private float dailyGrossAssimilation;
        private float dailyMaintenanceRespiration;
        private float dailyNetDryMatterGrowth;
        private float interceptedRadiationFraction;
        private float temperatureAssimilationFactor;
        private float waterStressFactor = 1f;

        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle valueStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private Vector2 panelScrollPosition;

        public bool IsRunning => isRunning;
        public int CurrentDay => currentDay;
        public float CurrentDevelopmentStage => currentDevelopmentStage;
        public float CurrentTemperatureSum => currentTemperatureSum;
        public float CurrentTemperature => currentTemperature;
        public float CurrentMinimumTemperature => currentMinimumTemperature;
        public float CurrentMaximumTemperature => currentMaximumTemperature;
        public float CurrentDayLength => currentDayLength;
        public float CurrentSolarRadiation => currentSolarRadiation;
        public float SoilWaterAvailability => soilWaterAvailability;
        public float TotalDryMatter => totalDryMatter;
        public float RootDryMatter => rootDryMatter;
        public float StemDryMatter => stemDryMatter;
        public float LeafDryMatter => leafDryMatter;
        public float StorageDryMatter => storageDryMatter;
        public float LeafAreaIndex => leafAreaIndex;
        public float DailyNetDryMatterGrowth => dailyNetDryMatterGrowth;
        public string StatusMessage => statusMessage;

        private void Awake()
        {
            RefreshAverageTemperature();
            ResolveGrowthController();
            ResolveMixedCropFieldController();
            PrepareVisualGrowthController();
            ResetNumericalState();
        }

        private void Start()
        {
            ResetVisualPlant();
        }

        private void Update()
        {
            ApplyVisualGrowthSpeed();
            RefreshLiveEnvironmentalPreview();

            if (!isRunning && Input.GetKeyDown(KeyCode.Space))
            {
                StartSimulation();
            }

            if (!isRunning || profile == null)
            {
                return;
            }

            // Speed changes only how quickly simulated days are played. Every
            // processed day still runs AdvanceOneDay exactly once with the same
            // environmental inputs, so the final day and phenological values
            // are independent of either playback control.
            float effectiveSimulationSpeed = GetEffectiveGrowthSpeed();
            dayTimer += Time.unscaledDeltaTime * effectiveSimulationSpeed;
            float dayDuration = ReferenceSecondsPerSimulatedDay;

            // The guard avoids a long frame if the speed slider is changed abruptly.
            int processedDays = 0;
            while (dayTimer >= dayDuration && processedDays < 100)
            {
                dayTimer -= dayDuration;
                AdvanceOneDay();
                processedDays++;

                if (!isRunning)
                {
                    break;
                }
            }
        }

        public void StartSimulation()
        {
            if (profile == null)
            {
                statusMessage = "Lipsește profilul fenologic al plantei.";
                Debug.LogError($"[{nameof(PlantPhenologySimulation)}] No phenology profile is assigned.", this);
                return;
            }

            ResolveGrowthController();
            ResolveMixedCropFieldController();
            ApplyFieldConfiguration();
            PrepareVisualGrowthController();
            ResetVisualPlant();
            ResetNumericalState();
            isRunning = true;
            statusMessage = "Semănată — se acumulează temperatura pentru răsărire.";

            Debug.Log(
                $"[{nameof(PlantPhenologySimulation)}] Started {profile.PlantName} simulation "
                + $"at Tmin {currentMinimumTemperature:0.0} °C, Tmax {currentMaximumTemperature:0.0} °C, "
                + $"{currentSolarRadiation:0.0} MJ/m²/day and {currentDayLength:0.0} h day length.",
                this);
        }

        public void StopSimulation()
        {
            isRunning = false;
            statusMessage = "Simulare oprită.";
            stemGrowthController?.StopGrowth();
            mixedCropFieldController?.StopAllGrowth();
        }

        private void AdvanceOneDay()
        {
            RefreshAverageTemperature();
            currentDay++;

            // Maturity stops biological development, not the calendar. Keep
            // counting simulated days while preserving every terminal value.
            if (hasMatured)
            {
                statusMessage = "Maturitate atinsă — numărătoarea zilelor continuă.";
                return;
            }

            if (currentDevelopmentStage < 0f)
            {
                float emergenceTemperature =
                    PhenologicalDevelopment.CalculateEmergenceEffectiveTemperature(
                        currentTemperature,
                        profile);
                currentTemperatureSum += emergenceTemperature;

                if (currentTemperatureSum + 0.0001f < profile.EmergenceTemperatureSum)
                {
                    return;
                }

                currentDevelopmentStage = 0f;
                currentTemperatureSum = 0f;
                hasEmerged = true;
                InitializeCropGrowthState();
                statusMessage = "Răsărită — începe creșterea vizuală.";
                if (mixedCropFieldController != null)
                {
                    mixedCropFieldController.StartAllGrowth();
                }
                else
                {
                    stemGrowthController?.StartGrowth();
                }

                Debug.Log(
                    $"[{nameof(PlantPhenologySimulation)}] {profile.PlantName} emerged on day {currentDay}.",
                    this);
                return;
            }

            dayLengthFactor = PhenologicalDevelopment.CalculateDayLengthReductionFactor(
                currentDevelopmentStage,
                currentDayLength,
                profile);

            currentVernalization += PhenologicalDevelopment.CalculateVernalizationRate(
                currentTemperature,
                profile);
            vernalizationFactor = PhenologicalDevelopment.CalculateVernalizationReductionFactor(
                currentVernalization,
                currentDevelopmentStage,
                profile);

            float effectiveTemperature = PhenologicalDevelopment.CalculateEffectiveTemperature(
                currentTemperature,
                profile.DailyTemperatureResponse);
            currentTemperatureSum += effectiveTemperature;

            currentDevelopmentRate = PhenologicalDevelopment.CalculateDevelopmentRate(
                currentTemperature,
                currentDevelopmentStage,
                dayLengthFactor,
                vernalizationFactor,
                profile);
            currentDevelopmentStage = Mathf.Min(
                profile.FinalDevelopmentStage,
                currentDevelopmentStage + currentDevelopmentRate);

            AdvanceCropGrowth();

            if (!hasFlowered && currentDevelopmentStage >= 1f)
            {
                hasFlowered = true;
                currentTemperatureSum = 0f;
                statusMessage = "Înflorire — DVS a ajuns la 1.";
                if (mixedCropFieldController != null)
                {
                    mixedCropFieldController.NotifyFloweringStarted();
                }
                else
                {
                    stemGrowthController?.NotifyFloweringStarted();
                }
                Debug.Log(
                    $"[{nameof(PlantPhenologySimulation)}] {profile.PlantName} reached flowering on day {currentDay}.",
                    this);
            }
            else if (!hasMatured)
            {
                statusMessage = hasEmerged
                    ? "Dezvoltare vegetativă în curs."
                    : statusMessage;
            }

            if (!hasMatured
                && currentDevelopmentStage >= profile.FinalDevelopmentStage - 0.0001f)
            {
                hasMatured = true;
                currentDevelopmentStage = profile.FinalDevelopmentStage;
                statusMessage = "Maturitate atinsă — numărătoarea zilelor continuă.";
                if (mixedCropFieldController != null)
                {
                    mixedCropFieldController.CompleteVisualMaturity();
                }
                else
                {
                    stemGrowthController?.CompleteVisualMaturity();
                }
                Debug.Log(
                    $"[{nameof(PlantPhenologySimulation)}] {profile.PlantName} reached maturity on day {currentDay}.",
                    this);
            }
        }

        private void ResolveGrowthController()
        {
            if (stemGrowthController == null)
            {
                stemGrowthController = GetComponent<StemGrowthController>();
            }

            if (stemGrowthController == null)
            {
                stemGrowthController = FindObjectOfType<StemGrowthController>(true);
            }
        }

        private void ResolveMixedCropFieldController()
        {
            if (mixedCropFieldController == null)
            {
                mixedCropFieldController = GetComponentInParent<MixedCropFieldController>();
            }

            if (mixedCropFieldController == null)
            {
                mixedCropFieldController = FindObjectOfType<MixedCropFieldController>(true);
            }
        }

        private void PrepareVisualGrowthController()
        {
            if (stemGrowthController == null)
            {
                return;
            }

            stemGrowthController.SetAutomaticStart(false);
            stemGrowthController.StopGrowth();
            ApplyVisualGrowthSpeed(true);

            if (mixedCropFieldController != null)
            {
                mixedCropFieldController.SetSimulationSpeed(GetEffectiveVisualGrowthSpeed());
            }
        }

        private void ResetVisualPlant()
        {
            if (stemGrowthController == null)
            {
                return;
            }

            stemGrowthController.SetAutomaticStart(false);
            stemGrowthController.SetStage(1);
            stemGrowthController.StopGrowth();
            mixedCropFieldController?.ResetAllGrowth();
        }

        private void ApplyFieldConfiguration()
        {
            ResolveMixedCropFieldController();
            if (mixedCropFieldController == null)
            {
                return;
            }

            requestedSunflowerCount = Mathf.Clamp(
                requestedSunflowerCount,
                0,
                MixedCropFieldController.MaxPlantsPerType);
            requestedCassavaCount = Mathf.Clamp(
                requestedCassavaCount,
                0,
                MixedCropFieldController.MaxPlantsPerType);
            mixedCropFieldController.ConfigureCounts(
                requestedSunflowerCount,
                requestedCassavaCount);
            mixedCropFieldController.SetSimulationSpeed(GetEffectiveVisualGrowthSpeed());

            if (hasMatured)
            {
                mixedCropFieldController.CompleteVisualMaturity();
            }
            else if (hasEmerged)
            {
                mixedCropFieldController.StartAllGrowth();
                if (hasFlowered)
                {
                    mixedCropFieldController.NotifyFloweringStarted();
                }
            }
        }

        private void ResetNumericalState()
        {
            isRunning = false;
            hasEmerged = false;
            hasFlowered = false;
            hasMatured = false;
            currentDay = 1;
            currentDevelopmentStage = -0.1f;
            currentTemperatureSum = 0f;
            currentVernalization = 1f;
            currentDevelopmentRate = 0f;
            dayLengthFactor = 1f;
            vernalizationFactor = 1f;
            dayTimer = 0f;
            totalDryMatter = 0f;
            rootDryMatter = 0f;
            stemDryMatter = 0f;
            leafDryMatter = 0f;
            storageDryMatter = 0f;
            leafAreaIndex = 0f;
            dailyGrossAssimilation = 0f;
            dailyMaintenanceRespiration = 0f;
            dailyNetDryMatterGrowth = 0f;
            interceptedRadiationFraction = 0f;
            temperatureAssimilationFactor = 0f;
            waterStressFactor = Mathf.Clamp01(soilWaterAvailability);
            statusMessage = "Reglează valorile și apasă Start Simulation.";
        }

        private void InitializeCropGrowthState()
        {
            if (profile == null)
            {
                return;
            }

            totalDryMatter = profile.InitialDryMatter;
            rootDryMatter = totalDryMatter * 0.30f;
            leafDryMatter = totalDryMatter * 0.50f;
            stemDryMatter = totalDryMatter - rootDryMatter - leafDryMatter;
            storageDryMatter = 0f;
            leafAreaIndex = profile.InitialLeafAreaIndex;
        }

        private void AdvanceCropGrowth()
        {
            WofostLiteDailyGrowth growth = WofostLiteGrowth.CalculateDailyGrowth(
                profile,
                currentDevelopmentStage,
                currentTemperature,
                currentSolarRadiation,
                soilWaterAvailability,
                totalDryMatter,
                leafDryMatter,
                leafAreaIndex);

            rootDryMatter = Mathf.Max(0f, rootDryMatter + growth.RootGrowth);
            stemDryMatter = Mathf.Max(0f, stemDryMatter + growth.StemGrowth);
            leafDryMatter = Mathf.Max(
                0f,
                leafDryMatter + growth.LeafGrowth - growth.LeafDeath);
            storageDryMatter = Mathf.Max(0f, storageDryMatter + growth.StorageGrowth);
            totalDryMatter = rootDryMatter
                + stemDryMatter
                + leafDryMatter
                + storageDryMatter;
            leafAreaIndex = growth.NewLeafAreaIndex;
            dailyGrossAssimilation = growth.GrossAssimilation;
            dailyMaintenanceRespiration = growth.MaintenanceRespiration;
            dailyNetDryMatterGrowth = growth.NetDryMatterGrowth;
            interceptedRadiationFraction = growth.InterceptedRadiationFraction;
            temperatureAssimilationFactor = growth.TemperatureAssimilationFactor;
            waterStressFactor = growth.WaterStressFactor;
        }

        private void ApplyVisualGrowthSpeed(bool force = false)
        {
            secondsPerSimulatedDay = Mathf.Clamp(
                secondsPerSimulatedDay,
                MinimumSecondsPerDay,
                MaximumSecondsPerDay);
            visualGrowthSpeed = Mathf.Clamp(
                visualGrowthSpeed,
                MinimumVisualGrowthSpeed,
                MaximumVisualGrowthSpeed);

            float effectiveGrowthSpeed = GetEffectiveVisualGrowthSpeed();
            if (!force
                && Mathf.Approximately(appliedVisualGrowthSpeed, effectiveGrowthSpeed))
            {
                return;
            }

            appliedVisualGrowthSpeed = effectiveGrowthSpeed;
            stemGrowthController?.SetSimulationSpeed(effectiveGrowthSpeed);
            mixedCropFieldController?.SetSimulationSpeed(effectiveGrowthSpeed);
        }

        private float GetEffectiveGrowthSpeed()
        {
            // At the default 0.5 seconds/day and 1x multiplier the visual speed
            // remains 1x. Halving seconds/day or doubling the multiplier has
            // the same result: both numerical and visual growth run twice as fast.
            float secondsPerDayFactor = ReferenceSecondsPerSimulatedDay
                / Mathf.Max(MinimumSecondsPerDay, secondsPerSimulatedDay);
            return Mathf.Max(0.1f, visualGrowthSpeed * secondsPerDayFactor);
        }

        private float GetEffectiveVisualGrowthSpeed()
        {
            return GetEffectiveGrowthSpeed() * Mathf.Max(0.1f, visualMorphologySpeedFactor);
        }

        private void OnGUI()
        {
            if (!showRuntimePanel)
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
            panelScrollPosition = GUILayout.BeginScrollView(
                panelScrollPosition,
                false,
                true);
            GUILayout.Label("SIMULARE FENOLOGICĂ", titleStyle);
            GUILayout.Label("Câmp mixt: floarea-soarelui + cassava", sectionStyle);
            GUILayout.Space(5f);

            GUILayout.Label("CONFIGURAȚIE CULTURI", sectionStyle);
            requestedSunflowerCount = DrawIntegerSlider(
                "Flori-soarelui",
                requestedSunflowerCount,
                0,
                MixedCropFieldController.MaxPlantsPerType);
            requestedCassavaCount = DrawIntegerSlider(
                "Plante cassava",
                requestedCassavaCount,
                0,
                MixedCropFieldController.MaxPlantsPerType);
            int requestedTotal = requestedSunflowerCount + requestedCassavaCount;
            int requestedRows = Mathf.CeilToInt(
                requestedTotal / (float)MixedCropFieldController.MaxPlantsPerRow);
            GUILayout.Label(
                $"{requestedTotal} plante · {requestedRows} rânduri · maximum 5 pe rând · alternare automată",
                statusStyle);
            if (GUILayout.Button("APLICĂ DISPUNEREA", GUILayout.Height(32f)))
            {
                ApplyFieldConfiguration();
                statusMessage = requestedTotal > 0
                    ? "Dispunere actualizată — plantele sunt spațiate și alternate."
                    : "Dispunere goală — alege cel puțin o plantă pentru vizualizare.";
            }

            GUILayout.Space(8f);
            GUILayout.Label("MEDIU ȘI VITEZĂ", sectionStyle);

            currentMinimumTemperature = DrawSlider(
                "Temperatură minimă",
                currentMinimumTemperature,
                MinimumTemperature,
                MaximumTemperature,
                "°C");
            currentMaximumTemperature = DrawSlider(
                "Temperatură maximă",
                currentMaximumTemperature,
                MinimumTemperature,
                MaximumTemperature,
                "°C");
            NormalizeTemperatureRange();
            GUILayout.Label($"Temperatură medie folosită: {currentTemperature:0.0} °C", statusStyle);
            currentDayLength = DrawSlider(
                "Durata zilei",
                currentDayLength,
                MinimumDayLength,
                MaximumDayLength,
                "h");
            currentSolarRadiation = DrawSlider(
                "Radiație solară",
                currentSolarRadiation,
                MinimumSolarRadiation,
                MaximumSolarRadiation,
                "MJ/m²");
            soilWaterAvailability = DrawSlider(
                "Apă disponibilă în sol",
                soilWaterAvailability,
                0f,
                1f,
                "");
            secondsPerSimulatedDay = DrawSlider(
                "Secunde / zi simulată",
                secondsPerSimulatedDay,
                MinimumSecondsPerDay,
                MaximumSecondsPerDay,
                "s");
            visualGrowthSpeed = DrawSlider(
                "Multiplicator viteză",
                visualGrowthSpeed,
                MinimumVisualGrowthSpeed,
                MaximumVisualGrowthSpeed,
                "x");

            // OnGUI receives the slider input after Update. Refreshing here as
            // well makes the displayed factor/rate react in the same frame.
            ApplyVisualGrowthSpeed();
            RefreshLiveEnvironmentalPreview();
            GUILayout.Label(
                $"Viteză calendar: {GetEffectiveGrowthSpeed():0.00}x   Morfologie: {GetEffectiveVisualGrowthSpeed():0.00}x",
                valueStyle);

            GUILayout.Space(8f);
            GUILayout.Label("STARE", sectionStyle);
            GUILayout.Label(statusMessage, statusStyle, GUILayout.MinHeight(38f));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Ziua: {currentDay}", valueStyle);
            GUILayout.Label($"DVS: {currentDevelopmentStage:0.000}", valueStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"TSUM: {currentTemperatureSum:0.0}", valueStyle);
            GUILayout.Label($"Rată: {currentDevelopmentRate:0.0000}", valueStyle);
            GUILayout.EndHorizontal();

            GUILayout.Label(
                $"Factor zi: {dayLengthFactor:0.00}   Factor vernalizare: {vernalizationFactor:0.00}",
                statusStyle);
            GUILayout.Space(4f);
            GUILayout.Label("CREȘTERE WOFOST-LITE", sectionStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Biomasă: {totalDryMatter:0} kg/ha", valueStyle);
            GUILayout.Label($"LAI: {leafAreaIndex:0.00}", valueStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label(
                $"Rădăcină {rootDryMatter:0} · Tulpină {stemDryMatter:0} · Frunze {leafDryMatter:0} · Floare/semințe {storageDryMatter:0} kg/ha",
                statusStyle);
            GUILayout.Label(
                $"Creștere netă: {dailyNetDryMatterGrowth:0.0} kg/ha/zi · Lumină interceptată: {interceptedRadiationFraction * 100f:0}%",
                statusStyle);
            GUILayout.Label(
                $"Asimilație: {dailyGrossAssimilation:0.0} · Respirație: {dailyMaintenanceRespiration:0.0} · Factor T: {temperatureAssimilationFactor:0.00} · Apă: {waterStressFactor:0.00}",
                statusStyle);
            GUILayout.Label(
                "Valorile se aplică în timp real simulării și animațiilor aflate deja în curs.",
                statusStyle);

            GUILayout.FlexibleSpace();
            GUI.enabled = profile != null;
            string buttonText = isRunning ? "RESTART SIMULATION" : "START SIMULATION";
            if (GUILayout.Button(buttonText, buttonStyle, GUILayout.Height(44f)))
            {
                StartSimulation();
            }
            GUI.enabled = true;

            GUILayout.Label(
                "Space: start · WASD: mișcare · ↑/↓: vertical · click dreapta + mouse: privire",
                statusStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUI.matrix = previousMatrix;
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

        private int DrawIntegerSlider(
            string label,
            int value,
            int minimum,
            int maximum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, valueStyle, GUILayout.Width(235f));
            GUILayout.Label(value.ToString(), valueStyle, GUILayout.Width(95f));
            GUILayout.EndHorizontal();
            return Mathf.RoundToInt(GUILayout.HorizontalSlider(value, minimum, maximum));
        }

        private void RefreshLiveEnvironmentalPreview()
        {
            RefreshAverageTemperature();

            if (profile == null)
            {
                currentDevelopmentRate = 0f;
                dayLengthFactor = 1f;
                vernalizationFactor = 1f;
                return;
            }

            if (currentDevelopmentStage < 0f)
            {
                currentDevelopmentRate = 0f;
                dayLengthFactor = 1f;
                vernalizationFactor = 1f;
                return;
            }

            dayLengthFactor = PhenologicalDevelopment.CalculateDayLengthReductionFactor(
                currentDevelopmentStage,
                currentDayLength,
                profile);
            vernalizationFactor = PhenologicalDevelopment.CalculateVernalizationReductionFactor(
                currentVernalization,
                currentDevelopmentStage,
                profile);
            currentDevelopmentRate = PhenologicalDevelopment.CalculateDevelopmentRate(
                currentTemperature,
                currentDevelopmentStage,
                dayLengthFactor,
                vernalizationFactor,
                profile);
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

        private void OnValidate()
        {
            currentMinimumTemperature = Mathf.Clamp(
                currentMinimumTemperature,
                MinimumTemperature,
                MaximumTemperature);
            currentMaximumTemperature = Mathf.Clamp(
                currentMaximumTemperature,
                MinimumTemperature,
                MaximumTemperature);
            NormalizeTemperatureRange();
            currentDayLength = Mathf.Clamp(
                currentDayLength,
                MinimumDayLength,
                MaximumDayLength);
            secondsPerSimulatedDay = Mathf.Clamp(
                secondsPerSimulatedDay,
                MinimumSecondsPerDay,
                MaximumSecondsPerDay);
            visualGrowthSpeed = Mathf.Clamp(
                visualGrowthSpeed,
                MinimumVisualGrowthSpeed,
                MaximumVisualGrowthSpeed);
            visualMorphologySpeedFactor = Mathf.Max(0.1f, visualMorphologySpeedFactor);
            currentSolarRadiation = Mathf.Clamp(
                currentSolarRadiation,
                MinimumSolarRadiation,
                MaximumSolarRadiation);
            soilWaterAvailability = Mathf.Clamp01(soilWaterAvailability);
            requestedSunflowerCount = Mathf.Clamp(
                requestedSunflowerCount,
                0,
                MixedCropFieldController.MaxPlantsPerType);
            requestedCassavaCount = Mathf.Clamp(
                requestedCassavaCount,
                0,
                MixedCropFieldController.MaxPlantsPerType);
        }

        private void NormalizeTemperatureRange()
        {
            currentMinimumTemperature = Mathf.Clamp(
                currentMinimumTemperature,
                MinimumTemperature,
                MaximumTemperature);
            currentMaximumTemperature = Mathf.Clamp(
                currentMaximumTemperature,
                MinimumTemperature,
                MaximumTemperature);

            if (currentMinimumTemperature > currentMaximumTemperature)
            {
                currentMaximumTemperature = currentMinimumTemperature;
            }

            RefreshAverageTemperature();
        }

        private void RefreshAverageTemperature()
        {
            currentTemperature = (currentMinimumTemperature + currentMaximumTemperature) * 0.5f;
        }
    }
}
