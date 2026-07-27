using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class HoloLensSimulationPanel : MonoBehaviour
{
    private const float MinimumTemperature = -10f;
    private const float MaximumTemperature = 45f;
    private const float ReferenceSecondsPerDay = 0.18f;
    private const float PanelWidth = 720f;
    private const float PanelHeight = 980f;

    [SerializeField]
    private Plant simulation;

    [Header("Stable plant-relative placement")]
    [SerializeField]
    private bool menuVisibleByDefault = true;

    [SerializeField]
    private Vector3 menuLocalOffset = new Vector3(-1.55f, 1.42f, -0.05f);

    [SerializeField, Min(0.0001f)]
    private float worldScale = 0.00062f;

    private float minimumTemperature;
    private float maximumTemperature;
    private float dayLength;
    private float solarRadiation;
    private float soilWater;
    private float secondsPerDay;
    private float calendarSpeedMultiplier = 1f;
    private bool valuesInitialized;
    private bool newSetupActive;
    private bool panelPlacementInitialized;

    private GameObject worldPanelObject;
    private Canvas worldCanvas;
    private Camera viewCamera;
    private TMP_Text statusText;
    private TMP_Text speedText;
    private TMP_Text buttonText;
    private TMP_Text liteModeButtonText;
    private Slider minimumTemperatureSlider;
    private Slider maximumTemperatureSlider;
    private HoloLensPlantPresentation presentation;
    private MapleSimulationPanel legacyPanel;

    private static readonly Color PanelColor =
        new Color(0.98f, 0.985f, 0.975f, 0.98f);
    private static readonly Color SectionColor =
        new Color(0.08f, 0.42f, 0.18f, 1f);
    private static readonly Color TextColor =
        new Color(0.08f, 0.12f, 0.09f, 1f);
    private static readonly Color MutedTextColor =
        new Color(0.32f, 0.39f, 0.34f, 1f);
    private static readonly Color TrackColor =
        new Color(0.82f, 0.87f, 0.83f, 1f);
    private static readonly Color FillColor =
        new Color(0.12f, 0.56f, 0.23f, 1f);

    private void Awake()
    {
        ResolveSimulation();
        InitializeValues();
        presentation = GetComponent<HoloLensPlantPresentation>();
        newSetupActive = menuVisibleByDefault;
        ResolveLegacyPanel();
        ApplySetupVisibility();
    }

    private void OnEnable()
    {
        ResolveSimulation();
        InitializeValues();
        BuildWorldPanel();
    }

    private void Update()
    {
        ResolveSimulation();
        InitializeValues();
        ResolveViewCamera();
        ConfigureEventSystem();
        TryPlacePanelAtPlant();
        ApplySetupVisibility();
        RefreshStatus();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
        {
            ToggleSetup();
        }
    }

    private void OnDestroy()
    {
        if (worldPanelObject != null)
        {
            Destroy(worldPanelObject);
        }
    }

    private void ResolveSimulation()
    {
        if (simulation == null)
        {
            simulation = GetComponent<Plant>();
        }

        if (simulation == null)
        {
            simulation = FindObjectOfType<Plant>();
        }
    }

    private void InitializeValues()
    {
        if (valuesInitialized || simulation == null)
        {
            return;
        }

        minimumTemperature = simulation.AverageTemperature - 6f;
        maximumTemperature = simulation.AverageTemperature + 6f;
        dayLength = simulation.DayLengthHours;
        solarRadiation = simulation.GlobalRadiation;
        soilWater = simulation.RelativeSoilWater;
        secondsPerDay = simulation.SecondsPerSimulatedDay;
        calendarSpeedMultiplier = simulation.PlaybackSpeed;
        valuesInitialized = true;
    }

    private void ApplyPanelValues()
    {
        if (simulation == null)
        {
            return;
        }

        NormalizeTemperatureRange();
        simulation.SetEnvironment(
            (minimumTemperature + maximumTemperature) * 0.5f,
            solarRadiation,
            soilWater,
            dayLength);
        simulation.SetSecondsPerSimulatedDay(secondsPerDay);
        simulation.SetPlaybackSpeed(calendarSpeedMultiplier);
        RefreshSpeedText();
    }

    private void BuildWorldPanel()
    {
        if (worldPanelObject != null)
        {
            return;
        }

        worldPanelObject = new GameObject(
            "HoloLens Simulation Menu",
            typeof(RectTransform));
        RectTransform canvasRect =
            worldPanelObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        canvasRect.localScale = Vector3.one * worldScale;

        worldCanvas = worldPanelObject.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = 100;

        CanvasScaler scaler = worldPanelObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 16f;
        worldPanelObject.AddComponent<GraphicRaycaster>();
        TrackedDeviceGraphicRaycaster trackedRaycaster =
            worldPanelObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        trackedRaycaster.ignoreReversedGraphics = false;

        Image background = worldPanelObject.AddComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = true;

        TMP_Text title = CreateText(
            "Titlu",
            worldPanelObject.transform,
            "SIMULARE FENOLOGICĂ",
            38f,
            FontStyles.Bold,
            SectionColor,
            TextAlignmentOptions.Center);
        SetTopStretch(title.rectTransform, 18f, 48f, 24f, 24f);

        TMP_Text subtitle = CreateText(
            "Subtitlu",
            worldPanelObject.transform,
            "Arțar (Acer) · control HoloLens în timp real",
            23f,
            FontStyles.Normal,
            MutedTextColor,
            TextAlignmentOptions.Center);
        SetTopStretch(subtitle.rectTransform, 66f, 34f, 24f, 24f);

        float top = 110f;
        minimumTemperatureSlider = CreateSliderRow(
            "Temperatură minimă",
            "°C",
            minimumTemperature,
            MinimumTemperature,
            MaximumTemperature,
            top,
            value =>
            {
                minimumTemperature = value;
                if (minimumTemperature > maximumTemperature)
                {
                    maximumTemperature = minimumTemperature;
                    maximumTemperatureSlider?.SetValueWithoutNotify(
                        maximumTemperature);
                }
                ApplyPanelValues();
            });
        top += 78f;

        maximumTemperatureSlider = CreateSliderRow(
            "Temperatură maximă",
            "°C",
            maximumTemperature,
            MinimumTemperature,
            MaximumTemperature,
            top,
            value =>
            {
                maximumTemperature = value;
                if (maximumTemperature < minimumTemperature)
                {
                    minimumTemperature = maximumTemperature;
                    minimumTemperatureSlider?.SetValueWithoutNotify(
                        minimumTemperature);
                }
                ApplyPanelValues();
            });
        top += 78f;

        CreateSliderRow(
            "Durata zilei",
            "h",
            dayLength,
            6f,
            18f,
            top,
            value =>
            {
                dayLength = value;
                ApplyPanelValues();
            });
        top += 78f;

        CreateSliderRow(
            "Radiație solară",
            "MJ/m²",
            solarRadiation,
            0f,
            35f,
            top,
            value =>
            {
                solarRadiation = value;
                ApplyPanelValues();
            });
        top += 78f;

        CreateSliderRow(
            "Apă disponibilă în sol",
            "",
            soilWater,
            0f,
            1f,
            top,
            value =>
            {
                soilWater = value;
                ApplyPanelValues();
            });
        top += 78f;

        CreateSliderRow(
            "Secunde / zi simulată",
            "s",
            secondsPerDay,
            0.1f,
            3f,
            top,
            value =>
            {
                secondsPerDay = value;
                ApplyPanelValues();
            });
        top += 78f;

        CreateSliderRow(
            "Multiplicator viteză",
            "x",
            calendarSpeedMultiplier,
            0.25f,
            4f,
            top,
            value =>
            {
                calendarSpeedMultiplier = value;
                ApplyPanelValues();
            });

        speedText = CreateText(
            "Viteză efectivă",
            worldPanelObject.transform,
            "",
            21f,
            FontStyles.Bold,
            SectionColor,
            TextAlignmentOptions.Center);
        SetTopStretch(speedText.rectTransform, 666f, 36f, 26f, 26f);

        statusText = CreateText(
            "Stare simulare",
            worldPanelObject.transform,
            "",
            20f,
            FontStyles.Normal,
            TextColor,
            TextAlignmentOptions.TopLeft);
        statusText.enableWordWrapping = true;
        SetTopStretch(statusText.rectTransform, 708f, 84f, 32f, 32f);

        CreateLiteModeButton(800f);
        CreateRestartButton(862f);

        TMP_Text hint = CreateText(
            "Indicație",
            worldPanelObject.transform,
            "Selectează cu raza mâinii și ciupește · M schimbă meniul pe PC",
            17f,
            FontStyles.Normal,
            MutedTextColor,
            TextAlignmentOptions.Center);
        SetTopStretch(hint.rectTransform, 934f, 28f, 22f, 22f);

        ResolveViewCamera();
        ConfigureEventSystem();
        worldPanelObject.SetActive(false);
        TryPlacePanelAtPlant();
        ApplyPanelValues();
        RefreshStatus();
    }

    private Slider CreateSliderRow(
        string label,
        string unit,
        float initialValue,
        float minimum,
        float maximum,
        float top,
        System.Action<float> onChanged)
    {
        GameObject rowObject = CreateRectObject(
            label + " Row",
            worldPanelObject.transform);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        SetTopStretch(rowRect, top, 72f, 32f, 32f);

        TMP_Text labelText = CreateText(
            label + " Label",
            rowObject.transform,
            label,
            21f,
            FontStyles.Normal,
            TextColor,
            TextAlignmentOptions.Left);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.offsetMin = new Vector2(0f, -30f);
        labelRect.offsetMax = new Vector2(-150f, 0f);

        TMP_Text valueText = CreateText(
            label + " Value",
            rowObject.transform,
            FormatValue(initialValue, unit),
            21f,
            FontStyles.Bold,
            SectionColor,
            TextAlignmentOptions.Right);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = new Vector2(1f, 1f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 1f);
        valueRect.anchoredPosition = Vector2.zero;
        valueRect.sizeDelta = new Vector2(145f, 30f);

        GameObject sliderObject = CreateRectObject(
            label + " Slider",
            rowObject.transform);
        RectTransform sliderRect =
            sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 1f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.pivot = new Vector2(0.5f, 1f);
        sliderRect.offsetMin = new Vector2(0f, -70f);
        sliderRect.offsetMax = new Vector2(0f, -34f);

        GameObject backgroundObject = CreateRectObject(
            "Background",
            sliderObject.transform);
        RectTransform backgroundRect =
            backgroundObject.GetComponent<RectTransform>();
        SetCenteredStretch(backgroundRect, 14f, 8f);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = TrackColor;

        GameObject fillAreaObject = CreateRectObject(
            "Fill Area",
            sliderObject.transform);
        RectTransform fillAreaRect =
            fillAreaObject.GetComponent<RectTransform>();
        SetCenteredStretch(fillAreaRect, 14f, 14f);

        GameObject fillObject = CreateRectObject(
            "Fill",
            fillAreaObject.transform);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.AddComponent<Image>();
        fill.color = FillColor;

        GameObject handleAreaObject = CreateRectObject(
            "Handle Slide Area",
            sliderObject.transform);
        RectTransform handleAreaRect =
            handleAreaObject.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(18f, 0f);
        handleAreaRect.offsetMax = new Vector2(-18f, 0f);

        GameObject handleObject = CreateRectObject(
            "Handle",
            handleAreaObject.transform);
        RectTransform handleRect =
            handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(36f, 36f);
        Image handle = handleObject.AddComponent<Image>();
        handle.color = SectionColor;

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.SetValueWithoutNotify(initialValue);
        slider.onValueChanged.AddListener(value =>
        {
            valueText.text = FormatValue(value, unit);
            onChanged?.Invoke(value);
        });
        return slider;
    }

    private void CreateRestartButton(float top)
    {
        GameObject buttonObject = CreateRectObject(
            "Restart Simulation Button",
            worldPanelObject.transform);
        RectTransform buttonRect =
            buttonObject.GetComponent<RectTransform>();
        SetTopStretch(buttonRect, top, 58f, 80f, 80f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = FillColor;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.75f, 0.82f, 0.75f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            simulation?.RestartCompleteSimulation();
            RefreshStatus();
        });

        buttonText = CreateText(
            "Button Text",
            buttonObject.transform,
            "PORNEȘTE SIMULAREA",
            25f,
            FontStyles.Bold,
            Color.white,
            TextAlignmentOptions.Center);
        buttonText.rectTransform.anchorMin = Vector2.zero;
        buttonText.rectTransform.anchorMax = Vector2.one;
        buttonText.rectTransform.offsetMin = Vector2.zero;
        buttonText.rectTransform.offsetMax = Vector2.zero;
    }

    private void CreateLiteModeButton(float top)
    {
        GameObject buttonObject = CreateRectObject(
            "Lite Mode Button",
            worldPanelObject.transform);
        RectTransform buttonRect =
            buttonObject.GetComponent<RectTransform>();
        SetTopStretch(buttonRect, top, 50f, 110f, 110f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = TrackColor;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(() =>
        {
            if (simulation != null)
            {
                simulation.SetLiteMode(!simulation.LiteModeEnabled);
            }

            RefreshStatus();
        });

        liteModeButtonText = CreateText(
            "Lite Mode Text",
            buttonObject.transform,
            "",
            22f,
            FontStyles.Bold,
            SectionColor,
            TextAlignmentOptions.Center);
        liteModeButtonText.rectTransform.anchorMin = Vector2.zero;
        liteModeButtonText.rectTransform.anchorMax = Vector2.one;
        liteModeButtonText.rectTransform.offsetMin = Vector2.zero;
        liteModeButtonText.rectTransform.offsetMax = Vector2.zero;
        RefreshLiteModeText();
    }

    private void ResolveViewCamera()
    {
        if (viewCamera != null
            && viewCamera.enabled
            && viewCamera.gameObject.activeInHierarchy)
        {
            return;
        }

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        viewCamera = null;
        for (int index = 0; index < cameras.Length; index++)
        {
            Camera candidate = cameras[index];
            if (candidate.enabled
                && candidate.gameObject.activeInHierarchy
                && IsXrCamera(candidate.transform))
            {
                viewCamera = candidate;
                break;
            }
        }

        if (viewCamera == null
            && Camera.main != null
            && Camera.main.enabled
            && Camera.main.gameObject.activeInHierarchy)
        {
            viewCamera = Camera.main;
        }

        if (viewCamera == null)
        {
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

    private static bool IsXrCamera(Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            string objectName = current.name.ToLowerInvariant();
            if (objectName.Contains("xr origin")
                || objectName.Contains("xr rig"))
            {
                return true;
            }
            current = current.parent;
        }

        return false;
    }

    private void ConfigureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject(
                "XR EventSystem",
                typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
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
            inputModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
        }

        inputModule.enableXRInput = true;
#if UNITY_EDITOR
        inputModule.enableMouseInput = true;
#endif
    }

    public void ToggleSetup()
    {
        SetNewSetupActive(!newSetupActive);
    }

    public void SetNewSetupActive(bool active)
    {
        newSetupActive = active;
        ApplySetupVisibility();
    }

    // Kept for existing scene/UI references.
    public void ToggleMenu()
    {
        ToggleSetup();
    }

    // Kept for existing scene/UI references.
    public void SetMenuVisible(bool visible)
    {
        SetNewSetupActive(visible);
    }

    private void ResolveLegacyPanel()
    {
        if (legacyPanel != null)
        {
            return;
        }

        legacyPanel = GetComponent<MapleSimulationPanel>();
        if (legacyPanel == null)
        {
            legacyPanel = gameObject.AddComponent<MapleSimulationPanel>();
        }
    }

    private void ApplySetupVisibility()
    {
        ResolveLegacyPanel();

        if (worldPanelObject != null)
        {
            worldPanelObject.SetActive(
                panelPlacementInitialized && newSetupActive);
        }

        if (presentation != null)
        {
            presentation.SetPotVisible(newSetupActive);
        }

        if (legacyPanel != null)
        {
            legacyPanel.SetRuntimePanelVisible(!newSetupActive);
        }
    }

    private void TryPlacePanelAtPlant()
    {
        if (panelPlacementInitialized
            || viewCamera == null
            || worldPanelObject == null)
        {
            return;
        }

        if (presentation != null && !presentation.PlacementCompleted)
        {
            return;
        }

        Transform panelTransform = worldPanelObject.transform;
        panelTransform.SetParent(transform, false);
        panelTransform.localPosition = menuLocalOffset;
        panelTransform.localScale = Vector3.one * worldScale;

        Vector3 directionFromCamera =
            panelTransform.position - viewCamera.transform.position;
        if (directionFromCamera.sqrMagnitude > 0.001f)
        {
            Quaternion worldRotation = Quaternion.LookRotation(
                directionFromCamera.normalized,
                Vector3.up);
            panelTransform.rotation = worldRotation;
        }

        panelPlacementInitialized = true;
        ApplySetupVisibility();
    }

    private void RefreshStatus()
    {
        if (simulation == null || statusText == null)
        {
            return;
        }

        string state = simulation.DevelopmentStage >= 2f
            ? "Maturitate atinsă."
            : simulation.SimulationRunning
                ? "Dezvoltarea arțarului este în curs."
                : "Simularea este pregătită.";
        float totalBiomass = simulation.RootBiomass
            + simulation.LeafBiomass
            + simulation.WoodBiomass;
        statusText.text =
            $"{state}  Ziua {simulation.SimulatedDay} · "
            + $"DVS {simulation.DevelopmentStage:0.000} · "
            + $"stadiu {simulation.StructuralStage}/4\n"
            + $"Biomasă {totalBiomass:0.00} · "
            + $"LAI {simulation.LeafAreaIndex:0.00} · "
            + $"creștere netă {simulation.NetAssimilation:0.000}/zi";

        if (buttonText != null)
        {
            buttonText.text = simulation.SimulationRunning
                ? "REPORNEȘTE SIMULAREA"
                : "PORNEȘTE SIMULAREA";
        }

        RefreshLiteModeText();
        RefreshSpeedText();
    }

    private void RefreshLiteModeText()
    {
        if (liteModeButtonText == null || simulation == null)
        {
            return;
        }

        liteModeButtonText.text = simulation.LiteModeEnabled
            ? "MOD LITE VIZUAL: ACTIV"
            : "MOD LITE VIZUAL: OPRIT";
    }

    private void RefreshSpeedText()
    {
        if (speedText == null)
        {
            return;
        }

        float effectiveCalendarSpeed =
            calendarSpeedMultiplier
            * ReferenceSecondsPerDay
            / Mathf.Max(0.1f, secondsPerDay);
        speedText.text =
            $"Viteză calendar {effectiveCalendarSpeed:0.00}x · "
            + $"morfologie {calendarSpeedMultiplier:0.00}x";
    }

    private void NormalizeTemperatureRange()
    {
        minimumTemperature = Mathf.Clamp(
            minimumTemperature,
            MinimumTemperature,
            MaximumTemperature);
        maximumTemperature = Mathf.Clamp(
            maximumTemperature,
            MinimumTemperature,
            MaximumTemperature);
        if (minimumTemperature > maximumTemperature)
        {
            maximumTemperature = minimumTemperature;
        }
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        string content,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateRectObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static GameObject CreateRectObject(
        string objectName,
        Transform parent)
    {
        GameObject gameObject = new GameObject(
            objectName,
            typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void SetTopStretch(
        RectTransform rect,
        float top,
        float height,
        float left,
        float right)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetCenteredStretch(
        RectTransform rect,
        float horizontalInset,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(horizontalInset, -height * 0.5f);
        rect.offsetMax = new Vector2(-horizontalInset, height * 0.5f);
    }

    private static string FormatValue(float value, string unit)
    {
        return string.IsNullOrWhiteSpace(unit)
            ? value.ToString("0.0")
            : $"{value:0.0} {unit}";
    }
}
