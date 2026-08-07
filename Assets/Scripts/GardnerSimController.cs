using System;
using System.Collections;
using ICI.ArucoTracking;
using ICI.PlantGrowth.MixedCrops;
using ICI.PlantGrowth.Phenology;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DefaultExecutionOrder(-2500)]
[DisallowMultipleComponent]
public sealed class GardnerSimController : MonoBehaviour
{
    public enum PlantChoice
    {
        Maple,
        Sunflower,
        Both
    }

    private const float PanelWidth = 1180f;
    private const float PanelHeight = 850f;

    [Header("Integrated simulations")]
    [SerializeField] private GameObject mapleRoot;
    [SerializeField] private Plant mapleSimulation;
    [SerializeField] private GameObject sunflowerRoot;
    [SerializeField] private PlantPhenologySimulation sunflowerSimulation;
    [SerializeField] private MixedCropFieldController sunflowerField;
    [SerializeField] private Transform gardenContent;

    [Header("Initial experience")]
    [SerializeField] private PlantChoice selectedPlant = PlantChoice.Both;
    [SerializeField] private bool startAutomatically;

    [Header("Comfortable HoloLens placement")]
    [SerializeField, Min(0.5f)] private float plantsDistance = 2.25f;
    [SerializeField, Min(0f)] private float plantsBelowEyeLevel = 0.95f;
    [SerializeField, Min(0.5f)] private float panelDistance = 1.1f;
    [SerializeField] private float panelHorizontalOffset = -0.35f;
    [SerializeField] private float panelVerticalOffset = -0.05f;
    [SerializeField, Min(0.0001f)] private float panelWorldScale = 0.00055f;
    [SerializeField, Range(0.1f, 1f)] private float showcaseScale = 0.38f;
    [SerializeField, Min(0.4f)] private float sideBySideSpacing = 3.8f;

    private static readonly Color PanelColor =
        new Color(0.965f, 0.978f, 0.958f, 0.985f);
    private static readonly Color SectionColor =
        new Color(1f, 1f, 1f, 0.94f);
    private static readonly Color GreenColor =
        new Color(0.075f, 0.39f, 0.16f, 1f);
    private static readonly Color SelectedGreen =
        new Color(0.10f, 0.52f, 0.22f, 1f);
    private static readonly Color IdleGreen =
        new Color(0.76f, 0.86f, 0.78f, 1f);
    private static readonly Color ActionOrange =
        new Color(0.92f, 0.38f, 0.08f, 1f);
    private static readonly Color DarkText =
        new Color(0.06f, 0.11f, 0.075f, 1f);
    private static readonly Color MutedText =
        new Color(0.28f, 0.35f, 0.30f, 1f);
    private static readonly Color TrackColor =
        new Color(0.77f, 0.83f, 0.78f, 1f);

    private Camera viewCamera;
    private GameObject panelObject;
    private Canvas worldCanvas;
    private TMP_Text informationText;
    private TMP_Text statusText;
    private TMP_Text runStateText;
    private Button mapleChoiceButton;
    private Button sunflowerChoiceButton;
    private Button bothChoiceButton;
    private Button startButton;
    private Button pauseButton;
    private Slider minimumTemperatureSlider;
    private Slider maximumTemperatureSlider;
    private EventSystem configuredEventSystem;
    private XRUIInputModule configuredInputModule;
    private bool placementCompleted;
    private bool experienceRunning;
    private bool mapleVisualStarted;
    private bool sunflowerVisualStarted;
    private bool valuesInitialized;
    private float minimumTemperature;
    private float maximumTemperature;
    private float dayLength;
    private float solarRadiation;
    private float soilWater;
    private float playbackSpeed;

    private void Awake()
    {
        ResolveReferences();
        DisableLegacyPanels();
        InitializeValues();
        ApplyEnvironment();
        PauseAllSimulations();
        ApplyPlantChoice();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BuildWorldPanel();
        StartCoroutine(InitializeAfterTrackingStarts());
    }

    private void Start()
    {
        if (startAutomatically)
        {
            StartSelectedPlants();
        }
    }

    private void Update()
    {
        ResolveViewCamera();
        ConfigureEventSystem();
        if (!placementCompleted && viewCamera != null)
        {
            RecenterExperience();
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            RecenterExperience();
        }

        RefreshStatus();
    }

    private void OnDestroy()
    {
        panelObject = null;
    }

    public void SelectMaple()
    {
        SetPlantChoice(PlantChoice.Maple);
    }

    public void SelectSunflower()
    {
        SetPlantChoice(PlantChoice.Sunflower);
    }

    public void SelectBoth()
    {
        SetPlantChoice(PlantChoice.Both);
    }

    public void StartSelectedPlants()
    {
        ResolveReferences();
        experienceRunning = true;

        if (UsesMaple())
        {
            if (mapleRoot != null)
            {
                mapleRoot.SetActive(true);
            }

            SetMapleBranchGrowthEnabled(true);
            mapleSimulation?.StartSimulation();
            mapleVisualStarted = true;
        }
        else
        {
            PauseMaple();
        }

        if (UsesSunflower())
        {
            if (sunflowerRoot != null)
            {
                sunflowerRoot.SetActive(true);
            }

            if (sunflowerSimulation != null)
            {
                if (sunflowerVisualStarted)
                {
                    sunflowerSimulation.ResumeSimulation();
                }
                else
                {
                    sunflowerSimulation.StartSimulation();
                    sunflowerVisualStarted = true;
                }
            }
        }
        else
        {
            sunflowerSimulation?.StopSimulation();
        }

        UpdateButtonState();
    }

    public void PauseSelectedPlants()
    {
        experienceRunning = false;
        PauseAllSimulations();
        UpdateButtonState();
    }

    public void RestartSelectedPlants()
    {
        ResolveReferences();
        ApplyEnvironment();
        experienceRunning = true;

        if (UsesMaple() && mapleSimulation != null)
        {
            mapleRoot?.SetActive(true);
            SetMapleBranchGrowthEnabled(true);
            if (mapleVisualStarted)
            {
                mapleSimulation.RestartCompleteSimulation();
            }
            else
            {
                mapleSimulation.ResetSimulation();
                mapleSimulation.StartSimulation();
                mapleVisualStarted = true;
            }
        }

        if (UsesSunflower())
        {
            sunflowerRoot?.SetActive(true);
            sunflowerSimulation?.StartSimulation();
            sunflowerVisualStarted = true;
        }

        UpdateButtonState();
    }

    public void RecenterExperience()
    {
        ResolveViewCamera();
        if (viewCamera == null)
        {
            placementCompleted = false;
            return;
        }

        Transform cameraTransform = viewCamera.transform;
        Vector3 forward = Vector3.ProjectOnPlane(
            cameraTransform.forward,
            Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        if (gardenContent != null)
        {
            gardenContent.localScale = Vector3.one * showcaseScale;
            ArucoPlantAnchor arucoAnchor =
                FindObjectOfType<ArucoPlantAnchor>(true);
            if (arucoAnchor == null || !arucoAnchor.HasPose)
            {
                gardenContent.position = cameraTransform.position
                    + forward * plantsDistance
                    - Vector3.up * plantsBelowEyeLevel;
                gardenContent.rotation = Quaternion.LookRotation(
                    forward,
                    Vector3.up);
            }
        }

        if (panelObject != null)
        {
            Transform panelTransform = panelObject.transform;
            panelTransform.position = cameraTransform.position
                + forward * panelDistance
                + right * panelHorizontalOffset
                + Vector3.up * panelVerticalOffset;
            Vector3 cameraToPanel =
                panelTransform.position - cameraTransform.position;
            panelTransform.rotation = Quaternion.LookRotation(
                cameraToPanel.normalized,
                Vector3.up);
            panelTransform.localScale = Vector3.one * panelWorldScale;
        }

        placementCompleted = true;
    }

    private IEnumerator InitializeAfterTrackingStarts()
    {
        yield return null;
        yield return null;
        DisableLegacyPanels();
        ResolveViewCamera();
        RecenterExperience();
        RefreshStatus();
    }

    private void ResolveReferences()
    {
        if (mapleSimulation == null)
        {
            mapleSimulation = FindObjectOfType<Plant>(true);
        }
        if (mapleRoot == null && mapleSimulation != null)
        {
            mapleRoot = mapleSimulation.gameObject;
        }
        if (sunflowerSimulation == null)
        {
            sunflowerSimulation =
                FindObjectOfType<PlantPhenologySimulation>(true);
        }
        if (sunflowerRoot == null && sunflowerSimulation != null)
        {
            sunflowerRoot = sunflowerSimulation.gameObject;
        }
        if (sunflowerField == null && sunflowerRoot != null)
        {
            sunflowerField =
                sunflowerRoot.GetComponent<MixedCropFieldController>();
        }
        if (gardenContent == null)
        {
            Transform candidate = transform.Find("Garden Content");
            if (candidate != null)
            {
                gardenContent = candidate;
            }
        }
    }

    private void ResolveViewCamera()
    {
        if (viewCamera != null
            && viewCamera.enabled
            && viewCamera.gameObject.activeInHierarchy)
        {
            return;
        }

        if (Camera.main != null
            && Camera.main.enabled
            && Camera.main.gameObject.activeInHierarchy)
        {
            viewCamera = Camera.main;
        }
        else
        {
            Camera[] cameras = FindObjectsOfType<Camera>(true);
            for (int index = 0; index < cameras.Length; index++)
            {
                if (cameras[index].enabled
                    && cameras[index].gameObject.activeInHierarchy)
                {
                    viewCamera = cameras[index];
                    break;
                }
            }
        }

        if (worldCanvas != null)
        {
            worldCanvas.worldCamera = viewCamera;
        }
    }

    private void DisableLegacyPanels()
    {
        if (mapleRoot != null)
        {
            HoloLensSimulationPanel holoLensPanel =
                mapleRoot.GetComponent<HoloLensSimulationPanel>();
            if (holoLensPanel != null)
            {
                holoLensPanel.SetMenuVisible(false);
                holoLensPanel.enabled = false;
            }

            MapleSimulationPanel maplePanel =
                mapleRoot.GetComponent<MapleSimulationPanel>();
            if (maplePanel != null)
            {
                maplePanel.SetRuntimePanelVisible(false);
                maplePanel.enabled = false;
            }
        }

        if (sunflowerSimulation != null)
        {
            sunflowerSimulation.SetRuntimePanelVisible(false);
        }
    }

    private void InitializeValues()
    {
        if (valuesInitialized)
        {
            return;
        }

        if (sunflowerSimulation != null)
        {
            minimumTemperature = sunflowerSimulation.CurrentMinimumTemperature;
            maximumTemperature = sunflowerSimulation.CurrentMaximumTemperature;
            dayLength = sunflowerSimulation.CurrentDayLength;
            solarRadiation = sunflowerSimulation.CurrentSolarRadiation;
            soilWater = sunflowerSimulation.SoilWaterAvailability;
            playbackSpeed = sunflowerSimulation.VisualGrowthSpeed;
        }
        else if (mapleSimulation != null)
        {
            minimumTemperature = mapleSimulation.AverageTemperature - 6f;
            maximumTemperature = mapleSimulation.AverageTemperature + 6f;
            dayLength = mapleSimulation.DayLengthHours;
            solarRadiation = mapleSimulation.GlobalRadiation;
            soilWater = mapleSimulation.RelativeSoilWater;
            playbackSpeed = mapleSimulation.PlaybackSpeed;
        }
        else
        {
            minimumTemperature = 16f;
            maximumTemperature = 28f;
            dayLength = 14f;
            solarRadiation = 18f;
            soilWater = 0.8f;
            playbackSpeed = 1f;
        }

        valuesInitialized = true;
    }

    private void ApplyEnvironment()
    {
        if (minimumTemperature > maximumTemperature)
        {
            float swap = minimumTemperature;
            minimumTemperature = maximumTemperature;
            maximumTemperature = swap;
        }

        float averageTemperature =
            (minimumTemperature + maximumTemperature) * 0.5f;
        mapleSimulation?.SetEnvironment(
            averageTemperature,
            solarRadiation,
            soilWater,
            dayLength);
        mapleSimulation?.SetPlaybackSpeed(playbackSpeed);
        mapleSimulation?.SetSecondsPerSimulatedDay(0.5f);

        sunflowerSimulation?.SetEnvironmentParameters(
            minimumTemperature,
            maximumTemperature,
            dayLength,
            solarRadiation,
            soilWater);
        sunflowerSimulation?.SetPlaybackParameters(0.5f, playbackSpeed);
    }

    private void SetPlantChoice(PlantChoice choice)
    {
        PauseAllSimulations();
        experienceRunning = false;
        selectedPlant = choice;
        ApplyPlantChoice();
        UpdateButtonState();
        RefreshStatus();
    }

    private void ApplyPlantChoice()
    {
        bool mapleVisible = UsesMaple();
        bool sunflowerVisible = UsesSunflower();

        if (gardenContent != null)
        {
            gardenContent.localScale = Vector3.one * showcaseScale;
        }

        if (mapleRoot != null)
        {
            mapleRoot.SetActive(mapleVisible);
            mapleRoot.transform.localPosition = selectedPlant == PlantChoice.Both
                ? Vector3.left * (sideBySideSpacing * 0.5f)
                : Vector3.zero;
        }

        if (sunflowerRoot != null)
        {
            sunflowerRoot.SetActive(sunflowerVisible);
            sunflowerRoot.transform.localPosition = selectedPlant == PlantChoice.Both
                ? Vector3.right * (sideBySideSpacing * 0.5f)
                : Vector3.zero;
        }

        UpdateSelectionColors();
        RefreshInformation();
    }

    private bool UsesMaple()
    {
        return selectedPlant == PlantChoice.Maple
            || selectedPlant == PlantChoice.Both;
    }

    private bool UsesSunflower()
    {
        return selectedPlant == PlantChoice.Sunflower
            || selectedPlant == PlantChoice.Both;
    }

    private void PauseAllSimulations()
    {
        PauseMaple();
        sunflowerSimulation?.StopSimulation();
    }

    private void PauseMaple()
    {
        mapleSimulation?.PauseSimulation();
    }

    private void SetMapleBranchGrowthEnabled(bool enabled)
    {
        if (mapleRoot == null)
        {
            return;
        }

        Branch[] branches = mapleRoot.GetComponentsInChildren<Branch>(true);
        for (int index = 0; index < branches.Length; index++)
        {
            if (branches[index] != null)
            {
                branches[index].enabled = enabled;
            }
        }
    }

    private void BuildWorldPanel()
    {
        if (panelObject != null)
        {
            return;
        }

        panelObject = new GameObject(
            "Gardner sim UI",
            typeof(RectTransform));
        panelObject.transform.SetParent(transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRect.localScale = Vector3.one * panelWorldScale;

        worldCanvas = panelObject.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = 500;

        CanvasScaler scaler = panelObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 18f;
        panelObject.AddComponent<GraphicRaycaster>();
        TrackedDeviceGraphicRaycaster trackedRaycaster =
            panelObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        trackedRaycaster.ignoreReversedGraphics = false;

        Image background = panelObject.AddComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = true;

        CreateText(
            "Title",
            panelObject.transform,
            "Gardner sim",
            46f,
            FontStyles.Bold,
            GreenColor,
            TextAlignmentOptions.Center,
            20f,
            14f,
            PanelWidth - 40f,
            58f);
        CreateText(
            "Subtitle",
            panelObject.transform,
            "Simulare interactivă pentru HoloLens 2",
            24f,
            FontStyles.Normal,
            MutedText,
            TextAlignmentOptions.Center,
            20f,
            67f,
            PanelWidth - 40f,
            34f);

        CreateSectionBackground("Control Section", 24f, 112f, 548f, 710f);
        CreateSectionBackground("Information Section", 594f, 112f, 562f, 402f);
        CreateSectionBackground("Status Section", 594f, 532f, 562f, 290f);

        CreateText(
            "Plant Choice Label",
            panelObject.transform,
            "CE VREI SĂ CREȘTI?",
            25f,
            FontStyles.Bold,
            GreenColor,
            TextAlignmentOptions.Left,
            46f,
            128f,
            500f,
            34f);

        mapleChoiceButton = CreateButton(
            "Maple Choice",
            "Arțar",
            46f,
            172f,
            154f,
            64f,
            SelectMaple);
        sunflowerChoiceButton = CreateButton(
            "Sunflower Choice",
            "Floarea-soarelui",
            207f,
            172f,
            208f,
            64f,
            SelectSunflower);
        bothChoiceButton = CreateButton(
            "Both Choice",
            "Ambele",
            422f,
            172f,
            128f,
            64f,
            SelectBoth);

        startButton = CreateButton(
            "Start",
            "Pornește",
            46f,
            250f,
            154f,
            64f,
            StartSelectedPlants,
            SelectedGreen);
        pauseButton = CreateButton(
            "Pause",
            "Pauză",
            207f,
            250f,
            154f,
            64f,
            PauseSelectedPlants);
        CreateButton(
            "Restart",
            "Repornește",
            368f,
            250f,
            182f,
            64f,
            RestartSelectedPlants,
            ActionOrange);

        CreateText(
            "Environment Label",
            panelObject.transform,
            "MEDIU COMUN",
            25f,
            FontStyles.Bold,
            GreenColor,
            TextAlignmentOptions.Left,
            46f,
            328f,
            500f,
            34f);

        minimumTemperatureSlider = CreateSliderRow(
            "Temperatură minimă",
            "°C",
            minimumTemperature,
            -10f,
            45f,
            46f,
            370f,
            value =>
            {
                minimumTemperature = Mathf.Min(value, maximumTemperature);
                minimumTemperatureSlider.SetValueWithoutNotify(
                    minimumTemperature);
                ApplyEnvironment();
            });
        maximumTemperatureSlider = CreateSliderRow(
            "Temperatură maximă",
            "°C",
            maximumTemperature,
            -10f,
            45f,
            46f,
            438f,
            value =>
            {
                maximumTemperature = Mathf.Max(value, minimumTemperature);
                maximumTemperatureSlider.SetValueWithoutNotify(
                    maximumTemperature);
                ApplyEnvironment();
            });
        CreateSliderRow(
            "Durata zilei",
            "h",
            dayLength,
            6f,
            18f,
            46f,
            506f,
            value =>
            {
                dayLength = value;
                ApplyEnvironment();
            });
        CreateSliderRow(
            "Radiație solară",
            "MJ/m²",
            solarRadiation,
            0f,
            35f,
            46f,
            574f,
            value =>
            {
                solarRadiation = value;
                ApplyEnvironment();
            });
        CreateSliderRow(
            "Apă disponibilă în sol",
            string.Empty,
            soilWater,
            0f,
            1f,
            46f,
            642f,
            value =>
            {
                soilWater = value;
                ApplyEnvironment();
            });
        CreateSliderRow(
            "Viteză simulare",
            "x",
            playbackSpeed,
            0.25f,
            4f,
            46f,
            710f,
            value =>
            {
                playbackSpeed = value;
                ApplyEnvironment();
            });

        CreateText(
            "Information Title",
            panelObject.transform,
            "DESPRE PROIECT",
            27f,
            FontStyles.Bold,
            GreenColor,
            TextAlignmentOptions.Left,
            620f,
            132f,
            510f,
            38f);
        informationText = CreateText(
            "Information",
            panelObject.transform,
            string.Empty,
            24f,
            FontStyles.Normal,
            DarkText,
            TextAlignmentOptions.TopLeft,
            620f,
            180f,
            510f,
            304f);
        informationText.enableWordWrapping = true;
        informationText.overflowMode = TextOverflowModes.Ellipsis;

        CreateText(
            "Status Title",
            panelObject.transform,
            "STAREA SIMULĂRII",
            27f,
            FontStyles.Bold,
            GreenColor,
            TextAlignmentOptions.Left,
            620f,
            550f,
            510f,
            38f);
        runStateText = CreateText(
            "Run State",
            panelObject.transform,
            "Pregătită",
            25f,
            FontStyles.Bold,
            ActionOrange,
            TextAlignmentOptions.Left,
            620f,
            594f,
            510f,
            36f);
        statusText = CreateText(
            "Status",
            panelObject.transform,
            string.Empty,
            23f,
            FontStyles.Normal,
            DarkText,
            TextAlignmentOptions.TopLeft,
            620f,
            636f,
            510f,
            116f);
        statusText.enableWordWrapping = true;

        CreateButton(
            "Recenter",
            "Recentrează meniul",
            620f,
            758f,
            270f,
            50f,
            RecenterExperience);
        CreateText(
            "Interaction Hint",
            panelObject.transform,
            "Folosește mâna sau raza HoloLens. În Play Mode: R recentrează.",
            19f,
            FontStyles.Normal,
            MutedText,
            TextAlignmentOptions.Center,
            902f,
            758f,
            228f,
            50f);

        ResolveViewCamera();
        worldCanvas.worldCamera = viewCamera;
        ConfigureEventSystem();
        UpdateSelectionColors();
        UpdateButtonState();
        RefreshInformation();
        RefreshStatus();
    }

    private void CreateSectionBackground(
        string name,
        float left,
        float top,
        float width,
        float height)
    {
        GameObject section = CreateRectObject(name, panelObject.transform);
        Image image = section.AddComponent<Image>();
        image.color = SectionColor;
        image.raycastTarget = false;
        SetRect(section.GetComponent<RectTransform>(), left, top, width, height);
    }

    private Button CreateButton(
        string name,
        string label,
        float left,
        float top,
        float width,
        float height,
        UnityEngine.Events.UnityAction action,
        Color? requestedColor = null)
    {
        GameObject buttonObject = CreateRectObject(name, panelObject.transform);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        SetRect(buttonRect, left, top, width, height);

        Image image = buttonObject.AddComponent<Image>();
        image.color = requestedColor ?? IdleGreen;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
        colors.pressedColor = new Color(0.82f, 0.86f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(action);

        TMP_Text text = CreateText(
            "Label",
            buttonObject.transform,
            label,
            23f,
            FontStyles.Bold,
            requestedColor.HasValue && requestedColor.Value == ActionOrange
                ? Color.white
                : DarkText,
            TextAlignmentOptions.Center,
            0f,
            0f,
            width,
            height);
        text.raycastTarget = false;
        return button;
    }

    private Slider CreateSliderRow(
        string label,
        string unit,
        float initialValue,
        float minimum,
        float maximum,
        float left,
        float top,
        Action<float> onValueChanged)
    {
        const float rowWidth = 500f;
        const float rowHeight = 62f;
        GameObject row = CreateRectObject(label, panelObject.transform);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        SetRect(rowRect, left, top, rowWidth, rowHeight);

        TMP_Text valueText = CreateText(
            "Value",
            row.transform,
            FormatValue(initialValue, unit),
            21f,
            FontStyles.Bold,
            GreenColor,
            TextAlignmentOptions.Right,
            342f,
            0f,
            158f,
            28f);
        CreateText(
            "Label",
            row.transform,
            label,
            21f,
            FontStyles.Normal,
            DarkText,
            TextAlignmentOptions.Left,
            0f,
            0f,
            340f,
            28f);

        GameObject track = CreateRectObject("Track", row.transform);
        RectTransform trackRect = track.GetComponent<RectTransform>();
        SetRect(trackRect, 0f, 34f, rowWidth, 24f);
        Image trackImage = track.AddComponent<Image>();
        trackImage.color = TrackColor;

        GameObject fillArea = CreateRectObject("Fill Area", track.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        SetStretch(fillAreaRect, 8f, 0f, 8f, 0f);

        GameObject fill = CreateRectObject("Fill", fillArea.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = SelectedGreen;

        GameObject handleArea = CreateRectObject("Handle Area", track.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        SetStretch(handleAreaRect, 8f, 0f, 8f, 0f);

        GameObject handle = CreateRectObject("Handle", handleArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(44f, 44f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = GreenColor;

        Slider slider = row.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.wholeNumbers = false;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.SetValueWithoutNotify(initialValue);
        slider.onValueChanged.AddListener(value =>
        {
            onValueChanged(value);
            valueText.text = FormatValue(slider.value, unit);
        });

        HoloLensSliderInput holoLensInput =
            track.AddComponent<HoloLensSliderInput>();
        holoLensInput.Initialize(slider, trackRect);
        return slider;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string content,
        float fontSize,
        FontStyles style,
        Color color,
        TextAlignmentOptions alignment,
        float left,
        float top,
        float width,
        float height)
    {
        GameObject textObject = CreateRectObject(name, parent);
        SetRect(
            textObject.GetComponent<RectTransform>(),
            left,
            top,
            width,
            height);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void SetRect(
        RectTransform rect,
        float left,
        float top,
        float width,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetStretch(
        RectTransform rect,
        float left,
        float bottom,
        float right,
        float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void ConfigureEventSystem()
    {
        EventSystem[] sceneEventSystems =
            FindObjectsOfType<EventSystem>(true);
        EventSystem eventSystem = FindPreferredEventSystem(
            sceneEventSystems);

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject(
                "XR EventSystem",
                typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        eventSystem.enabled = true;
        for (int index = 0; index < sceneEventSystems.Length; index++)
        {
            EventSystem duplicate = sceneEventSystems[index];
            if (duplicate != null && duplicate != eventSystem)
            {
                duplicate.enabled = false;
            }
        }

        XRUIInputModule inputModule =
            eventSystem.GetComponent<XRUIInputModule>();
        if (inputModule == null)
        {
            BaseInputModule[] oldModules =
                eventSystem.GetComponents<BaseInputModule>();
            for (int index = 0; index < oldModules.Length; index++)
            {
                oldModules[index].enabled = false;
            }
            inputModule =
                eventSystem.gameObject.AddComponent<XRUIInputModule>();
        }

        BaseInputModule[] modules =
            eventSystem.GetComponents<BaseInputModule>();
        for (int index = 0; index < modules.Length; index++)
        {
            if (modules[index] != inputModule)
            {
                modules[index].enabled = false;
            }
        }

        inputModule.enabled = true;
        inputModule.enableXRInput = true;
        inputModule.trackedDeviceDragThresholdMultiplier = 0.25f;
        eventSystem.pixelDragThreshold = 4;
#if UNITY_EDITOR
        inputModule.enableMouseInput = true;
#endif

        if (configuredEventSystem != eventSystem
            || configuredInputModule != inputModule)
        {
            configuredEventSystem = eventSystem;
            configuredInputModule = inputModule;
            RegisterActiveInteractorsWithUi();
        }
    }

    private static EventSystem FindPreferredEventSystem(
        EventSystem[] eventSystems)
    {
        for (int index = 0; index < eventSystems.Length; index++)
        {
            EventSystem candidate = eventSystems[index];
            if (candidate != null
                && candidate.gameObject.activeInHierarchy
                && candidate.GetComponent<XRUIInputModule>() != null)
            {
                return candidate;
            }
        }

        EventSystem current = EventSystem.current;
        if (current != null && current.gameObject.activeInHierarchy)
        {
            return current;
        }

        for (int index = 0; index < eventSystems.Length; index++)
        {
            EventSystem candidate = eventSystems[index];
            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                return candidate;
            }
        }

        return null;
    }

    private static void RegisterActiveInteractorsWithUi()
    {
        NearFarInteractor[] nearFarInteractors =
            FindObjectsOfType<NearFarInteractor>(true);
        for (int index = 0; index < nearFarInteractors.Length; index++)
        {
            NearFarInteractor interactor = nearFarInteractors[index];
            if (interactor != null && interactor.isActiveAndEnabled)
            {
                interactor.enableUIInteraction = false;
                interactor.enableUIInteraction = true;
            }
        }

        XRRayInteractor[] rayInteractors =
            FindObjectsOfType<XRRayInteractor>(true);
        for (int index = 0; index < rayInteractors.Length; index++)
        {
            XRRayInteractor interactor = rayInteractors[index];
            if (interactor != null && interactor.isActiveAndEnabled)
            {
                interactor.enableUIInteraction = false;
                interactor.enableUIInteraction = true;
            }
        }

        XRPokeInteractor[] pokeInteractors =
            FindObjectsOfType<XRPokeInteractor>(true);
        for (int index = 0; index < pokeInteractors.Length; index++)
        {
            XRPokeInteractor interactor = pokeInteractors[index];
            if (interactor != null && interactor.isActiveAndEnabled)
            {
                interactor.enableUIInteraction = false;
                interactor.enableUIInteraction = true;
            }
        }
    }

    private void RefreshInformation()
    {
        if (informationText == null)
        {
            return;
        }

        switch (selectedPlant)
        {
            case PlantChoice.Maple:
                informationText.text =
                    "ARȚAR\n"
                    + "Coroana este construită procedural din ramuri recursive și frunze. "
                    + "Un model WOFOST-inspired calculează DVS, biomasa și răspunsul la lumină, temperatură și apă.\n\n"
                    + "Anotimpurile sunt numai la arțar: frunzele trec spre roșu în toamnă și cad când DVS se apropie de maturitate.";
                break;

            case PlantChoice.Sunflower:
                informationText.text =
                    "FLOAREA-SOARELUI\n"
                    + "Modelul WOFOST-lite prezice DVS din valorile mediului. Tulpina și frunzele urmează DVS 0–1, "
                    + "înflorirea începe exact la DVS 1, iar maturitatea este atinsă la DVS 2.\n\n"
                    + "Biomasa este afișată per plantă, nu ca total al câmpului.";
                break;

            default:
                informationText.text =
                    "COMPARAȚIE ÎN ACELAȘI MEDIU\n"
                    + "Arțarul și floarea-soarelui folosesc aceleași temperaturi, radiație, apă și durată a zilei. "
                    + "Fiecare păstrează propria fenologie și propria animație. "
                    + "Anotimpurile sunt numai la arțar: frunzele se înroșesc și cad.\n\n"
                    + "Alege o specie sau Ambele, apoi apasă Pornește pentru a compara ritmul de creștere și DVS.";
                break;
        }
    }

    private void RefreshStatus()
    {
        if (statusText == null)
        {
            return;
        }

        string mapleStatus = string.Empty;
        if (UsesMaple() && mapleSimulation != null)
        {
            float mapleBiomass = mapleSimulation.RootBiomass
                + mapleSimulation.LeafBiomass
                + mapleSimulation.WoodBiomass;
            mapleStatus =
                $"Arțar · ziua {mapleSimulation.SimulatedDay} · "
                + $"{mapleSimulation.CurrentSeasonLabel} · "
                + $"DVS {mapleSimulation.DevelopmentStage:0.000} · "
                + $"biomasă {mapleBiomass:0.00} kg/plantă";
        }

        string sunflowerStatus = string.Empty;
        if (UsesSunflower() && sunflowerSimulation != null)
        {
            sunflowerStatus =
                $"Floarea-soarelui · ziua {sunflowerSimulation.CurrentDay} · "
                + $"DVS {sunflowerSimulation.CurrentDevelopmentStage:0.000} · "
                + $"biomasă {sunflowerSimulation.TotalDryMatterPerPlantGrams:0.0} g/plantă";
        }

        statusText.text = string.IsNullOrEmpty(mapleStatus)
            ? sunflowerStatus
            : string.IsNullOrEmpty(sunflowerStatus)
                ? mapleStatus
                : mapleStatus + "\n" + sunflowerStatus;

        if (runStateText != null)
        {
            runStateText.text = experienceRunning
                ? "În desfășurare"
                : "Pauză — alege și pornește";
            runStateText.color = experienceRunning
                ? SelectedGreen
                : ActionOrange;
        }
    }

    private void UpdateSelectionColors()
    {
        SetButtonColor(
            mapleChoiceButton,
            selectedPlant == PlantChoice.Maple ? SelectedGreen : IdleGreen);
        SetButtonColor(
            sunflowerChoiceButton,
            selectedPlant == PlantChoice.Sunflower ? SelectedGreen : IdleGreen);
        SetButtonColor(
            bothChoiceButton,
            selectedPlant == PlantChoice.Both ? SelectedGreen : IdleGreen);
    }

    private void UpdateButtonState()
    {
        SetButtonColor(startButton, experienceRunning ? IdleGreen : SelectedGreen);
        SetButtonColor(pauseButton, experienceRunning ? ActionOrange : IdleGreen);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button != null && button.targetGraphic != null)
        {
            button.targetGraphic.color = color;
        }
    }

    private static string FormatValue(float value, string unit)
    {
        return string.IsNullOrEmpty(unit)
            ? value.ToString("0.0")
            : $"{value:0.0} {unit}";
    }
}
