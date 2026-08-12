using UnityEngine;
using MixedReality.Toolkit.UX; 
using ICI.PlantGrowth.Phenology; 

public class GeneralMenuController : MonoBehaviour
{
    [Header("Core Simulation")]
    [Tooltip("Leave empty to auto-find in the scene")]
    [SerializeField] private PlantPhenologySimulation simulation;

    // Expose the simulation so other menus can access it
    public PlantPhenologySimulation Simulation => simulation;

    private void Awake()
    {
        // Resolve Dependencies
        if (simulation == null)
        {
            simulation = FindObjectOfType<PlantPhenologySimulation>(true);
        }

        if (simulation == null)
        {
            Debug.LogError("GeneralMenuController: PlantPhenologySimulation not found in scene!");
        }
    }

    // Shared utility for setting up MRTK3 sliders
    public void SetupSlider(Slider slider, float initialValue, System.Action<float> onValueChanged)
    {
        if (slider != null)
        {
            slider.Value = initialValue;
            slider.OnValueUpdated.AddListener((SliderEventData data) => 
            {
                onValueChanged?.Invoke(data.NewValue);
            });
            onValueChanged?.Invoke(initialValue);
        }
    }
}