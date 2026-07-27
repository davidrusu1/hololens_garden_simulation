using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ICI.PlantGrowth.Phenology
{
    /// <summary>
    /// Builds the default white world-space simulation menu and switches between
    /// it and the original IMGUI menu with M.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SunflowerSimulationPanel : MonoBehaviour
    {
        private const float MinimumTemperature = -10f;
        private const float MaximumTemperature = 45f;
        private const float PanelWidth = 840f;
        private const float PanelHeight = 1140f;

        [SerializeField]
        private bool newSetupVisibleByDefault = true;

        [SerializeField]
        private Vector3 menuLocalOffset = new Vector3(-1.15f, 1.35f, -0.05f);

        [SerializeField, Min(0.0001f)]
        private float worldScale = 0.00075f;

        private static readonly Color PanelColor =
            new Color(0.985f, 0.99f, 0.98f, 0.985f);
        private static readonly Color GreenColor =
            new Color(0.075f, 0.39f, 0.16f, 1f);
        private static readonly Color DarkTextColor =
            new Color(0.07f, 0.11f, 0.075f, 1f);
        private static readonly Color MutedTextColor =
            new Color(0.31f, 0.38f, 0.32f, 1f);
        private static readonly Color TrackColor =
            new Color(0.81f, 0.87f, 0.82f, 1f);
        private static readonly Color FillColor =
            new Color(0.12f, 0.55f, 0.23f, 1f);

        private PlantPhenologySimulation simulation;
        private SunflowerPresentationSetup presentation;
        private GameObject worldPanelObject;
        private Canvas worldCanvas;
        private Camera viewCamera;
        private Font runtimeFont;
        private Text statusText;
        private Text speedText;
        private Text startButtonText;
        private Slider minimumTemperatureSlider;
        private Slider maximumTemperatureSlider;
        private bool valuesInitialized;
        private bool newSetupActive;
        private bool panelPlacementInitialized;

        private int sunflowerCount;
        private int cassavaCount;
        private float minimumTemperature;
        private float maximumTemperature;
        private float dayLength;
        private float solarRadiation;
        private float soilWater;
        private float secondsPerDay;
        private float visualGrowthSpeed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForPhenologyScene()
        {
            PlantPhenologySimulation target =
                FindObjectOfType<PlantPhenologySimulation>(true);
            if (target == null)
            {
                return;
            }

            if (target.GetComponent<SunflowerPresentationSetup>() == null)
            {
                target.gameObject.AddComponent<SunflowerPresentationSetup>();
            }
            if (target.GetComponent<SunflowerSimulationPanel>() == null)
            {
                target.gameObject.AddComponent<SunflowerSimulationPanel>();
            }
        }

        private void Awake()
        {
            simulation = GetComponent<PlantPhenologySimulation>();
            presentation = GetComponent<SunflowerPresentationSetup>();
            newSetupActive = newSetupVisibleByDefault;
            InitializeValues();
            ApplySetupVisibility();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            InitializeValues();
            BuildWorldPanel();
            ApplySetupVisibility();
        }

        private void Update()
        {
            ResolveDependencies();
            ResolveViewCamera();
            ConfigureEventSystem();
            TryPlacePanel();

            if (Input.GetKeyDown(KeyCode.M))
            {
                ToggleSetup();
            }

            ApplySetupVisibility();
            RefreshStatus();
        }

        private void OnDestroy()
        {
            if (worldPanelObject != null)
            {
                Destroy(worldPanelObject);
            }
        }

        public void ToggleSetup()
        {
            newSetupActive = !newSetupActive;
            ApplySetupVisibility();
        }

        public void SetNewSetupActive(bool active)
        {
            newSetupActive = active;
            ApplySetupVisibility();
        }

        private void ResolveDependencies()
        {
            if (simulation == null)
            {
                simulation = GetComponent<PlantPhenologySimulation>();
            }
            if (simulation == null)
            {
                simulation = FindObjectOfType<PlantPhenologySimulation>(true);
            }

            if (presentation == null && simulation != null)
            {
                presentation =
                    simulation.GetComponent<SunflowerPresentationSetup>();
            }
        }

        private void InitializeValues()
        {
            if (valuesInitialized || simulation == null)
            {
                return;
            }

            sunflowerCount = simulation.RequestedSunflowerCount;
            cassavaCount = simulation.RequestedCassavaCount;
            minimumTemperature = simulation.CurrentMinimumTemperature;
            maximumTemperature = simulation.CurrentMaximumTemperature;
            dayLength = simulation.CurrentDayLength;
            solarRadiation = simulation.CurrentSolarRadiation;
            soilWater = simulation.SoilWaterAvailability;
            secondsPerDay = simulation.SecondsPerSimulatedDay;
            visualGrowthSpeed = simulation.VisualGrowthSpeed;
            valuesInitialized = true;
        }

        private void ApplyEnvironmentAndSpeed()
        {
            if (simulation == null)
            {
                return;
            }

            NormalizeTemperatureRange();
            simulation.SetEnvironmentParameters(
                minimumTemperature,
                maximumTemperature,
                dayLength,
                solarRadiation,
                soilWater);
            simulation.SetPlaybackParameters(
                secondsPerDay,
                visualGrowthSpeed);
            RefreshSpeedText();
        }

        private void ApplyPlantCounts()
        {
            simulation?.SetFieldConfiguration(sunflowerCount, cassavaCount);
        }

        private void BuildWorldPanel()
        {
            if (worldPanelObject != null)
            {
                return;
            }

            runtimeFont = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            worldPanelObject = new GameObject(
                "Sunflower Simulation Menu",
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

            Image background = worldPanelObject.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = true;

            Text title = CreateText(
                "Title",
                worldPanelObject.transform,
                "SIMULARE FENOLOGICĂ",
                38,
                FontStyle.Bold,
                GreenColor,
                TextAnchor.MiddleCenter);
            SetTopStretch(title.rectTransform, 20f, 50f, 28f, 28f);

            Text subtitle = CreateText(
                "Subtitle",
                worldPanelObject.transform,
                "Floarea-soarelui + cassava · parametri în timp real",
                22,
                FontStyle.Normal,
                MutedTextColor,
                TextAnchor.MiddleCenter);
            SetTopStretch(subtitle.rectTransform, 70f, 36f, 28f, 28f);

            CreateSectionLabel("CONFIGURAȚIE CULTURI", 112f);
            CreateSliderRow(
                "Flori-soarelui",
                "",
                sunflowerCount,
                0f,
                10f,
                140f,
                true,
                value =>
                {
                    sunflowerCount = Mathf.RoundToInt(value);
                    ApplyPlantCounts();
                });
            CreateSliderRow(
                "Plante cassava",
                "",
                cassavaCount,
                0f,
                10f,
                210f,
                true,
                value =>
                {
                    cassavaCount = Mathf.RoundToInt(value);
                    ApplyPlantCounts();
                });

            CreateSectionLabel("MEDIU ȘI VITEZĂ", 282f);
            minimumTemperatureSlider = CreateSliderRow(
                "Temperatură minimă",
                "°C",
                minimumTemperature,
                MinimumTemperature,
                MaximumTemperature,
                312f,
                false,
                value =>
                {
                    minimumTemperature = value;
                    if (minimumTemperature > maximumTemperature)
                    {
                        maximumTemperature = minimumTemperature;
                        maximumTemperatureSlider?.SetValueWithoutNotify(
                            maximumTemperature);
                    }
                    ApplyEnvironmentAndSpeed();
                });
            maximumTemperatureSlider = CreateSliderRow(
                "Temperatură maximă",
                "°C",
                maximumTemperature,
                MinimumTemperature,
                MaximumTemperature,
                382f,
                false,
                value =>
                {
                    maximumTemperature = value;
                    if (maximumTemperature < minimumTemperature)
                    {
                        minimumTemperature = maximumTemperature;
                        minimumTemperatureSlider?.SetValueWithoutNotify(
                            minimumTemperature);
                    }
                    ApplyEnvironmentAndSpeed();
                });
            CreateSliderRow(
                "Durata zilei",
                "h",
                dayLength,
                0f,
                24f,
                452f,
                false,
                value =>
                {
                    dayLength = value;
                    ApplyEnvironmentAndSpeed();
                });
            CreateSliderRow(
                "Radiație solară",
                "MJ/m²",
                solarRadiation,
                0f,
                35f,
                522f,
                false,
                value =>
                {
                    solarRadiation = value;
                    ApplyEnvironmentAndSpeed();
                });
            CreateSliderRow(
                "Apă disponibilă în sol",
                "",
                soilWater,
                0f,
                1f,
                592f,
                false,
                value =>
                {
                    soilWater = value;
                    ApplyEnvironmentAndSpeed();
                });
            CreateSliderRow(
                "Secunde / zi simulată",
                "s",
                secondsPerDay,
                0.1f,
                3f,
                662f,
                false,
                value =>
                {
                    secondsPerDay = value;
                    ApplyEnvironmentAndSpeed();
                });
            CreateSliderRow(
                "Multiplicator viteză",
                "x",
                visualGrowthSpeed,
                0.25f,
                4f,
                732f,
                false,
                value =>
                {
                    visualGrowthSpeed = value;
                    ApplyEnvironmentAndSpeed();
                });

            speedText = CreateText(
                "Effective Speed",
                worldPanelObject.transform,
                "",
                20,
                FontStyle.Bold,
                GreenColor,
                TextAnchor.MiddleCenter);
            SetTopStretch(speedText.rectTransform, 808f, 34f, 30f, 30f);

            statusText = CreateText(
                "Simulation Status",
                worldPanelObject.transform,
                "",
                19,
                FontStyle.Normal,
                DarkTextColor,
                TextAnchor.UpperLeft);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            SetTopStretch(statusText.rectTransform, 850f, 104f, 34f, 34f);

            CreateStartButton(970f);

            Text hint = CreateText(
                "Hint",
                worldPanelObject.transform,
                "M: meniu nou/vechi · Esc: eliberează cursorul · "
                + "WASD: orizontal · ↑/↓: vertical",
                17,
                FontStyle.Normal,
                MutedTextColor,
                TextAnchor.MiddleCenter);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetTopStretch(hint.rectTransform, 1046f, 70f, 30f, 30f);

            ResolveViewCamera();
            ConfigureEventSystem();
            worldPanelObject.SetActive(false);
            TryPlacePanel();
            ApplyEnvironmentAndSpeed();
            RefreshStatus();
        }

        private void CreateSectionLabel(string label, float top)
        {
            Text section = CreateText(
                label,
                worldPanelObject.transform,
                label,
                18,
                FontStyle.Bold,
                GreenColor,
                TextAnchor.MiddleLeft);
            SetTopStretch(section.rectTransform, top, 26f, 34f, 34f);
        }

        private Slider CreateSliderRow(
            string label,
            string unit,
            float initialValue,
            float minimum,
            float maximum,
            float top,
            bool wholeNumbers,
            System.Action<float> onChanged)
        {
            GameObject rowObject = CreateRectObject(
                label + " Row",
                worldPanelObject.transform);
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            SetTopStretch(rowRect, top, 66f, 34f, 34f);

            Text labelText = CreateText(
                label + " Label",
                rowObject.transform,
                label,
                19,
                FontStyle.Normal,
                DarkTextColor,
                TextAnchor.MiddleLeft);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.offsetMin = new Vector2(0f, -28f);
            labelRect.offsetMax = new Vector2(-160f, 0f);

            Text valueText = CreateText(
                label + " Value",
                rowObject.transform,
                FormatValue(initialValue, unit, wholeNumbers),
                19,
                FontStyle.Bold,
                GreenColor,
                TextAnchor.MiddleRight);
            RectTransform valueRect = valueText.rectTransform;
            valueRect.anchorMin = new Vector2(1f, 1f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.pivot = new Vector2(1f, 1f);
            valueRect.anchoredPosition = Vector2.zero;
            valueRect.sizeDelta = new Vector2(155f, 28f);

            GameObject sliderObject = CreateRectObject(
                label + " Slider",
                rowObject.transform);
            RectTransform sliderRect =
                sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 1f);
            sliderRect.anchorMax = new Vector2(1f, 1f);
            sliderRect.pivot = new Vector2(0.5f, 1f);
            sliderRect.offsetMin = new Vector2(0f, -64f);
            sliderRect.offsetMax = new Vector2(0f, -32f);

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
            StretchToParent(fillRect);
            Image fill = fillObject.AddComponent<Image>();
            fill.color = FillColor;

            GameObject handleAreaObject = CreateRectObject(
                "Handle Slide Area",
                sliderObject.transform);
            RectTransform handleAreaRect =
                handleAreaObject.GetComponent<RectTransform>();
            StretchToParent(handleAreaRect);
            handleAreaRect.offsetMin = new Vector2(17f, 0f);
            handleAreaRect.offsetMax = new Vector2(-17f, 0f);

            GameObject handleObject = CreateRectObject(
                "Handle",
                handleAreaObject.transform);
            RectTransform handleRect =
                handleObject.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(34f, 34f);
            Image handle = handleObject.AddComponent<Image>();
            handle.color = GreenColor;

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = wholeNumbers;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.SetValueWithoutNotify(initialValue);
            slider.onValueChanged.AddListener(value =>
            {
                valueText.text = FormatValue(value, unit, wholeNumbers);
                onChanged?.Invoke(value);
            });
            return slider;
        }

        private void CreateStartButton(float top)
        {
            GameObject buttonObject = CreateRectObject(
                "Start Simulation Button",
                worldPanelObject.transform);
            RectTransform buttonRect =
                buttonObject.GetComponent<RectTransform>();
            SetTopStretch(buttonRect, top, 62f, 96f, 96f);

            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = FillColor;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() =>
            {
                simulation?.StartSimulation();
                RefreshStatus();
            });

            startButtonText = CreateText(
                "Button Text",
                buttonObject.transform,
                "PORNEȘTE SIMULAREA",
                24,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            StretchToParent(startButtonText.rectTransform);
        }

        private void ApplySetupVisibility()
        {
            simulation?.SetRuntimePanelVisible(!newSetupActive);
            presentation?.SetNewSetupActive(newSetupActive);

            if (worldPanelObject != null)
            {
                worldPanelObject.SetActive(
                    panelPlacementInitialized && newSetupActive);
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

            viewCamera = Camera.main;
            if (viewCamera == null)
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

        private void TryPlacePanel()
        {
            if (panelPlacementInitialized
                || worldPanelObject == null
                || viewCamera == null)
            {
                return;
            }

            Transform panelTransform = worldPanelObject.transform;
            Transform stableRoot = transform.root;
            panelTransform.SetParent(stableRoot, false);
            panelTransform.localPosition = menuLocalOffset;
            panelTransform.localScale = Vector3.one * worldScale;

            Vector3 directionFromCamera =
                panelTransform.position - viewCamera.transform.position;
            if (directionFromCamera.sqrMagnitude > 0.001f)
            {
                panelTransform.rotation = Quaternion.LookRotation(
                    directionFromCamera.normalized,
                    Vector3.up);
            }

            panelPlacementInitialized = true;
            ApplySetupVisibility();
        }

        private void ConfigureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject(
                    "Runtime EventSystem",
                    typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
        }

        private void RefreshStatus()
        {
            if (simulation == null || statusText == null)
            {
                return;
            }

            statusText.text =
                $"{simulation.StatusMessage}\n"
                + $"Ziua {simulation.CurrentDay} · "
                + $"DVS {simulation.CurrentDevelopmentStage:0.000} · "
                + $"TSUM {simulation.CurrentTemperatureSum:0.0}\n"
                + $"Temperatură medie {simulation.CurrentTemperature:0.0} °C · "
                + $"Biomasă {simulation.TotalDryMatterPerPlantGrams:0.0} g SU/plantă";

            if (startButtonText != null)
            {
                startButtonText.text = simulation.IsRunning
                    ? "REPORNEȘTE SIMULAREA"
                    : "PORNEȘTE SIMULAREA";
            }
            RefreshSpeedText();
        }

        private void RefreshSpeedText()
        {
            if (simulation != null && speedText != null)
            {
                speedText.text =
                    $"Viteză calendar: {simulation.EffectiveSimulationSpeed:0.00}x"
                    + $"   Morfologie: {simulation.EffectiveVisualGrowthSpeed:0.00}x";
            }
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

        private Text CreateText(
            string objectName,
            Transform parent,
            string content,
            int fontSize,
            FontStyle fontStyle,
            Color color,
            TextAnchor alignment)
        {
            GameObject textObject = CreateRectObject(objectName, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = runtimeFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateRectObject(
            string objectName,
            Transform parent)
        {
            GameObject result = new GameObject(
                objectName,
                typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static void SetTopStretch(
            RectTransform rectTransform,
            float top,
            float height,
            float left,
            float right)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(left, -top - height);
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        private static void SetCenteredStretch(
            RectTransform rectTransform,
            float horizontalMargin,
            float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(1f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin =
                new Vector2(horizontalMargin, -height * 0.5f);
            rectTransform.offsetMax =
                new Vector2(-horizontalMargin, height * 0.5f);
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static string FormatValue(
            float value,
            string unit,
            bool wholeNumbers)
        {
            string formatted = wholeNumbers
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.0");
            return string.IsNullOrEmpty(unit)
                ? formatted
                : $"{formatted} {unit}";
        }
    }
}
