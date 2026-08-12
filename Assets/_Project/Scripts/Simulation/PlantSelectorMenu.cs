using UnityEngine;
using TMPro; 
using MrtkSlider = MixedReality.Toolkit.UX.Slider;

[RequireComponent(typeof(GeneralMenuController))]
public class PlantSelectorMenu : MonoBehaviour
{
    [Header("Plant Config Sliders")]
    [SerializeField] private MrtkSlider sunflowerCountSlider;
    [SerializeField] private MrtkSlider cassavaCountSlider;

    [Header("UI Labels")]
    [SerializeField] private TextMeshProUGUI sunflowerLabel;
    [SerializeField] private TextMeshProUGUI cassavaLabel;

    private GeneralMenuController menuController;
    private int sunflowerCount;
    private int cassavaCount;

    private void Start()
    {
        menuController = GetComponent<GeneralMenuController>();
        if (menuController.Simulation == null) return;

        sunflowerCount = menuController.Simulation.RequestedSunflowerCount;
        cassavaCount = menuController.Simulation.RequestedCassavaCount;

        menuController.SetupSlider(sunflowerCountSlider, sunflowerCount, value => 
        { 
            sunflowerCount = Mathf.RoundToInt(value); 
            if (sunflowerLabel != null) sunflowerLabel.text = "Sunflower Count: " + sunflowerCount;
            ApplyPlantCounts(); 
        });

        menuController.SetupSlider(cassavaCountSlider, cassavaCount, value => 
        { 
            cassavaCount = Mathf.RoundToInt(value); 
            if (cassavaLabel != null) cassavaLabel.text = "Cassava Count: " + cassavaCount;
            ApplyPlantCounts(); 
        });
    }

    private void ApplyPlantCounts()
    {
        menuController.Simulation?.SetFieldConfiguration(sunflowerCount, cassavaCount);
    }

    private void OnDestroy()
    {
        sunflowerCountSlider?.OnValueUpdated.RemoveAllListeners();
        cassavaCountSlider?.OnValueUpdated.RemoveAllListeners();
    }
}
