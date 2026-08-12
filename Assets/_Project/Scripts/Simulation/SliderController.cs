using UnityEngine;
using UnityEngine.UI;
using MixedReality.Toolkit.UX; 
using ICI.PlantGrowth.Phenology; 
using TMPro; // Perfect, you added this!

using MrtkSlider = MixedReality.Toolkit.UX.Slider;

public class UIController : MonoBehaviour
{
    [Header("Simulation Status")]
    [SerializeField] private TextMeshProUGUI currentDayLabel;

    [Header("Core Simulation")]
    [Tooltip("Leave empty to auto-find in the scene")]
    [SerializeField] private PlantPhenologySimulation simulation;

    [Header("Plant Config Sliders")]
    [SerializeField] private MrtkSlider sunflowerCountSlider;
    [SerializeField] private MrtkSlider cassavaCountSlider;

    [Header("UI Labels")]
    [SerializeField] private TextMeshProUGUI sunflowerLabel;
    [SerializeField] private TextMeshProUGUI cassavaLabel;
    [SerializeField] private TextMeshProUGUI minimumTemperatureLabel;
    [SerializeField] private TextMeshProUGUI maximumTemperatureLabel;
    [SerializeField] private TextMeshProUGUI dayLengthLabel;
    [SerializeField] private TextMeshProUGUI solarRadiationLabel;
    [SerializeField] private TextMeshProUGUI soilWaterLabel;
    [SerializeField] private TextMeshProUGUI secondsPerDayLabel;
    [SerializeField] private TextMeshProUGUI visualGrowthSpeedLabel;

    [Header("Environment Sliders")]
    [SerializeField] private MrtkSlider minimumTemperatureSlider;
    [SerializeField] private MrtkSlider maximumTemperatureSlider;
    [SerializeField] private MrtkSlider dayLengthSlider;
    [SerializeField] private MrtkSlider solarRadiationSlider;
    [SerializeField] private MrtkSlider soilWaterSlider;

    [Header("Speed Sliders")]
    [SerializeField] private MrtkSlider secondsPerDaySlider;
    [SerializeField] private MrtkSlider visualGrowthSpeedSlider;

    [Header("Playback Buttons")]
    [SerializeField] private PressableButton startSimulationButton;
    [SerializeField] private PressableButton pauseSimulationButton;
    [SerializeField] private PressableButton resumeSimulationButton;
    [SerializeField] private PressableButton stopSimulationButton;

    // Internal state tracking
    private int sunflowerCount;
    private int cassavaCount;
    private float minimumTemperature;
    private float maximumTemperature;
    private float dayLength;
    private float solarRadiation;
    private float soilWater;
    private float secondsPerDay;
    private float visualGrowthSpeed;

    private void Start()
    {
        // 1. Resolve Dependencies
        if (simulation == null)
        {
            simulation = FindObjectOfType<PlantPhenologySimulation>(true);
        }

        if (simulation == null)
        {
            Debug.LogError("UIController: PlantPhenologySimulation not found in scene!");
            return;
        }

        // 2. Initialize internal values from the simulation
        sunflowerCount = simulation.RequestedSunflowerCount;
        cassavaCount = simulation.RequestedCassavaCount;
        minimumTemperature = simulation.CurrentMinimumTemperature;
        maximumTemperature = simulation.CurrentMaximumTemperature;
        dayLength = simulation.CurrentDayLength;
        solarRadiation = simulation.CurrentSolarRadiation;
        soilWater = simulation.SoilWaterAvailability;
        secondsPerDay = simulation.SecondsPerSimulatedDay;
        visualGrowthSpeed = simulation.VisualGrowthSpeed;

        // Update the text label for sunflower count at the start
        SetupSlider(sunflowerCountSlider, sunflowerCount, value => 
        { 
            sunflowerCount = Mathf.RoundToInt(value); 
            
            // Update the text safely
            if (sunflowerLabel != null) 
            {
                sunflowerLabel.text = "Sunflower Count: " + sunflowerCount;
            }

            ApplyPlantCounts(); 
        });

        SetupSlider(cassavaCountSlider, cassavaCount, value => { cassavaCount = Mathf.RoundToInt(value); 
            if (cassavaLabel != null)
            {
                cassavaLabel.text = "Cassava Count: " + cassavaCount;
            }
            ApplyPlantCounts(); 
        });
        
        SetupSlider(dayLengthSlider, dayLength, value => { dayLength = value; 
            if (dayLengthLabel != null)
            {
                dayLengthLabel.text = "Day Length: " + dayLength.ToString("F1") + " hours";
            }
            ApplyEnvironmentAndSpeed(); });

        SetupSlider(solarRadiationSlider, solarRadiation, value => { solarRadiation = value; 
            if (solarRadiationLabel != null)
            {
                solarRadiationLabel.text = "Solar Radiation: " + solarRadiation.ToString("F1") + " W/m²";
            }
            ApplyEnvironmentAndSpeed(); });
        SetupSlider(soilWaterSlider, soilWater, value => { soilWater = value; 
            if (soilWaterLabel != null)
            {
                soilWaterLabel.text = "Soil Water: " + soilWater.ToString("F1");
            }
            ApplyEnvironmentAndSpeed(); });
        SetupSlider(secondsPerDaySlider, secondsPerDay, value => { secondsPerDay = value; 
            if (secondsPerDayLabel != null)
            {
                secondsPerDayLabel.text = "Seconds per Day: " + secondsPerDay.ToString("F1");
            }
            ApplyEnvironmentAndSpeed(); });
        SetupSlider(visualGrowthSpeedSlider, visualGrowthSpeed, value => { visualGrowthSpeed = value; 
            if (visualGrowthSpeedLabel != null)
            {
                visualGrowthSpeedLabel.text = "Visual Growth Speed: " + visualGrowthSpeed.ToString("F1");
            }
            ApplyEnvironmentAndSpeed(); });

        SetupSlider(minimumTemperatureSlider, minimumTemperature, OnMinTemperatureChanged);
        SetupSlider(maximumTemperatureSlider, maximumTemperature, OnMaxTemperatureChanged);
        
        if (startSimulationButton != null)
        {
            // Use .OnClicked for MRTK3 Pressable Buttons
            startSimulationButton.OnClicked.AddListener(() =>
            {
                simulation?.StartSimulation(); 
            });
        }

        
        // Wire up the Pause Button
        if (pauseSimulationButton != null)
        {
            pauseSimulationButton.OnClicked.AddListener(() =>
            {
                simulation?.PauseSimulation(); 
            });
        }

        // Wire up a Resume Button
        if (resumeSimulationButton != null)
        {
            resumeSimulationButton.OnClicked.AddListener(() =>
            {
                simulation?.ResumeSimulation(); 
            });
        }
        
        // Wire up the Stop Button
        if (stopSimulationButton != null)
        {
            stopSimulationButton.OnClicked.AddListener(() =>
            {
                simulation?.StopSimulation(); 
            });
        }
    }

    private void Update()
    {
        // Continuously update the text with the simulation's current day
        if (simulation != null)
        {
            // 1. Actualizăm ziua curentă
            if (currentDayLabel != null)
            {
                currentDayLabel.text = "Current Day: " + simulation.CurrentDay;
            }

            // 2. Verificăm starea simulării
            bool isRunning = simulation.IsRunning;
            
            // Ne folosim de mesajul de status setat de noi în scriptul de bază pentru a ști dacă e pe pauză
            bool isPaused = simulation.StatusMessage == "Simulare în pauză."; 

            // 3. Afișăm/Ascundem butoanele dinamic
            if (startSimulationButton != null)
                startSimulationButton.gameObject.SetActive(!isRunning && !isPaused); // Apare doar la început / după stop

            if (pauseSimulationButton != null)
                pauseSimulationButton.gameObject.SetActive(isRunning); // Apare doar când simularea merge

            if (resumeSimulationButton != null)
                resumeSimulationButton.gameObject.SetActive(!isRunning && isPaused); // Apare doar pe pauză

            if (stopSimulationButton != null)
                stopSimulationButton.gameObject.SetActive(isRunning || isPaused); // Apare oricând simularea e activă
        }
    }

    // This method handles the specific MRTK3 SliderEventData
    private void SetupSlider(MrtkSlider slider, float initialValue, System.Action<float> onValueChanged)
    {
        if (slider != null)
        {
            // Set the starting visual position of the MRTK3 slider
            slider.Value = initialValue;
            
            // Listen for MRTK3 OnValueUpdated events
            slider.OnValueUpdated.AddListener((SliderEventData data) => 
            {
                onValueChanged?.Invoke(data.NewValue);
            });

            onValueChanged?.Invoke(initialValue);
        }
    }

    private void OnMinTemperatureChanged(float value)
    {
        minimumTemperature = value;
        if (minimumTemperature > maximumTemperature)
        {
            maximumTemperature = minimumTemperature;
            if (maximumTemperatureSlider != null) maximumTemperatureSlider.Value = maximumTemperature;
        }
        if (minimumTemperatureLabel != null)
        {
            minimumTemperatureLabel.text = "Minimum Temperature: " + minimumTemperature.ToString("F1") + "°C";
        }
        ApplyEnvironmentAndSpeed();
    }

    private void OnMaxTemperatureChanged(float value)
    {
        maximumTemperature = value;
        if (maximumTemperature < minimumTemperature)
        {
            minimumTemperature = maximumTemperature;
            if (minimumTemperatureSlider != null) minimumTemperatureSlider.Value = minimumTemperature;
        }

        if (maximumTemperatureLabel != null)
        {
            maximumTemperatureLabel.text = "Maximum Temperature: " + maximumTemperature.ToString("F1") + "°C";
        }

        ApplyEnvironmentAndSpeed();
    }

    private void ApplyEnvironmentAndSpeed()
    {
        if (simulation == null) return;

        minimumTemperature = Mathf.Clamp(minimumTemperature, -10f, 45f);
        maximumTemperature = Mathf.Clamp(maximumTemperature, -10f, 45f);

        simulation.SetEnvironmentParameters(
            minimumTemperature,
            maximumTemperature,
            dayLength,
            solarRadiation,
            soilWater);

        simulation.SetPlaybackParameters(
            secondsPerDay,
            visualGrowthSpeed);
    }

    private void ApplyPlantCounts()
    {
        simulation?.SetFieldConfiguration(sunflowerCount, cassavaCount);
    }

    private void OnDestroy()
    {
        // Clean up MRTK3 listeners when the object is destroyed
        sunflowerCountSlider?.OnValueUpdated.RemoveAllListeners();
        cassavaCountSlider?.OnValueUpdated.RemoveAllListeners();
        minimumTemperatureSlider?.OnValueUpdated.RemoveAllListeners();
        maximumTemperatureSlider?.OnValueUpdated.RemoveAllListeners();
        dayLengthSlider?.OnValueUpdated.RemoveAllListeners();
        solarRadiationSlider?.OnValueUpdated.RemoveAllListeners();
        soilWaterSlider?.OnValueUpdated.RemoveAllListeners();
        secondsPerDaySlider?.OnValueUpdated.RemoveAllListeners();
        visualGrowthSpeedSlider?.OnValueUpdated.RemoveAllListeners();
    }
}