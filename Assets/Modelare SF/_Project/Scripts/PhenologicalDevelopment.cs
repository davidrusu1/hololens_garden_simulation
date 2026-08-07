using UnityEngine;

namespace ICI.PlantGrowth.Phenology
{
    /// <summary>
    /// Numerical phenology calculations adapted from the standalone prototype.
    /// </summary>
    public static class PhenologicalDevelopment
    {
        public static float CalculateEmergenceEffectiveTemperature(
            float temperature,
            PlantPhenologyProfile profile)
        {
            if (profile == null || temperature <= profile.EmergenceBaseTemperature)
            {
                return 0f;
            }

            float cappedTemperature = Mathf.Min(
                temperature,
                profile.EmergenceMaximumEffectiveTemperature);
            return Mathf.Max(0f, cappedTemperature - profile.EmergenceBaseTemperature);
        }

        public static float CalculateDevelopmentRate(
            float temperature,
            float currentDevelopmentStage,
            float dayLengthFactor,
            float vernalizationFactor,
            PlantPhenologyProfile profile)
        {
            if (profile == null)
            {
                return 0f;
            }

            float effectiveTemperature = CalculateEffectiveTemperature(
                temperature,
                profile.DailyTemperatureResponse);

            if (currentDevelopmentStage < 1f)
            {
                return dayLengthFactor
                    * vernalizationFactor
                    * effectiveTemperature
                    / Mathf.Max(0.001f, profile.TemperatureSumToFlowering);
            }

            if (currentDevelopmentStage < profile.FinalDevelopmentStage)
            {
                return effectiveTemperature
                    / Mathf.Max(0.001f, profile.TemperatureSumToMaturity);
            }

            return 0f;
        }

        public static float CalculateDayLengthReductionFactor(
            float currentDevelopmentStage,
            float dayLength,
            PlantPhenologyProfile profile)
        {
            if (profile == null
                || currentDevelopmentStage >= 1f
                || profile.DevelopmentMode == 0)
            {
                return 1f;
            }

            if (dayLength <= profile.CriticalDayLength)
            {
                return 0f;
            }

            if (dayLength >= profile.OptimumDayLength
                || Mathf.Approximately(profile.OptimumDayLength, profile.CriticalDayLength))
            {
                return 1f;
            }

            return Mathf.Clamp01(
                (dayLength - profile.CriticalDayLength)
                / (profile.OptimumDayLength - profile.CriticalDayLength));
        }

        public static float CalculateVernalizationReductionFactor(
            float currentVernalization,
            float currentDevelopmentStage,
            PlantPhenologyProfile profile)
        {
            if (profile == null
                || profile.DevelopmentMode < 2
                || currentDevelopmentStage < 0f
                || currentDevelopmentStage >= profile.VernalizationEndDevelopmentStage)
            {
                return 1f;
            }

            float range = profile.SaturatedVernalizationRequirement
                - profile.BaseVernalizationRequirement;

            if (range <= 0.001f)
            {
                return 1f;
            }

            return Mathf.Clamp01(
                (currentVernalization - profile.BaseVernalizationRequirement) / range);
        }

        public static float CalculateVernalizationRate(
            float temperature,
            PlantPhenologyProfile profile)
        {
            if (profile == null || profile.DevelopmentMode < 2)
            {
                return 0f;
            }

            return CalculateEffectiveTemperature(
                temperature,
                profile.VernalizationTemperatureResponse);
        }

        public static float CalculateEffectiveTemperature(
            float temperature,
            Vector2[] responseTable)
        {
            if (responseTable == null || responseTable.Length == 0)
            {
                return 0f;
            }

            if (responseTable.Length == 1 || temperature <= responseTable[0].x)
            {
                return responseTable[0].y;
            }

            int lastIndex = responseTable.Length - 1;
            if (temperature >= responseTable[lastIndex].x)
            {
                return responseTable[lastIndex].y;
            }

            for (int index = 0; index < lastIndex; index++)
            {
                Vector2 start = responseTable[index];
                Vector2 end = responseTable[index + 1];

                if (temperature < start.x || temperature > end.x)
                {
                    continue;
                }

                float range = end.x - start.x;
                if (Mathf.Abs(range) <= 0.0001f)
                {
                    return start.y;
                }

                float progress = (temperature - start.x) / range;
                return Mathf.Lerp(start.y, end.y, progress);
            }

            return 0f;
        }
    }
}
