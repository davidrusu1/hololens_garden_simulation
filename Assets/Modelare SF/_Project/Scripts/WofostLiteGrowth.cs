using UnityEngine;

namespace ICI.PlantGrowth.Phenology
{
    /// <summary>
    /// Result of one WOFOST-inspired daily crop-growth calculation.
    /// All dry-matter values are expressed in kg/ha and LAI in ha/ha.
    /// </summary>
    public readonly struct WofostLiteDailyGrowth
    {
        public WofostLiteDailyGrowth(
            float interceptedRadiationFraction,
            float temperatureAssimilationFactor,
            float waterStressFactor,
            float grossAssimilation,
            float maintenanceRespiration,
            float netDryMatterGrowth,
            float rootGrowth,
            float stemGrowth,
            float leafGrowth,
            float storageGrowth,
            float leafDeath,
            float newLeafAreaIndex)
        {
            InterceptedRadiationFraction = interceptedRadiationFraction;
            TemperatureAssimilationFactor = temperatureAssimilationFactor;
            WaterStressFactor = waterStressFactor;
            GrossAssimilation = grossAssimilation;
            MaintenanceRespiration = maintenanceRespiration;
            NetDryMatterGrowth = netDryMatterGrowth;
            RootGrowth = rootGrowth;
            StemGrowth = stemGrowth;
            LeafGrowth = leafGrowth;
            StorageGrowth = storageGrowth;
            LeafDeath = leafDeath;
            NewLeafAreaIndex = newLeafAreaIndex;
        }

        public float InterceptedRadiationFraction { get; }
        public float TemperatureAssimilationFactor { get; }
        public float WaterStressFactor { get; }
        public float GrossAssimilation { get; }
        public float MaintenanceRespiration { get; }
        public float NetDryMatterGrowth { get; }
        public float RootGrowth { get; }
        public float StemGrowth { get; }
        public float LeafGrowth { get; }
        public float StorageGrowth { get; }
        public float LeafDeath { get; }
        public float NewLeafAreaIndex { get; }
    }

    /// <summary>
    /// Compact, deterministic crop-growth layer inspired by the WOFOST daily flow:
    /// intercepted radiation -> gross assimilation -> respiration -> organ
    /// partitioning -> leaf-area growth and senescence.
    /// </summary>
    public static class WofostLiteGrowth
    {
        private const float ReferenceRespirationTemperature = 25f;
        private const float GramsPerSquareMetreToKilogramsPerHectare = 10f;

        public static WofostLiteDailyGrowth CalculateDailyGrowth(
            PlantPhenologyProfile profile,
            float developmentStage,
            float averageTemperature,
            float solarRadiationMegajoulesPerSquareMetre,
            float soilWaterAvailability,
            float totalDryMatter,
            float leafDryMatter,
            float leafAreaIndex)
        {
            if (profile == null || developmentStage < 0f)
            {
                return new WofostLiteDailyGrowth(
                    0f,
                    0f,
                    Mathf.Clamp01(soilWaterAvailability),
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    Mathf.Max(0f, leafAreaIndex));
            }

            float safeLeafAreaIndex = Mathf.Max(0f, leafAreaIndex);
            float interceptedRadiationFraction = 1f - Mathf.Exp(
                -profile.LightExtinctionCoefficient * safeLeafAreaIndex);
            interceptedRadiationFraction = Mathf.Clamp01(interceptedRadiationFraction);

            float temperatureFactor = CalculateTriangularTemperatureFactor(
                averageTemperature,
                profile.AssimilationMinimumTemperature,
                profile.AssimilationOptimumTemperature,
                profile.AssimilationMaximumTemperature);
            float waterFactor = Mathf.Clamp01(soilWaterAvailability);

            float grossAssimilation = Mathf.Max(0f, solarRadiationMegajoulesPerSquareMetre)
                * profile.PhotosyntheticallyActiveRadiationFraction
                * interceptedRadiationFraction
                * profile.RadiationUseEfficiency
                * GramsPerSquareMetreToKilogramsPerHectare
                * temperatureFactor
                * waterFactor;

            float respirationTemperatureFactor = Mathf.Pow(
                profile.RespirationQ10,
                (averageTemperature - ReferenceRespirationTemperature) / 10f);
            float maintenanceRespiration = Mathf.Max(0f, totalDryMatter)
                * profile.MaintenanceRespirationFraction
                * Mathf.Max(0f, respirationTemperatureFactor);
            maintenanceRespiration = Mathf.Min(maintenanceRespiration, grossAssimilation);

            float netDryMatterGrowth = Mathf.Max(
                0f,
                grossAssimilation - maintenanceRespiration);

            CalculatePartitioningFractions(
                developmentStage,
                out float rootFraction,
                out float stemFraction,
                out float leafFraction,
                out float storageFraction);

            float rootGrowth = netDryMatterGrowth * rootFraction;
            float stemGrowth = netDryMatterGrowth * stemFraction;
            float leafGrowth = netDryMatterGrowth * leafFraction;
            float storageGrowth = netDryMatterGrowth * storageFraction;

            float senescenceFraction = CalculateLeafSenescenceFraction(
                profile,
                developmentStage,
                waterFactor);
            float leafDeath = Mathf.Max(0f, leafDryMatter) * senescenceFraction;

            float effectiveTemperature = Mathf.Max(
                0f,
                PhenologicalDevelopment.CalculateEffectiveTemperature(
                    averageTemperature,
                    profile.DailyTemperatureResponse));
            float exponentialLeafAreaGrowth = safeLeafAreaIndex
                * profile.MaximumRelativeLeafAreaGrowth
                * effectiveTemperature;
            float sourceLimitedLeafAreaGrowth = leafGrowth * profile.SpecificLeafArea;
            float leafAreaGrowth = Mathf.Min(
                exponentialLeafAreaGrowth,
                sourceLimitedLeafAreaGrowth);
            float leafAreaDeath = safeLeafAreaIndex * senescenceFraction;
            float newLeafAreaIndex = Mathf.Clamp(
                safeLeafAreaIndex + Mathf.Max(0f, leafAreaGrowth) - leafAreaDeath,
                0f,
                profile.MaximumLeafAreaIndex);

            return new WofostLiteDailyGrowth(
                interceptedRadiationFraction,
                temperatureFactor,
                waterFactor,
                grossAssimilation,
                maintenanceRespiration,
                netDryMatterGrowth,
                rootGrowth,
                stemGrowth,
                leafGrowth,
                storageGrowth,
                leafDeath,
                newLeafAreaIndex);
        }

        private static float CalculateTriangularTemperatureFactor(
            float temperature,
            float minimum,
            float optimum,
            float maximum)
        {
            if (temperature <= minimum || temperature >= maximum)
            {
                return 0f;
            }

            if (temperature <= optimum)
            {
                return Mathf.InverseLerp(minimum, optimum, temperature);
            }

            return 1f - Mathf.InverseLerp(optimum, maximum, temperature);
        }

        private static void CalculatePartitioningFractions(
            float developmentStage,
            out float rootFraction,
            out float stemFraction,
            out float leafFraction,
            out float storageFraction)
        {
            if (developmentStage < 1f)
            {
                float progress = Mathf.Clamp01(developmentStage);
                rootFraction = Mathf.Lerp(0.30f, 0.12f, progress);
                leafFraction = Mathf.Lerp(0.50f, 0.25f, progress);
                storageFraction = 0f;
            }
            else
            {
                float progress = Mathf.Clamp01(developmentStage - 1f);
                rootFraction = Mathf.Lerp(0.12f, 0.02f, progress);
                leafFraction = Mathf.Lerp(0.15f, 0.02f, progress);
                // After flowering, progressively direct most new assimilates
                // toward the sunflower head and seeds, as in WOFOST partitioning tables.
                storageFraction = Mathf.Lerp(0.15f, 0.80f, progress);
            }

            stemFraction = Mathf.Max(
                0f,
                1f - rootFraction - leafFraction - storageFraction);
        }

        private static float CalculateLeafSenescenceFraction(
            PlantPhenologyProfile profile,
            float developmentStage,
            float waterFactor)
        {
            float maturityProgress = Mathf.InverseLerp(
                0.95f,
                profile.FinalDevelopmentStage,
                developmentStage);
            float ageSenescence = profile.MaximumLeafSenescenceRate
                * Mathf.SmoothStep(0f, 1f, maturityProgress);
            float droughtSenescence = Mathf.Max(0f, 1f - waterFactor) * 0.025f;
            return Mathf.Clamp01(ageSenescence + droughtSenescence);
        }
    }
}
