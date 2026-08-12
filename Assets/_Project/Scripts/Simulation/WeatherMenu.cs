using UnityEngine;
using TMPro;
using MrtkSlider = MixedReality.Toolkit.UX.Slider;

[RequireComponent(typeof(GeneralMenuController))]
public class WeatherMenu : MonoBehaviour
{
    [Header("Environment Sliders")]
    [SerializeField] private MrtkSlider minimumTemperatureSlider;
    [SerializeField] private MrtkSlider maximumTemperatureSlider;
    [SerializeField] private MrtkSlider dayLengthSlider;
    [SerializeField] private MrtkSlider solarRadiationSlider;
    [SerializeField] private MrtkSlider soilWaterSlider;

    [Header("UI Labels")]
    [SerializeField] private TextMeshProUGUI minimumTemperatureLabel;
    [SerializeField] private TextMeshProUGUI maximumTemperatureLabel;
    [SerializeField] private TextMeshProUGUI dayLengthLabel;
    [SerializeField] private TextMeshProUGUI solarRadiationLabel;
    [SerializeField] private TextMeshProUGUI soilWaterLabel;

    private GeneralMenuController menuController;
    private float minimumTemperature;
    private float maximumTemperature;
    private float dayLength;
    private float solarRadiation;
    private float soilWater;

    private void Start()
    {
        menuController = GetComponent<GeneralMenuController>();
        if (menuController.Simulation == null) return;

        var sim = menuController.Simulation;
        minimumTemperature = sim.CurrentMinimumTemperature;
        maximumTemperature = sim.CurrentMaximumTemperature;
        dayLength = sim.CurrentDayLength;
        solarRadiation = sim.CurrentSolarRadiation;
        soilWater = sim.SoilWaterAvailability;

        menuController.SetupSlider(dayLengthSlider, dayLength, value => 
        { 
            dayLength = value; 
            if (dayLengthLabel != null) dayLengthLabel.text = "Day Length: " + dayLength.ToString("F1") + " hours";
            ApplyEnvironment(); 
        });

        menuController.SetupSlider(solarRadiationSlider, solarRadiation, value => 
        { 
            solarRadiation = value; 
            if (solarRadiationLabel != null) solarRadiationLabel.text = "Solar Radiation: " + solarRadiation.ToString("F1") + " W/m²";
            ApplyEnvironment(); 
        });

        menuController.SetupSlider(soilWaterSlider, soilWater, value => 
        { 
            soilWater = value; 
            if (soilWaterLabel != null) soilWaterLabel.text = "Soil Water: " + soilWater.ToString("F1");
            ApplyEnvironment(); 
        });

        menuController.SetupSlider(minimumTemperatureSlider, minimumTemperature, OnMinTemperatureChanged);
        menuController.SetupSlider(maximumTemperatureSlider, maximumTemperature, OnMaxTemperatureChanged);
    }

    private void OnMinTemperatureChanged(float value)
    {
        minimumTemperature = value;
        if (minimumTemperature > maximumTemperature)
        {
            maximumTemperature = minimumTemperature;
            if (maximumTemperatureSlider != null) maximumTemperatureSlider.Value = maximumTemperature;
        }
        if (minimumTemperatureLabel != null) minimumTemperatureLabel.text = "Minimum Temperature: " + minimumTemperature.ToString("F1") + "°C";
        ApplyEnvironment();
    }

    private void OnMaxTemperatureChanged(float value)
    {
        maximumTemperature = value;
        if (maximumTemperature < minimumTemperature)
        {
            minimumTemperature = maximumTemperature;
            if (minimumTemperatureSlider != null) minimumTemperatureSlider.Value = minimumTemperature;
        }
        if (maximumTemperatureLabel != null) maximumTemperatureLabel.text = "Maximum Temperature: " + maximumTemperature.ToString("F1") + "°C";
        ApplyEnvironment();
    }

    private void ApplyEnvironment()
    {
        if (menuController.Simulation == null) return;

        minimumTemperature = Mathf.Clamp(minimumTemperature, -10f, 45f);
        maximumTemperature = Mathf.Clamp(maximumTemperature, -10f, 45f);

        menuController.Simulation.SetEnvironmentParameters(
            minimumTemperature, maximumTemperature, dayLength, solarRadiation, soilWater);
    }

    private void OnDestroy()
    {
        minimumTemperatureSlider?.OnValueUpdated.RemoveAllListeners();
        maximumTemperatureSlider?.OnValueUpdated.RemoveAllListeners();
        dayLengthSlider?.OnValueUpdated.RemoveAllListeners();
        solarRadiationSlider?.OnValueUpdated.RemoveAllListeners();
        soilWaterSlider?.OnValueUpdated.RemoveAllListeners();
    }
}