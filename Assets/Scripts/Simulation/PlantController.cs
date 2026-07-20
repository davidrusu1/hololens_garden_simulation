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


    // Environmental Parameters
    [SerializeField, Tooltip("Current Day Length [hours]")]
    private float currentDayLength = 12f;

    private float currentVernalization = 1f;

    // Reduction factors
    private float f_dayl = 1f; // Daylength reduction factor
    private float f_vern = 1f; // Vernalization reduction factor

        // Flags
    private bool hasReachedAnthesis = false;
    private bool hasReachedMaturity = false;
    private bool hasBeenVernalized = false;

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
            effectiveTemperature = PhenologicalDevelopment.CalculateEmergenceEffectiveTemperature(currentTemperature, plantData);

            // Update TSUM for emergence
            currentTSUM += effectiveTemperature;

            // Check if the plant has emerged
            if(currentTSUM >= plantData.TSUMEM)
            {
                currentDVS = 0f; // Plant has emerged
                Debug.Log("The plant " + plantData.name + " has emerged at day " + TimeManager.Instance.currentDay + " with a TSUM of " + currentTSUM);
                currentTSUM = 0f; // Reset TSUM for the next phase
            }
        }else
        {
                // Calculate reduction factors
            // Daylength reduction factor
            f_dayl = PhenologicalDevelopment.CalculateDayLengthReductionFactor(currentDVS, currentDayLength, plantData);

            // Vernalization reduction factor
            currentVernalization += PhenologicalDevelopment.CalculateVernalizationRate(currentTemperature, plantData);
            f_vern = PhenologicalDevelopment.CalculateVernalizationReductionFactor(currentVernalization, currentDVS, plantData);

                // Update DVS
            // Calculate development rate for the current day
            currentDevelopmentRate = PhenologicalDevelopment.CalculateDevelopmentRate(currentTemperature, currentDVS, f_dayl, f_vern, plantData);

            // Update DVS based on the development rate
            currentDVS += currentDevelopmentRate;
            // Update currentTSUM based on the effective temperature
            currentTSUM += effectiveTemperature;

            // Check for phenological milestones
            if(currentDVS >= 1f && !hasReachedAnthesis)
            {
                hasReachedAnthesis = true;
                Debug.Log("The plant " + plantData.name + " has reached anthesis (flowering) at day " + TimeManager.Instance.currentDay + " with a DVS of " + currentDVS);
            }
            else if(currentDVS >= plantData.DVSEND && !hasReachedMaturity)
            {
                hasReachedMaturity = true;
                currentDVS = plantData.DVSEND; // Cap DVS at the maximum
                Debug.Log("The plant " + plantData.name + " has reached maturity at day " + TimeManager.Instance.currentDay + " with a DVS of " + currentDVS);
            }
            else if(currentDVS >= plantData.VERNDVS && !hasBeenVernalized && plantData.IDSL == 2)
            {
                hasBeenVernalized = true;
                Debug.Log("The plant " + plantData.name + " has completed vernalization at day " + TimeManager.Instance.currentDay + " with a vernalization of " + currentVernalization);
            }
        }
    }
}