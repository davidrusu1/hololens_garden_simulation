using UnityEngine;

namespace ICI.PlantGrowth.Phenology
{
    /// <summary>
    /// Biological constants used by the numerical phenology simulation.
    /// The default values reproduce the sunflower profile from the prototype.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SunflowerPhenologyProfile",
        menuName = "Plant Growth/Phenology Profile")]
    public sealed class PlantPhenologyProfile : ScriptableObject
    {
        [Header("Plant")]
        [SerializeField]
        private string plantName = "Sunflower";

        [Header("Emergence")]
        [SerializeField, Min(0f)]
        [Tooltip("Temperature sum required from sowing to emergence.")]
        private float emergenceTemperatureSum = 130f;

        [SerializeField]
        [Tooltip("Lower threshold temperature for emergence.")]
        private float emergenceBaseTemperature = 3f;

        [SerializeField]
        [Tooltip("Maximum effective temperature for emergence.")]
        private float emergenceMaximumEffectiveTemperature = 32f;

        [Header("Phenological development")]
        [SerializeField, Range(0, 2)]
        [Tooltip("0 = temperature, 1 = temperature and day length, 2 = temperature, day length and vernalization.")]
        private int developmentMode = 1;

        [SerializeField, Range(0f, 24f)]
        private float criticalDayLength = 8f;

        [SerializeField, Range(0f, 24f)]
        private float optimumDayLength = 16f;

        [SerializeField]
        [Tooltip("X = daily temperature, Y = effective temperature used for development.")]
        private Vector2[] dailyTemperatureResponse =
        {
            new Vector2(0f, 0f),
            new Vector2(2f, 0f),
            new Vector2(18f, 16f),
            new Vector2(40f, 38f)
        };

        [Header("Vernalization")]
        [SerializeField, Min(0f)]
        private float baseVernalizationRequirement = 14f;

        [SerializeField, Min(0f)]
        private float saturatedVernalizationRequirement = 70f;

        [SerializeField]
        private Vector2[] vernalizationTemperatureResponse =
        {
            new Vector2(-8f, 0f),
            new Vector2(-4f, 0f),
            new Vector2(3f, 1f),
            new Vector2(10f, 1f),
            new Vector2(17f, 0f),
            new Vector2(20f, 0f)
        };

        [SerializeField, Min(0f)]
        private float vernalizationEndDevelopmentStage = 0.3f;

        [Header("Milestones")]
        [SerializeField, Min(0.001f)]
        [Tooltip("Temperature sum required from emergence to flowering (DVS 0 to 1).")]
        private float temperatureSumToFlowering = 1050f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Temperature sum required from flowering to maturity (DVS 1 to 2).")]
        private float temperatureSumToMaturity = 1000f;

        [SerializeField, Min(1f)]
        private float finalDevelopmentStage = 2f;

        [Header("WOFOST-lite canopy growth")]
        [SerializeField, Min(0.001f)]
        [Tooltip("Initial crop dry matter at emergence, in kg/ha.")]
        private float initialDryMatter = 50f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Initial green leaf area index at emergence.")]
        private float initialLeafAreaIndex = 0.05f;

        [SerializeField, Range(0.1f, 1.5f)]
        [Tooltip("Beer-Lambert light extinction coefficient of the canopy.")]
        private float lightExtinctionCoefficient = 0.8f;

        [SerializeField, Range(0.35f, 0.6f)]
        [Tooltip("Fraction of global solar radiation usable as PAR.")]
        private float photosyntheticallyActiveRadiationFraction = 0.5f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Dry-matter radiation-use efficiency, in g/MJ intercepted PAR.")]
        private float radiationUseEfficiency = 2.2f;

        [SerializeField, Range(0f, 0.1f)]
        [Tooltip("Daily maintenance respiration at the reference temperature.")]
        private float maintenanceRespirationFraction = 0.015f;

        [SerializeField, Range(1f, 3f)]
        [Tooltip("Respiration increase for every 10 degrees C above the reference temperature.")]
        private float respirationQ10 = 2f;

        [SerializeField]
        private float assimilationMinimumTemperature = 5f;

        [SerializeField]
        private float assimilationOptimumTemperature = 25f;

        [SerializeField]
        private float assimilationMaximumTemperature = 42f;

        [SerializeField, Min(0.0001f)]
        [Tooltip("Specific leaf area in ha of leaf per kg of leaf dry matter.")]
        private float specificLeafArea = 0.0035f;

        [SerializeField, Min(0.1f)]
        private float maximumLeafAreaIndex = 5f;

        [SerializeField, Min(0.0001f)]
        [Tooltip("Maximum relative LAI increase during early exponential growth, per degree-day.")]
        private float maximumRelativeLeafAreaGrowth = 0.008f;

        [SerializeField, Range(0f, 0.2f)]
        [Tooltip("Maximum daily fraction of leaf area lost near maturity.")]
        private float maximumLeafSenescenceRate = 0.04f;

        public string PlantName => string.IsNullOrWhiteSpace(plantName) ? name : plantName;
        public float EmergenceTemperatureSum => emergenceTemperatureSum;
        public float EmergenceBaseTemperature => emergenceBaseTemperature;
        public float EmergenceMaximumEffectiveTemperature => emergenceMaximumEffectiveTemperature;
        public int DevelopmentMode => developmentMode;
        public float CriticalDayLength => criticalDayLength;
        public float OptimumDayLength => optimumDayLength;
        public Vector2[] DailyTemperatureResponse => dailyTemperatureResponse;
        public float BaseVernalizationRequirement => baseVernalizationRequirement;
        public float SaturatedVernalizationRequirement => saturatedVernalizationRequirement;
        public Vector2[] VernalizationTemperatureResponse => vernalizationTemperatureResponse;
        public float VernalizationEndDevelopmentStage => vernalizationEndDevelopmentStage;
        public float TemperatureSumToFlowering => temperatureSumToFlowering;
        public float TemperatureSumToMaturity => temperatureSumToMaturity;
        public float FinalDevelopmentStage => finalDevelopmentStage;
        public float InitialDryMatter => initialDryMatter;
        public float InitialLeafAreaIndex => initialLeafAreaIndex;
        public float LightExtinctionCoefficient => lightExtinctionCoefficient;
        public float PhotosyntheticallyActiveRadiationFraction => photosyntheticallyActiveRadiationFraction;
        public float RadiationUseEfficiency => radiationUseEfficiency;
        public float MaintenanceRespirationFraction => maintenanceRespirationFraction;
        public float RespirationQ10 => respirationQ10;
        public float AssimilationMinimumTemperature => assimilationMinimumTemperature;
        public float AssimilationOptimumTemperature => assimilationOptimumTemperature;
        public float AssimilationMaximumTemperature => assimilationMaximumTemperature;
        public float SpecificLeafArea => specificLeafArea;
        public float MaximumLeafAreaIndex => maximumLeafAreaIndex;
        public float MaximumRelativeLeafAreaGrowth => maximumRelativeLeafAreaGrowth;
        public float MaximumLeafSenescenceRate => maximumLeafSenescenceRate;

        private void OnValidate()
        {
            emergenceTemperatureSum = Mathf.Max(0f, emergenceTemperatureSum);
            emergenceMaximumEffectiveTemperature = Mathf.Max(
                emergenceBaseTemperature,
                emergenceMaximumEffectiveTemperature);
            optimumDayLength = Mathf.Max(criticalDayLength, optimumDayLength);
            saturatedVernalizationRequirement = Mathf.Max(
                baseVernalizationRequirement,
                saturatedVernalizationRequirement);
            temperatureSumToFlowering = Mathf.Max(0.001f, temperatureSumToFlowering);
            temperatureSumToMaturity = Mathf.Max(0.001f, temperatureSumToMaturity);
            finalDevelopmentStage = Mathf.Max(1f, finalDevelopmentStage);
            initialDryMatter = Mathf.Max(0.001f, initialDryMatter);
            initialLeafAreaIndex = Mathf.Max(0.001f, initialLeafAreaIndex);
            lightExtinctionCoefficient = Mathf.Clamp(lightExtinctionCoefficient, 0.1f, 1.5f);
            photosyntheticallyActiveRadiationFraction = Mathf.Clamp(
                photosyntheticallyActiveRadiationFraction,
                0.35f,
                0.6f);
            radiationUseEfficiency = Mathf.Max(0.1f, radiationUseEfficiency);
            maintenanceRespirationFraction = Mathf.Clamp(
                maintenanceRespirationFraction,
                0f,
                0.1f);
            respirationQ10 = Mathf.Clamp(respirationQ10, 1f, 3f);
            assimilationOptimumTemperature = Mathf.Max(
                assimilationMinimumTemperature + 0.1f,
                assimilationOptimumTemperature);
            assimilationMaximumTemperature = Mathf.Max(
                assimilationOptimumTemperature + 0.1f,
                assimilationMaximumTemperature);
            specificLeafArea = Mathf.Max(0.0001f, specificLeafArea);
            maximumLeafAreaIndex = Mathf.Max(initialLeafAreaIndex, maximumLeafAreaIndex);
            maximumRelativeLeafAreaGrowth = Mathf.Max(
                0.0001f,
                maximumRelativeLeafAreaGrowth);
            maximumLeafSenescenceRate = Mathf.Clamp(
                maximumLeafSenescenceRate,
                0f,
                0.2f);
        }
    }
}
