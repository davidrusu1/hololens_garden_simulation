using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ICI.PlantGrowth
{
    /// <summary>
    /// Grows a stem through four prefab-based stages.
    /// Stages 1-3 grow three times before being replaced by the next stage.
    /// Stage 4 is terminal for the current stem and never grows.
    /// A new stem can then start above the completed stage-4 stem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StemGrowthController : MonoBehaviour
    {
        private const int TerminalStage = 4;
        private const float VegetativeDvsStep = 1f / 18f;

        [Header("AR anchor")]
        [SerializeField]
        [Tooltip("Shown immediately when the scene starts. The stem appears after the initial delay.")]
        private GameObject arucoAnchorPrefab;

        [SerializeField, Min(0f)]
        private float secondsBeforeStemAppears = 3f;

        [Header("Backdrop")]
        [SerializeField]
        [Tooltip("Creates a neutral panel behind the plant so the stem is easier to see.")]
        private bool showBackdrop = true;

        [SerializeField, Min(0.05f)]
        [Tooltip("Distance behind the plant on the local Z axis.")]
        private float backdropDistance = 0.6f;

        [SerializeField]
        private Vector2 backdropSize = new Vector2(2.4f, 2.5f);

        [SerializeField]
        [Tooltip("Vertical position of the center of the backdrop.")]
        private float backdropCenterHeight = 1.05f;

        [SerializeField]
        private Color backdropColor = new Color(0.68f, 0.7f, 0.66f, 1f);

        [Header("Stem models (stage 1 to stage 4)")]
        [SerializeField]
        [Tooltip("Place the four stem prefabs here in order. A missing next stage stops the growth safely.")]
        private GameObject[] stagePrefabs = new GameObject[TerminalStage];

        [SerializeField]
        [Tooltip("Optional parent for the instantiated stem. If it is empty, this GameObject is used.")]
        private Transform modelContainer;

        [Header("Stem appearance")]
        [SerializeField]
        [Tooltip("One solid color shared by every stem stage and by the flower peduncle.")]
        private Color unifiedStemColor = new Color(0.08f, 0.32f, 0.09f, 1f);

        [Header("Initial state")]
        [SerializeField, Range(1, TerminalStage)]
        private int startingStage = 1;

        [Header("Growth settings")]
        [SerializeField, Min(0.01f)]
        private float secondsBetweenGrowthSteps = 3f;

        [SerializeField, Min(1f)]
        [Tooltip("Length multiplier applied on every growth step.")]
        private float scaleMultiplier = 1.2f;

        [SerializeField, Min(1f)]
        [Tooltip("Thickness multiplier at the base. This should be larger than the tip multiplier.")]
        private float baseThicknessGrowthMultiplier = 1.12f;

        [SerializeField, Min(1f)]
        [Tooltip("Thickness multiplier near the top of the stem.")]
        private float tipThicknessGrowthMultiplier = 1.04f;

        [SerializeField]
        [Tooltip("Hides decorative spherical tips included in the stem prefabs.")]
        private bool hideRoundedStemTips = true;

        [SerializeField, Min(0.1f)]
        [Tooltip("Maximum total height of all stacked stem segments, measured from the anchor.")]
        private float maximumTotalStemLength = 3f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Maximum diameter allowed anywhere along the stem.")]
        private float maximumStemThickness = 0.14f;

        [SerializeField, Min(1)]
        private int growthStepsBeforeNextStage = 3;

        [SerializeField]
        [Tooltip("After stage 4, keep it in place and start a new stem above it.")]
        private bool stackNewStemAfterStageFour = true;

        [SerializeField, Min(0f)]
        private float secondsBeforeNextStackedStem = 3f;

        [SerializeField, Min(0f)]
        [Tooltip("Small overlap between completed stems so there is no visible gap.")]
        private float stackedStemOverlap = 0.015f;

        [SerializeField]
        private bool startAutomatically = true;

        [SerializeField, Min(0.1f)]
        [Tooltip("Runtime speed applied to stem, leaf and flower animation delays.")]
        private float simulationSpeedMultiplier = 1f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Keeps flower expansion active from DVS 1 until close to DVS 2 instead of completing immediately after flowering starts.")]
        private float flowerGrowthSpeedRatio = 0.3f;

        [Header("Leaves")]
        [SerializeField]
        private LeafGrowthController leafGrowthController;

        [Header("Flower")]
        [SerializeField]
        private FlowerGrowthController flowerGrowthController;

        private GameObject activeStem;
        private GameObject activeArucoAnchor;
        private GameObject activeBackdrop;
        private Material backdropMaterial;
        private Coroutine growthRoutine;
        private readonly List<GameObject> completedStems = new List<GameObject>();
        private int growthStep;
        private bool stemHasAppeared;
        private float currentStemBaseHeight;
        private float requiredBaseThicknessForNextStem;
        private float currentSegmentBaseThicknessLimit;
        private Material[] consistentStemMaterials;
        private float initialPlantHeight;
        private float initialPlantThickness;
        private bool floweringAuthorized;
        private bool maturityRequested;
        private bool plantDimensionsReady;
        private float flowerSupportHeight;
        private float flowerSupportThickness;
        private bool developmentStageDriven;
        private float currentDevelopmentStage = -0.1f;
        private float nextVegetativeDvsThreshold;
        private bool leavesSpawnedForActiveStem;

        public int CurrentStage { get; private set; }
        public GameObject ActiveStem => activeStem;
        public GameObject ActiveArucoAnchor => activeArucoAnchor;
        public bool IsGrowing => developmentStageDriven
            ? currentDevelopmentStage >= 0f && currentDevelopmentStage < 1f
            : growthRoutine != null;
        public float CurrentDevelopmentStage => currentDevelopmentStage;
        public bool HasReachedMatureStemDimensions => plantDimensionsReady;
        public bool HasFlowerStarted => flowerGrowthController != null
            && flowerGrowthController.HasStarted;
        public bool IsFlowerComplete => flowerGrowthController != null
            && flowerGrowthController.IsComplete;

        public void SetAutomaticStart(bool enabled)
        {
            startAutomatically = enabled;
        }

        public void SetSimulationSpeed(float speedMultiplier)
        {
            simulationSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
            leafGrowthController?.SetSimulationSpeed(simulationSpeedMultiplier);
            flowerGrowthController?.SetSimulationSpeed(
                simulationSpeedMultiplier * flowerGrowthSpeedRatio);
        }

        /// <summary>
        /// Makes DVS the visual clock. DVS below zero is pre-emergence,
        /// DVS 0..1 drives stem and leaves, and DVS 1..2 drives the flower.
        /// Reapplying the same DVS never advances the plant.
        /// </summary>
        public void SetDevelopmentStage(float developmentStage)
        {
            developmentStageDriven = true;
            float clampedDevelopmentStage = Mathf.Clamp(
                developmentStage,
                -0.1f,
                2f);

            if (clampedDevelopmentStage < 0f)
            {
                bool needsReset = currentDevelopmentStage >= 0f
                    || activeStem != null
                    || completedStems.Count > 0
                    || plantDimensionsReady;
                currentDevelopmentStage = clampedDevelopmentStage;
                nextVegetativeDvsThreshold = 0f;
                if (needsReset)
                {
                    SetStage(1);
                }

                floweringAuthorized = false;
                maturityRequested = false;
                leafGrowthController?.SetDevelopmentStage(
                    currentDevelopmentStage);
                flowerGrowthController?.SetDevelopmentStage(
                    currentDevelopmentStage,
                    0f,
                    0f);
                return;
            }

            currentDevelopmentStage = clampedDevelopmentStage;
            leafGrowthController?.SetDevelopmentStage(currentDevelopmentStage);

            if (currentDevelopmentStage < 1f)
            {
                floweringAuthorized = false;
                maturityRequested = false;
                StopGrowth();
                AdvanceStemToCurrentDevelopmentStage();
                return;
            }

            floweringAuthorized = true;
            maturityRequested = currentDevelopmentStage >= 2f;
            CompleteStemImmediately();
            SynchronizeFlowerWithPhenology();
        }

        /// <summary>
        /// Called by the numerical phenology simulation at DVS 1.
        /// The flower starts only after both phenology and its physical stem
        /// support are ready.
        /// </summary>
        public void NotifyFloweringStarted()
        {
            if (developmentStageDriven)
            {
                SetDevelopmentStage(Mathf.Max(1f, currentDevelopmentStage));
                return;
            }

            floweringAuthorized = true;
            SynchronizeFlowerWithPhenology();
        }

        /// <summary>
        /// Called at DVS 2. Existing leaf animations and the flower are moved
        /// to their mature state without changing the numerical calendar.
        /// </summary>
        public void CompleteVisualMaturity()
        {
            if (developmentStageDriven)
            {
                SetDevelopmentStage(2f);
                return;
            }

            floweringAuthorized = true;
            maturityRequested = true;
            leafGrowthController?.CompleteGrowthImmediately();
            SynchronizeFlowerWithPhenology();
        }

        private void Awake()
        {
            CurrentStage = Mathf.Clamp(startingStage, 1, TerminalStage);
            ShowBackdrop();
            ShowArucoAnchor();

            if (leafGrowthController == null)
            {
                leafGrowthController = GetComponent<LeafGrowthController>();
            }

            if (leafGrowthController != null)
            {
                Transform leafParent = modelContainer != null ? modelContainer : transform;
                leafGrowthController.Initialize(leafParent);
                leafGrowthController.SetSimulationSpeed(simulationSpeedMultiplier);
            }

            if (flowerGrowthController == null)
            {
                flowerGrowthController = GetComponent<FlowerGrowthController>();
            }

            if (flowerGrowthController != null)
            {
                Transform flowerParent = modelContainer != null ? modelContainer : transform;
                flowerGrowthController.Initialize(flowerParent);
                flowerGrowthController.SetSimulationSpeed(
                    simulationSpeedMultiplier * flowerGrowthSpeedRatio);
            }
        }

        private void Start()
        {
            // Preserve the controller's standalone behaviour in scenes that do
            // not use the numerical phenology component.
            if (GetComponent<Phenology.PlantPhenologySimulation>() == null)
            {
                floweringAuthorized = true;
            }

            if (startAutomatically)
            {
                StartGrowth();
            }
        }

        private void OnDisable()
        {
            StopGrowth();
        }

        private void OnDestroy()
        {
            if (backdropMaterial != null)
            {
                Destroy(backdropMaterial);
            }

            if (consistentStemMaterials != null)
            {
                foreach (Material stemMaterial in consistentStemMaterials)
                {
                    if (stemMaterial != null)
                    {
                        Destroy(stemMaterial);
                    }
                }
            }
        }

        /// <summary>Starts or resumes growth of the current stem.</summary>
        public void StartGrowth()
        {
            if (!isActiveAndEnabled || growthRoutine != null)
            {
                return;
            }

            if (!HasPrefabForStage(CurrentStage))
            {
                Debug.LogWarning($"[{nameof(StemGrowthController)}] Stage {CurrentStage} has no stem prefab.", this);
                return;
            }

            growthRoutine = StartCoroutine(GrowStem());
        }

        public void StopGrowth()
        {
            if (growthRoutine == null)
            {
                return;
            }

            StopCoroutine(growthRoutine);
            growthRoutine = null;
        }

        /// <summary>Switches immediately to a stage and restarts its growth cycle.</summary>
        public void SetStage(int stage)
        {
            StopGrowth();
            CurrentStage = Mathf.Clamp(stage, 1, TerminalStage);
            growthStep = 0;
            stemHasAppeared = false;
            currentStemBaseHeight = 0f;
            currentSegmentBaseThicknessLimit = 0f;
            initialPlantHeight = 0f;
            initialPlantThickness = 0f;
            floweringAuthorized = GetComponent<Phenology.PlantPhenologySimulation>() == null;
            maturityRequested = false;
            plantDimensionsReady = false;
            flowerSupportHeight = 0f;
            flowerSupportThickness = 0f;
            nextVegetativeDvsThreshold = 0f;
            leavesSpawnedForActiveStem = false;
            ClearAllStems();
            leafGrowthController?.ResetLeaves();
            flowerGrowthController?.ResetFlower();

            if (startAutomatically && isActiveAndEnabled)
            {
                StartGrowth();
            }
        }

        private IEnumerator GrowStem()
        {
            if (!stemHasAppeared)
            {
                yield return WaitForSimulationSeconds(secondsBeforeStemAppears);
                ShowStage(CurrentStage);
                stemHasAppeared = true;
            }

            while (true)
            {
                while (CurrentStage < TerminalStage)
                {
                    yield return WaitForSimulationSeconds(secondsBetweenGrowthSteps);

                    if (activeStem == null)
                    {
                        growthRoutine = null;
                        yield break;
                    }

                    GrowActiveStemBiologically();
                    SynchronizePlantThicknessWithHeight();
                    growthStep++;

                    if (growthStep < growthStepsBeforeNextStage)
                    {
                        continue;
                    }

                    int nextStage = CurrentStage + 1;
                    if (!HasPrefabForStage(nextStage))
                    {
                        Debug.LogWarning(
                            $"[{nameof(StemGrowthController)}] Stem stage {nextStage} is missing. Growth stopped at stage {CurrentStage}.",
                            this);
                        growthRoutine = null;
                        yield break;
                    }

                    CurrentStage = nextStage;
                    growthStep = 0;
                    ShowStage(CurrentStage);
                }

                // Stage 4 no longer changes length or stage. It becomes the base for
                // the next segment, or joins the final uniform-thickness maturation.
                StemDimensions leafStemDimensions = MeasureStem(activeStem);
                leafGrowthController?.SetStemMaterials(consistentStemMaterials);
                leafGrowthController?.SpawnLeavesForCompletedStem(
                    currentStemBaseHeight,
                    leafStemDimensions.topHeight,
                    leafStemDimensions.baseThickness * 0.5f);

                if (HasReachedMaximumPlantHeight(out float flowerHeight))
                {
                    yield return GrowMaturePlantToMaximumThickness();

                    if (HasReachedMaximumPlantDimensions(out flowerHeight))
                    {
                        flowerSupportThickness = MeasureStemTopInterfaceThickness(activeStem);
                        if (flowerSupportThickness <= 0f)
                        {
                            flowerSupportThickness = maximumStemThickness;
                        }

                        flowerSupportHeight = flowerHeight;
                        plantDimensionsReady = true;
                        SynchronizeFlowerWithPhenology();
                    }

                    break;
                }

                if (!stackNewStemAfterStageFour || activeStem == null)
                {
                    break;
                }

                yield return WaitForSimulationSeconds(secondsBeforeNextStackedStem);

                StemDimensions completedDimensions = MeasureStem(activeStem);
                if (completedDimensions.topHeight >= maximumTotalStemLength - 0.001f)
                {
                    break;
                }

                currentStemBaseHeight = completedDimensions.topHeight - stackedStemOverlap;
                requiredBaseThicknessForNextStem = Mathf.Min(
                    completedDimensions.topThickness,
                    maximumStemThickness);
                currentSegmentBaseThicknessLimit = requiredBaseThicknessForNextStem;
                completedStems.Add(activeStem);
                activeStem = null;
                CurrentStage = 1;
                growthStep = 0;
                ShowStage(CurrentStage);
            }

            growthRoutine = null;
        }

        private void AdvanceStemToCurrentDevelopmentStage()
        {
            const int safetyLimit = 64;
            int safety = 0;
            while (nextVegetativeDvsThreshold
                    <= currentDevelopmentStage + 0.0001f
                && currentDevelopmentStage < 1f
                && safety++ < safetyLimit)
            {
                AdvanceOneVegetativeDvsEvent();
                nextVegetativeDvsThreshold += VegetativeDvsStep;
            }

            leafGrowthController?.SetDevelopmentStage(currentDevelopmentStage);
        }

        private void AdvanceOneVegetativeDvsEvent()
        {
            if (!stemHasAppeared)
            {
                ShowStage(CurrentStage);
                stemHasAppeared = activeStem != null;
                return;
            }

            if (activeStem == null)
            {
                return;
            }

            if (CurrentStage < TerminalStage)
            {
                GrowActiveStemBiologically();
                SynchronizePlantThicknessWithHeight();
                growthStep++;

                if (growthStep < growthStepsBeforeNextStage)
                {
                    return;
                }

                int nextStage = CurrentStage + 1;
                if (!HasPrefabForStage(nextStage))
                {
                    return;
                }

                CurrentStage = nextStage;
                growthStep = 0;
                ShowStage(CurrentStage);
                return;
            }

            if (!leavesSpawnedForActiveStem)
            {
                StemDimensions leafStemDimensions = MeasureStem(activeStem);
                leafGrowthController?.SetStemMaterials(consistentStemMaterials);
                leafGrowthController?.SpawnLeavesForCompletedStem(
                    currentStemBaseHeight,
                    leafStemDimensions.topHeight,
                    leafStemDimensions.baseThickness * 0.5f);
                leavesSpawnedForActiveStem = true;
            }

            if (HasReachedMaximumPlantHeight(out _))
            {
                GetPlantMaximumDimensions(out _, out float greatestThickness);
                if (greatestThickness < maximumStemThickness - 0.001f)
                {
                    float thicknessFactor = Mathf.Min(
                        baseThicknessGrowthMultiplier,
                        maximumStemThickness
                            / Mathf.Max(greatestThickness, 0.0001f));
                    ScaleAllStemThickness(thicknessFactor);
                }

                return;
            }

            if (!stackNewStemAfterStageFour)
            {
                return;
            }

            StemDimensions completedDimensions = MeasureStem(activeStem);
            currentStemBaseHeight =
                completedDimensions.topHeight - stackedStemOverlap;
            requiredBaseThicknessForNextStem = Mathf.Min(
                completedDimensions.topThickness,
                maximumStemThickness);
            currentSegmentBaseThicknessLimit =
                requiredBaseThicknessForNextStem;
            completedStems.Add(activeStem);
            activeStem = null;
            CurrentStage = 1;
            growthStep = 0;
            leavesSpawnedForActiveStem = false;
            ShowStage(CurrentStage);
        }

        private void CompleteStemImmediately()
        {
            if (plantDimensionsReady)
            {
                return;
            }

            StopGrowth();
            if (!HasPrefabForStage(CurrentStage))
            {
                return;
            }

            if (!stemHasAppeared)
            {
                ShowStage(CurrentStage);
                stemHasAppeared = activeStem != null;
            }

            const int safetyLimit = 96;
            int safety = 0;
            while (!plantDimensionsReady && safety++ < safetyLimit)
            {
                while (CurrentStage < TerminalStage && safety++ < safetyLimit)
                {
                    while (growthStep < growthStepsBeforeNextStage
                        && safety++ < safetyLimit)
                    {
                        GrowActiveStemBiologically();
                        SynchronizePlantThicknessWithHeight();
                        growthStep++;
                    }

                    int nextStage = CurrentStage + 1;
                    if (!HasPrefabForStage(nextStage))
                    {
                        break;
                    }

                    CurrentStage = nextStage;
                    growthStep = 0;
                    ShowStage(CurrentStage);
                }

                if (!leavesSpawnedForActiveStem)
                {
                    StemDimensions leafStemDimensions = MeasureStem(activeStem);
                    leafGrowthController?.SetStemMaterials(consistentStemMaterials);
                    leafGrowthController?.SpawnLeavesForCompletedStem(
                        currentStemBaseHeight,
                        leafStemDimensions.topHeight,
                        leafStemDimensions.baseThickness * 0.5f);
                    leavesSpawnedForActiveStem = true;
                }

                if (HasReachedMaximumPlantHeight(out float flowerHeight))
                {
                    GetPlantMaximumDimensions(out _, out float greatestThickness);
                    while (greatestThickness < maximumStemThickness - 0.001f
                        && safety++ < safetyLimit)
                    {
                        float thicknessFactor = Mathf.Min(
                            baseThicknessGrowthMultiplier,
                            maximumStemThickness
                                / Mathf.Max(greatestThickness, 0.0001f));
                        ScaleAllStemThickness(thicknessFactor);
                        GetPlantMaximumDimensions(
                            out _,
                            out greatestThickness);
                    }

                    GetPlantMaximumDimensions(
                        out flowerHeight,
                        out greatestThickness);
                    flowerSupportThickness =
                        MeasureStemTopInterfaceThickness(activeStem);
                    if (flowerSupportThickness <= 0f)
                    {
                        flowerSupportThickness = Mathf.Max(
                            greatestThickness,
                            maximumStemThickness);
                    }

                    flowerSupportHeight = flowerHeight;
                    plantDimensionsReady = true;
                    break;
                }

                if (!stackNewStemAfterStageFour || activeStem == null)
                {
                    break;
                }

                StemDimensions completedDimensions = MeasureStem(activeStem);
                if (completedDimensions.topHeight
                    >= maximumTotalStemLength - 0.001f)
                {
                    break;
                }

                currentStemBaseHeight =
                    completedDimensions.topHeight - stackedStemOverlap;
                requiredBaseThicknessForNextStem = Mathf.Min(
                    completedDimensions.topThickness,
                    maximumStemThickness);
                currentSegmentBaseThicknessLimit =
                    requiredBaseThicknessForNextStem;
                completedStems.Add(activeStem);
                activeStem = null;
                CurrentStage = 1;
                growthStep = 0;
                leavesSpawnedForActiveStem = false;
                ShowStage(CurrentStage);
            }

            leafGrowthController?.SetDevelopmentStage(currentDevelopmentStage);
        }

        private void SynchronizeFlowerWithPhenology()
        {
            if (!floweringAuthorized
                || !plantDimensionsReady
                || flowerGrowthController == null)
            {
                return;
            }

            flowerGrowthController.SetStemMaterials(consistentStemMaterials);

            if (developmentStageDriven)
            {
                flowerGrowthController.SetDevelopmentStage(
                    currentDevelopmentStage,
                    flowerSupportHeight,
                    flowerSupportThickness);
                return;
            }

            if (maturityRequested)
            {
                flowerGrowthController.CompleteGrowthImmediately(
                    flowerSupportHeight,
                    flowerSupportThickness);
                return;
            }

            flowerGrowthController.StartFlowerGrowth(
                flowerSupportHeight,
                flowerSupportThickness);
        }

        private IEnumerator GrowMaturePlantToMaximumThickness()
        {
            GetPlantMaximumDimensions(out _, out float greatestThickness);

            while (greatestThickness < maximumStemThickness - 0.001f)
            {
                yield return WaitForSimulationSeconds(secondsBetweenGrowthSteps);

                float thicknessFactor = Mathf.Min(
                    baseThicknessGrowthMultiplier,
                    maximumStemThickness / Mathf.Max(greatestThickness, 0.0001f));

                ScaleAllStemThickness(thicknessFactor);

                GetPlantMaximumDimensions(out _, out greatestThickness);
            }
        }

        private bool HasReachedMaximumPlantHeight(out float topHeight)
        {
            GetPlantMaximumDimensions(out topHeight, out _);
            return topHeight >= maximumTotalStemLength - 0.001f;
        }

        private bool HasReachedMaximumPlantDimensions(out float topHeight)
        {
            GetPlantMaximumDimensions(out topHeight, out float greatestThickness);

            const float dimensionTolerance = 0.001f;
            bool reachedMaximumHeight = topHeight >= maximumTotalStemLength - dimensionTolerance;
            bool reachedMaximumThickness = greatestThickness >= maximumStemThickness - dimensionTolerance;
            return reachedMaximumHeight && reachedMaximumThickness;
        }

        private void GetPlantMaximumDimensions(
            out float topHeight,
            out float greatestThickness)
        {
            topHeight = 0f;
            greatestThickness = 0f;

            foreach (GameObject completedStem in completedStems)
            {
                AccumulateStemMaximums(completedStem, ref topHeight, ref greatestThickness);
            }

            AccumulateStemMaximums(activeStem, ref topHeight, ref greatestThickness);
        }

        private void AccumulateStemMaximums(
            GameObject stem,
            ref float topHeight,
            ref float greatestThickness)
        {
            StemDimensions dimensions = MeasureStem(stem);
            if (!dimensions.isValid)
            {
                return;
            }

            topHeight = Mathf.Max(topHeight, dimensions.topHeight);
            greatestThickness = Mathf.Max(
                greatestThickness,
                dimensions.baseThickness,
                dimensions.topThickness);
        }

        private void ShowArucoAnchor()
        {
            if (activeArucoAnchor != null)
            {
                Destroy(activeArucoAnchor);
                activeArucoAnchor = null;
            }

            if (arucoAnchorPrefab == null)
            {
                return;
            }

            Transform parent = modelContainer != null ? modelContainer : transform;
            activeArucoAnchor = Instantiate(arucoAnchorPrefab, parent, false);
            activeArucoAnchor.name = "Aruco Anchor";
            CopyPrefabTransform(activeArucoAnchor.transform, arucoAnchorPrefab.transform);
        }

        private void ShowBackdrop()
        {
            if (!showBackdrop)
            {
                return;
            }

            Transform parent = modelContainer != null ? modelContainer : transform;
            activeBackdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            activeBackdrop.name = "Plant Backdrop";
            activeBackdrop.transform.SetParent(parent, false);
            activeBackdrop.transform.localPosition = new Vector3(0f, backdropCenterHeight, backdropDistance);
            activeBackdrop.transform.localRotation = Quaternion.identity;
            activeBackdrop.transform.localScale = new Vector3(backdropSize.x, backdropSize.y, 0.04f);

            Collider backdropCollider = activeBackdrop.GetComponent<Collider>();
            if (backdropCollider != null)
            {
                backdropCollider.enabled = false;
            }

            Renderer backdropRenderer = activeBackdrop.GetComponent<Renderer>();
            Shader shader = Shader.Find("HDRP/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Standard");

            if (backdropRenderer == null || shader == null)
            {
                return;
            }

            backdropMaterial = new Material(shader)
            {
                name = "Runtime Plant Backdrop Material"
            };

            SetMaterialColor(backdropMaterial, "_UnlitColor", backdropColor);
            SetMaterialColor(backdropMaterial, "_BaseColor", backdropColor);
            SetMaterialColor(backdropMaterial, "_Color", backdropColor);
            backdropRenderer.material = backdropMaterial;
        }

        private static void SetMaterialColor(Material material, string propertyName, Color color)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        private void ShowStage(int stage)
        {
            StemDimensions previousDimensions = MeasureStem(activeStem);
            HideActiveStem();

            if (!HasPrefabForStage(stage))
            {
                return;
            }

            Transform parent = modelContainer != null ? modelContainer : transform;
            activeStem = Instantiate(stagePrefabs[stage - 1], parent, false);
            activeStem.name = $"Stem Stage {stage}";

            // Explicitly preserve the position, rotation and base scale authored in the prefab.
            CopyPrefabTransform(activeStem.transform, stagePrefabs[stage - 1].transform);
            activeStem.transform.localPosition += Vector3.up * currentStemBaseHeight;

            PrepareStemModel(activeStem);
            ApplyConsistentStemMaterials(activeStem);

            if (previousDimensions.isValid)
            {
                EnsureStemDoesNotShrink(activeStem, previousDimensions);
            }

            if (requiredBaseThicknessForNextStem > 0f)
            {
                MatchBaseThickness(activeStem, requiredBaseThicknessForNextStem);
                requiredBaseThicknessForNextStem = 0f;
            }

            ClampStemToMaximumDimensions(activeStem);
            CaptureInitialPlantDimensions();
        }

        private void CaptureInitialPlantDimensions()
        {
            if (initialPlantThickness > 0f)
            {
                return;
            }

            GetPlantMaximumDimensions(out float topHeight, out float greatestThickness);
            if (greatestThickness <= 0f)
            {
                return;
            }

            initialPlantHeight = topHeight;
            initialPlantThickness = Mathf.Min(greatestThickness, maximumStemThickness);
        }

        private void SynchronizePlantThicknessWithHeight()
        {
            GetPlantMaximumDimensions(out float topHeight, out float greatestThickness);
            if (greatestThickness <= 0f)
            {
                return;
            }

            if (initialPlantThickness <= 0f)
            {
                initialPlantHeight = topHeight;
                initialPlantThickness = Mathf.Min(greatestThickness, maximumStemThickness);
            }

            float growthHeightRange = Mathf.Max(
                maximumTotalStemLength - initialPlantHeight,
                0.0001f);
            float heightProgress = Mathf.Clamp01(
                (topHeight - initialPlantHeight) / growthHeightRange);
            float requiredThickness = Mathf.Lerp(
                initialPlantThickness,
                maximumStemThickness,
                heightProgress);

            if (greatestThickness >= requiredThickness - 0.0001f)
            {
                return;
            }

            float thicknessFactor = requiredThickness / Mathf.Max(greatestThickness, 0.0001f);
            ScaleAllStemThickness(thicknessFactor);
        }

        private void ScaleAllStemThickness(float thicknessFactor)
        {
            foreach (GameObject completedStem in completedStems)
            {
                if (completedStem != null)
                {
                    DeformStemThickness(completedStem, thicknessFactor, thicknessFactor);
                }
            }

            if (activeStem != null)
            {
                DeformStemThickness(activeStem, thicknessFactor, thicknessFactor);
            }
        }

        private void PrepareStemModel(GameObject stem)
        {
            foreach (Transform child in stem.GetComponentsInChildren<Transform>(true))
            {
                if (hideRoundedStemTips
                    && (child.name.Contains("Living Tip") || child.name.Contains("Rounded Tip")))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // Each runtime stem gets its own mesh, so tapering never modifies the prefab asset.
            foreach (MeshFilter meshFilter in GetRibbedStemMeshes(stem))
            {
                meshFilter.mesh = Instantiate(meshFilter.sharedMesh);
                meshFilter.mesh.name = $"{meshFilter.sharedMesh.name} Runtime Growth";
            }
        }

        private void ApplyConsistentStemMaterials(GameObject stem)
        {
            Renderer[] ribbedRenderers = GetRibbedStemRenderers(stem);
            if (ribbedRenderers.Length == 0)
            {
                return;
            }

            if (consistentStemMaterials == null || consistentStemMaterials.Length == 0)
            {
                consistentStemMaterials = CreateUnifiedStemMaterials(
                    ribbedRenderers[0].sharedMaterials);
            }

            foreach (Renderer ribbedRenderer in ribbedRenderers)
            {
                ribbedRenderer.sharedMaterials = consistentStemMaterials;
            }
        }

        private Material[] CreateUnifiedStemMaterials(Material[] sourceMaterials)
        {
            int materialCount = Mathf.Max(sourceMaterials != null ? sourceMaterials.Length : 0, 1);
            var materials = new Material[materialCount];

            for (int index = 0; index < materialCount; index++)
            {
                Material source = sourceMaterials != null && index < sourceMaterials.Length
                    ? sourceMaterials[index]
                    : null;
                Shader unifiedShader = Shader.Find("HDRP/Unlit")
                                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                                    ?? Shader.Find("Unlit/Color")
                                    ?? (source != null ? source.shader : Shader.Find("Standard"));
                Material material = new Material(unifiedShader);
                material.name = "Runtime Unified Stem Green";

                if (material.HasProperty("_BaseColorMap"))
                {
                    material.SetTexture("_BaseColorMap", Texture2D.whiteTexture);
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", Texture2D.whiteTexture);
                }

                SetMaterialColor(material, "_BaseColor", unifiedStemColor);
                SetMaterialColor(material, "_Color", unifiedStemColor);
                SetMaterialColor(material, "_UnlitColor", unifiedStemColor);
                SetMaterialColor(material, "_EmissionColor", Color.black);
                SetMaterialColor(material, "_EmissiveColor", Color.black);
                materials[index] = material;
            }

            return materials;
        }

        private void GrowActiveStemBiologically()
        {
            StemDimensions dimensions = MeasureStem(activeStem);
            if (!dimensions.isValid)
            {
                return;
            }

            float maximumSegmentLength = Mathf.Max(
                0.001f,
                maximumTotalStemLength - currentStemBaseHeight);
            float targetLength = Mathf.Min(
                dimensions.height * scaleMultiplier,
                maximumSegmentLength);

            if (targetLength > dimensions.height)
            {
                Vector3 scale = activeStem.transform.localScale;
                scale.y *= targetLength / dimensions.height;
                activeStem.transform.localScale = scale;
            }

            float targetBaseThickness = Mathf.Min(
                dimensions.baseThickness * baseThicknessGrowthMultiplier,
                maximumStemThickness);
            if (currentSegmentBaseThicknessLimit > 0f)
            {
                targetBaseThickness = Mathf.Min(
                    targetBaseThickness,
                    currentSegmentBaseThicknessLimit);
            }

            float targetTipThickness = Mathf.Min(
                dimensions.topThickness * tipThicknessGrowthMultiplier,
                maximumStemThickness);

            DeformStemThickness(
                activeStem,
                targetBaseThickness / Mathf.Max(dimensions.baseThickness, 0.0001f),
                targetTipThickness / Mathf.Max(dimensions.topThickness, 0.0001f));

            ClampStemToMaximumDimensions(activeStem);
        }

        private void EnsureStemDoesNotShrink(GameObject stem, StemDimensions minimumDimensions)
        {
            StemDimensions currentDimensions = MeasureStem(stem);
            if (!currentDimensions.isValid)
            {
                return;
            }

            if (currentDimensions.height < minimumDimensions.height)
            {
                Vector3 scale = stem.transform.localScale;
                scale.y *= minimumDimensions.height / Mathf.Max(currentDimensions.height, 0.0001f);
                stem.transform.localScale = scale;
                currentDimensions = MeasureStem(stem);
            }

            float baseFactor = minimumDimensions.baseThickness / Mathf.Max(currentDimensions.baseThickness, 0.0001f);
            float tipFactor = minimumDimensions.topThickness / Mathf.Max(currentDimensions.topThickness, 0.0001f);
            DeformStemThickness(stem, Mathf.Max(1f, baseFactor), Mathf.Max(1f, tipFactor));
        }

        private void MatchBaseThickness(GameObject stem, float requiredThickness)
        {
            // A few small correction passes compensate for the smooth taper band.
            for (int pass = 0; pass < 3; pass++)
            {
                StemDimensions dimensions = MeasureStem(stem);
                if (!dimensions.isValid)
                {
                    return;
                }

                float difference = Mathf.Abs(dimensions.baseThickness - requiredThickness);
                if (difference <= 0.0001f)
                {
                    break;
                }

                float baseFactor = requiredThickness / Mathf.Max(dimensions.baseThickness, 0.0001f);
                DeformStemThickness(stem, baseFactor, 1f);
            }
        }

        private void ClampStemToMaximumDimensions(GameObject stem)
        {
            StemDimensions dimensions = MeasureStem(stem);
            if (!dimensions.isValid)
            {
                return;
            }

            float maximumSegmentLength = Mathf.Max(
                0.001f,
                maximumTotalStemLength - currentStemBaseHeight);
            if (dimensions.height > maximumSegmentLength)
            {
                Vector3 scale = stem.transform.localScale;
                scale.y *= maximumSegmentLength / dimensions.height;
                stem.transform.localScale = scale;
                dimensions = MeasureStem(stem);
            }

            float allowedBaseThickness = maximumStemThickness;
            if (currentSegmentBaseThicknessLimit > 0f)
            {
                allowedBaseThickness = Mathf.Min(
                    allowedBaseThickness,
                    currentSegmentBaseThicknessLimit);
            }

            float baseFactor = dimensions.baseThickness > allowedBaseThickness
                ? allowedBaseThickness / dimensions.baseThickness
                : 1f;
            float tipFactor = dimensions.topThickness > maximumStemThickness
                ? maximumStemThickness / dimensions.topThickness
                : 1f;

            if (baseFactor < 1f || tipFactor < 1f)
            {
                DeformStemThickness(stem, baseFactor, tipFactor);
            }
        }

        private static void DeformStemThickness(GameObject stem, float baseFactor, float tipFactor)
        {
            foreach (MeshFilter meshFilter in GetRibbedStemMeshes(stem))
            {
                Mesh mesh = meshFilter.mesh;
                Vector3[] vertices = mesh.vertices;
                if (vertices.Length == 0)
                {
                    continue;
                }

                float minimumY = vertices[0].y;
                float maximumY = vertices[0].y;
                foreach (Vector3 vertex in vertices)
                {
                    minimumY = Mathf.Min(minimumY, vertex.y);
                    maximumY = Mathf.Max(maximumY, vertex.y);
                }

                float height = Mathf.Max(maximumY - minimumY, 0.0001f);
                for (int index = 0; index < vertices.Length; index++)
                {
                    Vector3 vertex = vertices[index];
                    float normalizedHeight = Mathf.Clamp01((vertex.y - minimumY) / height);
                    float smoothHeight = Mathf.SmoothStep(0f, 1f, normalizedHeight);
                    float radialFactor = Mathf.Lerp(baseFactor, tipFactor, smoothHeight);
                    vertex.x *= radialFactor;
                    vertex.z *= radialFactor;
                    vertices[index] = vertex;
                }

                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
            }
        }

        private StemDimensions MeasureStem(GameObject stem)
        {
            if (stem == null)
            {
                return default;
            }

            Transform parent = modelContainer != null ? modelContainer : transform;
            var points = new List<Vector3>();

            foreach (MeshFilter meshFilter in GetRibbedStemMeshes(stem))
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                foreach (Vector3 vertex in mesh.vertices)
                {
                    points.Add(parent.InverseTransformPoint(meshFilter.transform.TransformPoint(vertex)));
                }
            }

            if (points.Count == 0)
            {
                return default;
            }

            float minimumY = points[0].y;
            float maximumY = points[0].y;
            foreach (Vector3 point in points)
            {
                minimumY = Mathf.Min(minimumY, point.y);
                maximumY = Mathf.Max(maximumY, point.y);
            }

            float height = Mathf.Max(maximumY - minimumY, 0.0001f);
            float baseThickness = MeasureBandThickness(points, minimumY, maximumY, false);
            float topThickness = MeasureBandThickness(points, minimumY, maximumY, true);

            return new StemDimensions
            {
                isValid = true,
                height = height,
                baseThickness = baseThickness,
                topThickness = topThickness,
                topHeight = maximumY
            };
        }

        private float MeasureStemTopInterfaceThickness(GameObject stem)
        {
            if (stem == null)
            {
                return 0f;
            }

            Transform parent = modelContainer != null ? modelContainer : transform;
            var points = new List<Vector3>();

            foreach (MeshFilter meshFilter in GetRibbedStemMeshes(stem))
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                foreach (Vector3 vertex in mesh.vertices)
                {
                    points.Add(parent.InverseTransformPoint(
                        meshFilter.transform.TransformPoint(vertex)));
                }
            }

            if (points.Count == 0)
            {
                return 0f;
            }

            float minimumY = points[0].y;
            float maximumY = points[0].y;
            foreach (Vector3 point in points)
            {
                minimumY = Mathf.Min(minimumY, point.y);
                maximumY = Mathf.Max(maximumY, point.y);
            }

            return MeasureBandThickness(
                points,
                minimumY,
                maximumY,
                true,
                0.04f);
        }

        private static float MeasureBandThickness(
            List<Vector3> points,
            float minimumY,
            float maximumY,
            bool topBand,
            float bandFraction = 0.18f)
        {
            float bandHeight = (maximumY - minimumY)
                * Mathf.Clamp(bandFraction, 0.01f, 1f);
            float bandMinimum = topBand ? maximumY - bandHeight : minimumY;
            float bandMaximum = topBand ? maximumY : minimumY + bandHeight;
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumZ = float.PositiveInfinity;
            float maximumZ = float.NegativeInfinity;

            foreach (Vector3 point in points)
            {
                if (point.y < bandMinimum || point.y > bandMaximum)
                {
                    continue;
                }

                minimumX = Mathf.Min(minimumX, point.x);
                maximumX = Mathf.Max(maximumX, point.x);
                minimumZ = Mathf.Min(minimumZ, point.z);
                maximumZ = Mathf.Max(maximumZ, point.z);
            }

            if (float.IsInfinity(minimumX))
            {
                return 0.0001f;
            }

            return Mathf.Max(maximumX - minimumX, maximumZ - minimumZ);
        }

        private static MeshFilter[] GetRibbedStemMeshes(GameObject stem)
        {
            var stemMeshes = new List<MeshFilter>();
            foreach (MeshFilter meshFilter in stem.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.name.Contains("Ribbed Stem"))
                {
                    stemMeshes.Add(meshFilter);
                }
            }

            return stemMeshes.ToArray();
        }

        private static Renderer[] GetRibbedStemRenderers(GameObject stem)
        {
            var stemRenderers = new List<Renderer>();
            foreach (Renderer stemRenderer in stem.GetComponentsInChildren<Renderer>(true))
            {
                if (stemRenderer.name.Contains("Ribbed Stem"))
                {
                    stemRenderers.Add(stemRenderer);
                }
            }

            return stemRenderers.ToArray();
        }

        private struct StemDimensions
        {
            public bool isValid;
            public float height;
            public float baseThickness;
            public float topThickness;
            public float topHeight;
        }

        private void HideActiveStem()
        {
            if (activeStem == null)
            {
                return;
            }

            Destroy(activeStem);
            activeStem = null;
        }

        private void ClearAllStems()
        {
            HideActiveStem();

            foreach (GameObject completedStem in completedStems)
            {
                if (completedStem != null)
                {
                    Destroy(completedStem);
                }
            }

            completedStems.Clear();
        }

        private static void CopyPrefabTransform(Transform instanceTransform, Transform prefabTransform)
        {
            instanceTransform.localPosition = prefabTransform.localPosition;
            instanceTransform.localRotation = prefabTransform.localRotation;
            instanceTransform.localScale = prefabTransform.localScale;
        }

        private bool HasPrefabForStage(int stage)
        {
            int index = stage - 1;
            return stagePrefabs != null
                   && index >= 0
                   && index < stagePrefabs.Length
                   && stagePrefabs[index] != null;
        }

        private IEnumerator WaitForSimulationSeconds(float seconds)
        {
            if (developmentStageDriven)
            {
                nextVegetativeDvsThreshold = Mathf.Min(
                    1f,
                    nextVegetativeDvsThreshold + VegetativeDvsStep);
                while (currentDevelopmentStage + 0.0001f
                    < nextVegetativeDvsThreshold
                    && currentDevelopmentStage < 1f)
                {
                    yield return null;
                }

                yield break;
            }

            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                remaining -= Time.deltaTime * Mathf.Max(0.1f, simulationSpeedMultiplier);
                yield return null;
            }
        }

        private void OnValidate()
        {
            startingStage = Mathf.Clamp(startingStage, 1, TerminalStage);
            secondsBeforeStemAppears = Mathf.Max(0f, secondsBeforeStemAppears);
            backdropDistance = Mathf.Max(0.05f, backdropDistance);
            backdropSize.x = Mathf.Max(0.1f, backdropSize.x);
            backdropSize.y = Mathf.Max(0.1f, backdropSize.y);
            secondsBetweenGrowthSteps = Mathf.Max(0.01f, secondsBetweenGrowthSteps);
            scaleMultiplier = Mathf.Max(1f, scaleMultiplier);
            baseThicknessGrowthMultiplier = Mathf.Max(1f, baseThicknessGrowthMultiplier);
            tipThicknessGrowthMultiplier = Mathf.Clamp(
                tipThicknessGrowthMultiplier,
                1f,
                baseThicknessGrowthMultiplier);
            maximumTotalStemLength = Mathf.Max(0.1f, maximumTotalStemLength);
            maximumStemThickness = Mathf.Max(0.001f, maximumStemThickness);
            growthStepsBeforeNextStage = Mathf.Max(1, growthStepsBeforeNextStage);
            secondsBeforeNextStackedStem = Mathf.Max(0f, secondsBeforeNextStackedStem);
            stackedStemOverlap = Mathf.Max(0f, stackedStemOverlap);
            simulationSpeedMultiplier = Mathf.Max(0.1f, simulationSpeedMultiplier);

            if (stagePrefabs == null || stagePrefabs.Length != TerminalStage)
            {
                System.Array.Resize(ref stagePrefabs, TerminalStage);
            }
        }
    }
}
