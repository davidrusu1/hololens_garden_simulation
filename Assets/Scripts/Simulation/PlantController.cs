using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlantController : MonoBehaviour
{
        // Script parameters
    // Reference to the PlantData ScriptableObject
    [Header("Plant Data")]
    [Tooltip("Drag your PlantData .asset file here")]
    public PlantData plantData;

    // Current state of the plant
    [Header("Current State")]
    [SerializeField, Tooltip("Current Development Stage (DVS)")] 
    private float currentDVS = -0.1f; 
    private float currentDevelopmentRate = 0f;

    // Temperature Parameters
    [SerializeField, Tooltip("Current Temperature [Celsius]")]
    private float currentTemperature = 20f;
    private float effectiveTemperature = 0f;
    
    [SerializeField, Tooltip("Total accumulated heat (TSUM)")] 
    private float currentTSUM = 0f;

        // Flags
    private bool hasReachedAnthesis = false;
    private bool hasReachedMaturity = false;

    // Connect to the TimeManager to run the simulation every day
    private void OnEnable()
    {
        TimeManager.OnDayChanged += RunDailySimulation;
    }

    private void OnDisable()
    {
        TimeManager.OnDayChanged -= RunDailySimulation;
    }

    // Daily simulation logic
    private void RunDailySimulation()
    {
        // For emergence
        if(currentDVS < 0)
        {
            // Calculate effective temperature for emergence
            if(currentTemperature < plantData.TBASEM)
            {
                effectiveTemperature = 0f;
            }
            else if(currentTemperature > plantData.TEFFMX)
            {
                effectiveTemperature = plantData.TEFFMX - plantData.TBASEM;
            }
            else
            {
                effectiveTemperature = currentTemperature - plantData.TBASEM;
            }

            // Update TSUM for emergence
            currentTSUM += effectiveTemperature;

            // Check if the plant has emerged
            if(currentTSUM >= plantData.TSUMEM)
            {
                currentDVS = 0f; // Plant has emerged
                Debug.Log("The plant has emerged at day " + TimeManager.Instance.currentDay + " with a TSUM of " + currentTSUM);
                currentTSUM = 0f; // Reset TSUM for the next phase
            }
        }else
        {
            // Calculate development rate for the current day
            currentDevelopmentRate = CalculateDevelopmentRate(currentTemperature, plantData);

            // Update DVS based on the development rate
            currentDVS += currentDevelopmentRate;
            // Update currentTSUM based on the effective temperature
            currentTSUM += effectiveTemperature;

            if(currentDVS >= 1f && !hasReachedAnthesis)
            {
                hasReachedAnthesis = true;
                Debug.Log("The plant has reached anthesis (flowering) at day " + TimeManager.Instance.currentDay + " with a DVS of " + currentDVS);
            }
            else if(currentDVS >= plantData.DVSEND && !hasReachedMaturity)
            {
                hasReachedMaturity = true;
                currentDVS = plantData.DVSEND; // Cap DVS at the maximum
                Debug.Log("The plant has reached maturity at day " + TimeManager.Instance.currentDay + " with a DVS of " + currentDVS);
            }
        }
    }

    // Function to calculate the development rate based on temperature and plant data after emergence
    private float CalculateDevelopmentRate(float temperature, PlantData data)
    {
        // Placeholder logic for development rate calculation
        // This should be replaced with the actual WOFOST model calculations
        float developmentRate = 0f;

        // Calculate effective temperature using the DTSMTB table
        float effectiveTemp = CalculateEffectiveTemperature(temperature, data);
        if(currentDVS < 1f) // Pre-anthesis
            developmentRate = effectiveTemp / data.TSUM1;
        else if(currentDVS >= 1f && currentDVS < data.DVSEND) // Post-anthesis
            developmentRate = effectiveTemp / (data.TSUM2 + data.TSUM1);
        else
            developmentRate = 0f; // No further development after harvest
        developmentRate = effectiveTemp / data.TSUM1;

        return developmentRate;
    }

    // Function to calculate effective temperature after emergence
    private float CalculateEffectiveTemperature(float temperature, PlantData data)
    {
        if(data.IDSL == 0)  // If the plant has temperature-dependent development
        {
            for(int i = 0; i < data.DTSMTB.Length - 1; i++)
            {
                if (temperature >= data.DTSMTB[i].x && temperature <= data.DTSMTB[i + 1].x)
                {
                    // Linear interpolation between the two points
                    float x0 = data.DTSMTB[i].x;
                    float y0 = data.DTSMTB[i].y;
                    float x1 = data.DTSMTB[i + 1].x;
                    float y1 = data.DTSMTB[i + 1].y;

                    // Calculate the effective temperature using linear interpolation
                    return y0 + (y1 - y0) * ((temperature - x0) / (x1 - x0));
                }else if (temperature < data.DTSMTB[0].x)
                {
                    // If the temperature is below the first point, return the first point's effective temperature
                    return data.DTSMTB[0].y;
                }
                else if (temperature > data.DTSMTB[data.DTSMTB.Length - 1].x)
                {
                    // If the temperature is above the last point, return the last point's effective temperature
                    return data.DTSMTB[data.DTSMTB.Length - 1].y;
                }
            }
        }

        // Return a default value if temperature is outside the defined range
        return 0f;

    }
}


