using UnityEngine;

public static class PhenologicalDevelopment
{
    // Calculates the effective temperature during the emergence phase
    public static float CalculateEmergenceEffectiveTemperature(float temperature, PlantData data)
    {
        if (temperature < data.TBASEM)
            return 0f;
        else if (temperature > data.TEFFMX)
            return data.TEFFMX - data.TBASEM;
        else
            return temperature - data.TBASEM;
    }

    // Function to calculate the development rate based on temperature and plant data after emergence
    public static float CalculateDevelopmentRate(float temperature, float currentDVS, float f_dayl, float f_vern, PlantData data)
    {
        float effectiveTemp = CalculateEffectiveTemperature(temperature, data);

        if (currentDVS < 1f) // Pre-anthesis
        {
            if (data.TSUM1 <= 0f) 
            {
                Debug.LogError($"TSUM1 is zero or less on {data.name}! Returning 0 to prevent division by zero.");
                return 0f;
            }
            return f_dayl * f_vern * (effectiveTemp / data.TSUM1);
        }
        else if (currentDVS >= 1f && currentDVS < data.DVSEND) // Post-anthesis
        {
            if (data.TSUM2 <= 0f)
            {
                Debug.LogError($"TSUM2 is zero or less on {data.name}! Returning 0 to prevent division by zero.");
                return 0f;
            }
            // Standard WOFOST uses just TSUM2 for the post-anthesis phase
            return effectiveTemp / data.TSUM2; 
        }
        else
        {
            return 0f; // No further development after harvest
        }
    }

    // Function to calculate effective temperature after emergence using the DTSMTB table
    public static float CalculateEffectiveTemperature(float temperature, PlantData data)
    {
        if (data.DTSMTB == null || data.DTSMTB.Length == 0) return 0f;

        if (temperature < data.DTSMTB[0].x)
            return data.DTSMTB[0].y;
        else if (temperature > data.DTSMTB[data.DTSMTB.Length - 1].x)
            return data.DTSMTB[data.DTSMTB.Length - 1].y;

        for (int i = 0; i < data.DTSMTB.Length - 1; i++)
        {
            if (temperature >= data.DTSMTB[i].x && temperature <= data.DTSMTB[i + 1].x)
            {
                float x0 = data.DTSMTB[i].x;
                float y0 = data.DTSMTB[i].y;
                float x1 = data.DTSMTB[i + 1].x;
                float y1 = data.DTSMTB[i + 1].y;

                if (Mathf.Approximately(x1, x0)) return y0; // Prevent division by zero if table has duplicate X values

                return y0 + (y1 - y0) * ((temperature - x0) / (x1 - x0));
            }
        }
        return 0f;
    }

    public static float CalculateDayLengthReductionFactor(float currentDVS, float dayLength, PlantData data)
    {
        if (currentDVS < 1f) // Pre-anthesis phase
        {
            if (data.IDSL == 0) return 1f; 
            else if (data.IDSL == 1 || data.IDSL == 2) 
            {
                if (dayLength < data.DLC)
                    return 0f; 
                else if (dayLength >= data.DLO)
                    return 1f; 
                else
                {
                    if (Mathf.Approximately(data.DLO, data.DLC)) return 1f;
                    return (dayLength - data.DLC) / (data.DLO - data.DLC);
                }
            }
        }
        return 1f;
    }

    public static float CalculateVernalizationReductionFactor(float currentVernalization, float currentDVS, PlantData data)
    {
        if (data.IDSL == 0 || data.IDSL == 1) return 1f;

        if (data.IDSL == 2 && currentDVS < data.VERNDVS && currentDVS >= 0f) 
        {
            if (Mathf.Approximately(data.VERNSAT, data.VERNBASE)) return 1f;
            return Mathf.Clamp01((currentVernalization - data.VERNBASE) / (data.VERNSAT - data.VERNBASE));
        }

        return 1f;
    }

    public static float CalculateVernalizationRate(float temperature, PlantData data)
    {
        if (data.VERNRTB == null || data.VERNRTB.Length == 0) return 0f;

        if (temperature < data.VERNRTB[0].x)
            return data.VERNRTB[0].y;
        else if (temperature > data.VERNRTB[data.VERNRTB.Length - 1].x)
            return data.VERNRTB[data.VERNRTB.Length - 1].y;

        for (int i = 0; i < data.VERNRTB.Length - 1; i++)
        {
            if (temperature >= data.VERNRTB[i].x && temperature <= data.VERNRTB[i + 1].x)
            {
                float x0 = data.VERNRTB[i].x;
                float y0 = data.VERNRTB[i].y;
                float x1 = data.VERNRTB[i + 1].x;
                float y1 = data.VERNRTB[i + 1].y;

                if (Mathf.Approximately(x1, x0)) return y0; // Prevent division by zero

                return y0 + (y1 - y0) * ((temperature - x0) / (x1 - x0));
            }
        }
        return 0f;
    }
}