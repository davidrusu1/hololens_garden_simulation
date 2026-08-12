using UnityEngine;
using MixedReality.Toolkit.UX; 
using TMPro; 
using MrtkSlider = MixedReality.Toolkit.UX.Slider;

[RequireComponent(typeof(GeneralMenuController))]
public class SimulationMenu : MonoBehaviour
{
    [Header("Simulation Status")]
    [SerializeField] private TextMeshProUGUI currentDayLabel;

    [Header("Playback Buttons")]
    [SerializeField] private PressableButton startSimulationButton;
    [SerializeField] private PressableButton pauseSimulationButton;
    [SerializeField] private PressableButton resumeSimulationButton;
    [SerializeField] private PressableButton stopSimulationButton;

    [Header("Speed Sliders")]
    [SerializeField] private MrtkSlider secondsPerDaySlider;
    [SerializeField] private MrtkSlider visualGrowthSpeedSlider;
    
    [Header("Speed Labels")]
    [SerializeField] private TextMeshProUGUI secondsPerDayLabel;
    [SerializeField] private TextMeshProUGUI visualGrowthSpeedLabel;

    private GeneralMenuController menuController;
    private float secondsPerDay;
    private float visualGrowthSpeed;

    private void Start()
    {
        menuController = GetComponent<GeneralMenuController>();
        if (menuController.Simulation == null) return;

        secondsPerDay = menuController.Simulation.SecondsPerSimulatedDay;
        visualGrowthSpeed = menuController.Simulation.VisualGrowthSpeed;

        menuController.SetupSlider(secondsPerDaySlider, secondsPerDay, value => 
        { 
            secondsPerDay = value; 
            if (secondsPerDayLabel != null) secondsPerDayLabel.text = "Seconds per Day: " + secondsPerDay.ToString("F1");
            ApplyPlaybackParameters(); 
        });

        menuController.SetupSlider(visualGrowthSpeedSlider, visualGrowthSpeed, value => 
        { 
            visualGrowthSpeed = value; 
            if (visualGrowthSpeedLabel != null) visualGrowthSpeedLabel.text = "Visual Growth Speed: " + visualGrowthSpeed.ToString("F1");
            ApplyPlaybackParameters(); 
        });

        // Wire up buttons
        startSimulationButton?.OnClicked.AddListener(() => menuController.Simulation.StartSimulation());
        pauseSimulationButton?.OnClicked.AddListener(() => menuController.Simulation.PauseSimulation());
        resumeSimulationButton?.OnClicked.AddListener(() => menuController.Simulation.ResumeSimulation());
        stopSimulationButton?.OnClicked.AddListener(() => menuController.Simulation.StopSimulation());
    }

    private void Update()
    {
        if (menuController == null || menuController.Simulation == null) return;
        
        var sim = menuController.Simulation;

        if (currentDayLabel != null)
        {
            currentDayLabel.text = "Current Day: " + sim.CurrentDay;
        }

        bool isRunning = sim.IsRunning;
        bool isPaused = sim.StatusMessage == "Simulare în pauză."; 

        if (startSimulationButton != null)
            startSimulationButton.gameObject.SetActive(!isRunning && !isPaused); 

        if (pauseSimulationButton != null)
            pauseSimulationButton.gameObject.SetActive(isRunning); 

        if (resumeSimulationButton != null)
            resumeSimulationButton.gameObject.SetActive(!isRunning && isPaused); 

        if (stopSimulationButton != null)
            stopSimulationButton.gameObject.SetActive(isRunning || isPaused); 
    }

    private void ApplyPlaybackParameters()
    {
        menuController.Simulation?.SetPlaybackParameters(secondsPerDay, visualGrowthSpeed);
    }

    private void OnDestroy()
    {
        secondsPerDaySlider?.OnValueUpdated.RemoveAllListeners();
        visualGrowthSpeedSlider?.OnValueUpdated.RemoveAllListeners();
    }
}