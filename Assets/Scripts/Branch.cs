using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Branch : MonoBehaviour
{
    private const float GoldenAngle = 137.5f;
    private const int MaximumBranchGeneration = 5;
    private const int MaximumChildBranchesPerStem = 7;
    private const int FreeSpacePathSamples = 6;
    private const int RandomGrowthZoneSamples = 10;
    private static readonly List<Branch> ActiveBranches = new List<Branch>();
    private static readonly List<Branch> PendingBranchGrowth = new List<Branch>();
    private static readonly List<Branch> ActiveBranchGrowth = new List<Branch>();
    private static bool supportedWeightCacheDirty = true;
    private static float nextSupportedWeightCacheRefreshAt;
    private static int lastGrowthSchedulerFrame = -1;

    private readonly struct CanopyBalanceState
    {
        public CanopyBalanceState(
            Vector3 weightedMoment,
            float totalWeight,
            Vector3 heavierSideDirection,
            float heavierSideWeight,
            float lighterSideWeight,
            float normalizedSideImbalance)
        {
            WeightedMoment = weightedMoment;
            TotalWeight = totalWeight;
            HeavierSideDirection = heavierSideDirection;
            HeavierSideWeight = heavierSideWeight;
            LighterSideWeight = lighterSideWeight;
            NormalizedSideImbalance = normalizedSideImbalance;
        }

        public Vector3 WeightedMoment { get; }
        public float TotalWeight { get; }
        public Vector3 HeavierSideDirection { get; }
        public float HeavierSideWeight { get; }
        public float LighterSideWeight { get; }
        public float NormalizedSideImbalance { get; }
    }

    private readonly struct ArcAttachmentPose
    {
        public ArcAttachmentPose(
            float straightHeight,
            Quaternion straightLocalRotation,
            bool followsStemTangent)
        {
            StraightHeight = straightHeight;
            StraightLocalRotation = straightLocalRotation;
            FollowsStemTangent = followsStemTangent;
        }

        public float StraightHeight { get; }
        public Quaternion StraightLocalRotation { get; }
        public bool FollowsStemTangent { get; }
    }

    [Header("Recursive stems")]
    public GameObject jointPrefab;
    public Transform modelCyl;

    [Min(0f)]
    public float delayBranches = 2f;

    [Min(0.01f)]
    public float vitezaCrestere = 0.5f;

    [Min(0.05f)]
    public float maxHeight = 1.75f;

    [Range(0, MaximumBranchGeneration)]
    public int maxGen = MaximumBranchGeneration;

    [Header("Randomized branch counts")]
    [Range(0, MaximumChildBranchesPerStem)]
    public int firstOrderBranchesMin = 7;
    [Range(0, MaximumChildBranchesPerStem)]
    public int firstOrderBranchesMax = 7;
    [Range(0, MaximumChildBranchesPerStem)]
    public int secondOrderBranchesMin = 5;
    [Range(0, MaximumChildBranchesPerStem)]
    public int secondOrderBranchesMax = 6;
    [Range(0, MaximumChildBranchesPerStem)]
    public int thirdOrderBranchesMin = 4;
    [Range(0, MaximumChildBranchesPerStem)]
    public int thirdOrderBranchesMax = 6;
    [Range(0, MaximumChildBranchesPerStem)]
    public int fourthOrderBranchesMin = 4;
    [Range(0, MaximumChildBranchesPerStem)]
    public int fourthOrderBranchesMax = 5;
    [Range(0, MaximumChildBranchesPerStem)]
    public int fifthOrderBranchesMin = 3;
    [Range(0, MaximumChildBranchesPerStem)]
    public int fifthOrderBranchesMax = 4;

    [Range(30f, 80f)]
    [Tooltip("Tilt from vertical used by the lowest child stem to widen the plant.")]
    public float lowerChildBranchAngle = 60f;

    [Range(5f, 50f)]
    [Tooltip("Tilt from vertical used by the highest child stem to continue upward growth.")]
    public float upperChildBranchAngle = 28f;

    [Header("Attachment zones")]
    [Range(0.05f, 0.7f)]
    [Tooltip("Lowest height on the main trunk at which an order-1 branch may attach.")]
    public float trunkBranchAttachmentStartFraction = 0.2f;

    [Range(0.25f, 0.98f)]
    [Tooltip("Highest height on the main trunk at which an order-1 branch may attach.")]
    public float trunkBranchAttachmentEndFraction = 0.9f;

    [Range(0.05f, 0.85f)]
    [Tooltip("Lower attachment limit used on branches of order 1 and higher.")]
    public float childStemAttachmentStartFraction = 0.6f;

    [Range(0.15f, 1f)]
    [Tooltip("Upper attachment limit used on branches of order 1 and higher.")]
    public float childStemAttachmentEndFraction = 0.9f;

    [Range(0.35f, 0.95f)]
    public float childLengthMultiplier = 0.8f;

    [Range(0.8f, 1.4f)]
    [Tooltip("Keeps the requested first-order length while later orders use the original recursive length ratio.")]
    public float primaryBranchLengthBoost = 1.05f;

    [Range(0.35f, 0.95f)]
    public float childThicknessMultiplier = 0.72f;

    [Header("Upper crown dome")]
    [Range(0, 6)]
    [Tooltip("Minimum number of short first-order twigs added near the top of the trunk to close the crown.")]
    public int crownFillerBranchesMin = 3;

    [Range(0, 6)]
    [Tooltip("Maximum number of short first-order twigs added near the top of the trunk to close the crown.")]
    public int crownFillerBranchesMax = 4;

    [Range(0.65f, 0.95f)]
    [Tooltip("Lowest trunk fraction used by the short dome-forming twigs.")]
    public float crownFillerAttachmentStartFraction = 0.8f;

    [Range(0.8f, 0.99f)]
    [Tooltip("Highest trunk fraction used by the short dome-forming twigs.")]
    public float crownFillerAttachmentEndFraction = 0.98f;

    [Range(0.2f, 0.7f)]
    [Tooltip("Length multiplier for the lowest dome-forming twig.")]
    public float crownFillerLowerLengthMultiplier = 0.42f;

    [Range(0.15f, 0.6f)]
    [Tooltip("Length multiplier for the highest dome-forming twig.")]
    public float crownFillerUpperLengthMultiplier = 0.24f;

    [Range(20f, 75f)]
    [Tooltip("Tilt from vertical of the lowest dome-forming twig.")]
    public float crownFillerLowerTiltDegrees = 55f;

    [Range(15f, 65f)]
    [Tooltip("Tilt from vertical of the highest dome-forming twig.")]
    public float crownFillerUpperTiltDegrees = 38f;

    [Range(0.3f, 0.8f)]
    [Tooltip("Thickness inherited by the short dome-forming twigs.")]
    public float crownFillerThicknessMultiplier = 0.58f;

    [Range(0, 2)]
    [Tooltip("Maximum number of generations allowed after a dome-forming twig.")]
    public int crownFillerDescendantGenerations = 2;

    [Header("Spatial growth strategy")]
    [Min(1f)]
    [Tooltip("Side of the free-space square, expressed in stem thicknesses. 10 means 1000% x 1000% of the new stem thickness.")]
    public float freeSpaceSquareThicknessMultiplier = 10f;

    [Min(0f)]
    [Tooltip("Radius around the best free-space target in which the final growth direction is selected randomly, expressed in new-branch thicknesses.")]
    public float randomGrowthZoneRadiusInStemThicknesses = 5f;

    [Range(8, 32)]
    [Tooltip("Number of possible directions evaluated before a new stem starts growing.")]
    public int growthDirectionCandidateCount = 16;

    [Range(0f, 0.8f)]
    [Tooltip("Minimum horizontal divergence from the main stem axis. Higher values force child stems more directly outward.")]
    public float minimumOutwardDivergence = 0.2f;

    [Min(0f)]
    [Tooltip("Strength of canopy balancing when several directions are equally free. 0 disables balancing.")]
    public float canopyBalanceInfluence = 4f;

    [Header("Higher-order open-space gate")]
    [Range(0.5f, 4f)]
    [Tooltip("Orders 2 and higher grow only when their predicted path has at least this much surface clearance, expressed in new-stem thicknesses.")]
    public float minimumHigherOrderClearanceInStemThicknesses = 1.25f;

    [Header("Stem appearance")]
    public Color stemColor = new Color(0.24f, 0.13f, 0.07f, 1f);

    [Header("Weight bending")]
    [Range(0f, 18f)]
    [Tooltip("Maximum extra downward bend at the base of a loaded side branch.")]
    public float maximumWeightBendDegrees = 9f;

    [Range(0f, 0.35f)]
    [Tooltip("The main trunk remains almost rigid while younger branches bend more easily.")]
    public float mainTrunkBendMultiplier = 0.08f;

    [Min(0.1f)]
    [Tooltip("How smoothly a branch reacts when wood, leaves and child stems add weight.")]
    public float weightBendResponse = 2.4f;

    [Range(0f, 1.5f)]
    [Tooltip("Adds a circular arc along the loaded stem while preserving its existing base lean.")]
    public float weightArcBendMultiplier = 0.72f;

    [Range(0f, 12f)]
    [Tooltip("Maximum extra circular curvature along a loaded stem.")]
    public float maximumWeightArcDegrees = 7.5f;

    [Header("Maple structural stages")]
    [Range(0.1f, 0.45f)]
    public float stageOneLengthFraction = 0.28f;

    [Range(0.3f, 0.7f)]
    public float stageTwoLengthFraction = 0.52f;

    [Range(0.55f, 0.9f)]
    public float stageThreeLengthFraction = 0.76f;

    [Range(0.15f, 0.6f)]
    public float stageOneThicknessFraction = 0.38f;

    [Range(0.35f, 0.8f)]
    public float stageTwoThicknessFraction = 0.62f;

    [Range(0.6f, 0.95f)]
    public float stageThreeThicknessFraction = 0.82f;

    [Min(0f)]
    public float secondsBetweenStructuralStages = 0.65f;

    [Header("Long-term secondary growth")]
    [Range(0f, 0.8f)]
    [Tooltip("Length added during every long-term growth unit. Growth has no hard maximum; higher-order stems retain a progressively smaller fraction.")]
    public float matureLengthIncrease = 0.4f;

    [Range(0f, 1.6f)]
    [Tooltip("Radial thickness added during every long-term growth unit. Growth has no hard maximum; older wood thickens more than young twigs.")]
    public float matureThicknessIncrease = 0.9f;

    [Range(0.35f, 1f)]
    [Tooltip("Fraction of long-term growth retained by each successive branch order.")]
    public float secondaryGrowthRetentionPerOrder = 0.68f;

    [Min(5f)]
    [Tooltip("Approximate seconds per long-term growth unit at normal biological conditions and 1x playback.")]
    public float secondsToFullSecondaryGrowth = 36f;

    [Range(1f, 3f)]
    [Tooltip("How much chronological age accelerates secondary growth. Young stems start slowly, while old stems lengthen and thicken more strongly.")]
    public float oldStemGrowthAcceleration = 1.65f;

    [Range(1f, 2f)]
    [Tooltip("Extra secondary-growth priority reserved for the main trunk.")]
    public float trunkSecondaryGrowthPriority = 1.25f;

    [Header("Leaves")]
    [Range(4, 64)]
    [Tooltip("Maximum randomized leaf count before spacing and stem length are applied.")]
    public int leavesPerStem = 48;

    [Range(4, 64)]
    [Tooltip("Minimum randomized leaf count before spacing and stem length are applied.")]
    public int minimumLeavesPerStem = 32;

    [Range(0.2f, 0.5f)]
    [Tooltip("Fraction of every stem kept free of leaves at its base.")]
    public float firstLeafHeightFraction = 0.3f;

    [Range(0.55f, 0.98f)]
    [Tooltip("Highest fraction of the stem at which a leaf node may appear.")]
    public float lastLeafHeightFraction = 0.95f;

    [Min(0.05f)]
    [Tooltip("Minimum distance between consecutive leaf nodes on the main stem. Shorter child stems scale this distance proportionally.")]
    public float minimumLeafNodeSpacing = 0.16f;

    [Range(0.3f, 0.8f)]
    [Tooltip("Minimum leaf-node spacing relative to the leaf length.")]
    public float leafLengthSpacingMultiplier = 0.34f;

    [Range(1f, 4f)]
    [Tooltip("Minimum leaf-node spacing relative to the stem radius.")]
    public float stemRadiusLeafSpacingMultiplier = 1.6f;

    [Min(0.05f)]
    public float leafLength = 0.62f;

    [Range(0.4f, 1.1f)]
    public float leafWidthToLength = 0.92f;

    [Min(0.05f)]
    public float leafGrowthDuration = 1.35f;

    [Range(0.01f, 0.4f)]
    [Tooltip("The stem tip must pass the leaf node by this distance before the leaf can appear.")]
    public float leafStemAdvanceMargin = 0.12f;

    [Range(0f, 2f)]
    [Tooltip("Time for which the visible stem must already cover a leaf node before that leaf starts growing.")]
    public float leafNodeMaturationDelay = 0.35f;

    public Color leafColor = new Color(0.10f, 0.38f, 0.09f, 1f);

    [Header("Runtime state")]
    public bool maxed;
    public bool init;
    public bool readyToGrow;
    public int currGen;

    [Range(1, 4)]
    public int currentStructuralStage = 1;

    private float originalRadiusX;
    private float originalRadiusZ;
    private int nextLeafIndex;
    private int runtimeLeafCount;
    private bool leafSpawningComplete;
    private bool jointsStarted;
    private PlantLSystem.StemPlan[] queuedChildPlans;
    private float nextLeafNodeSupportedSince = -1f;
    private MaterialPropertyBlock stemProperties;
    private Branch mainStem;
    private Branch supportingStem;
    private PlantLSystem growthGrammar;
    private Plant mapleSimulation;
    private int plannedLeafBudget = -1;
    private float cumulativeThicknessScale = 1f;
    private float structuralStageReachedAt = -1f;
    private Quaternion unloadedLocalRotation;
    private Vector3 weightBendAxis = Vector3.right;
    private float currentWeightBendDegrees;
    private float targetWeightBendDegrees;
    private float nextWeightEvaluationAt;
    private float nextArcVisualUpdateAt;
    private int cachedSupportedLeaves;
    private int cachedSupportedChildStems;
    private MapleStemModel stemModel;
    private float currentStemArcDegrees;
    private Vector3 currentStemArcDirection;
    private float secondaryGrowthProgress;
    private float secondaryGrowthAgeStrength;
    private float currentSecondaryGrowthRateMultiplier = 1f;
    private float currentMatureLengthMultiplier = 1f;
    private float currentMatureThicknessMultiplier = 1f;
    private float nextSecondaryGrowthUpdateAt;
    private float lastSecondaryGrowthUpdateAt = -1f;
    private readonly Dictionary<Transform, ArcAttachmentPose> arcAttachments =
        new Dictionary<Transform, ArcAttachmentPose>();
    private readonly List<Transform> invalidArcAttachments =
        new List<Transform>();

    public float GrowthStartedAt { get; private set; } = -1f;
    public PlantLSystem GrowthGrammar => growthGrammar;
    public float LastCanopySideImbalance { get; private set; }
    public Vector3 LastHeavierCanopyDirection { get; private set; }
    public float LastOptimalClearance { get; private set; }
    public float LastSelectedClearance { get; private set; }
    public float LastRandomGrowthZoneRadius { get; private set; }
    public bool LastGrowthUsedRandomZone { get; private set; }
    public int CurrentStructuralStage => currentStructuralStage;
    public float CurrentWeightBendDegrees => currentWeightBendDegrees;
    public float CurrentStemArcDegrees => currentStemArcDegrees;
    public float SecondaryGrowthProgress => secondaryGrowthProgress;
    public float SecondaryGrowthAgeStrength => secondaryGrowthAgeStrength;
    public float CurrentSecondaryGrowthRateMultiplier =>
        currentSecondaryGrowthRateMultiplier;
    public float CurrentMatureLengthMultiplier =>
        currentMatureLengthMultiplier;
    public float CurrentMatureThicknessMultiplier =>
        currentMatureThicknessMultiplier;

    private void Awake()
    {
        unloadedLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        if (!ActiveBranches.Contains(this))
        {
            ActiveBranches.Add(this);
        }

        supportedWeightCacheDirty = true;
    }

    private void OnDisable()
    {
        ActiveBranches.Remove(this);
        PendingBranchGrowth.Remove(this);
        ActiveBranchGrowth.Remove(this);
        supportedWeightCacheDirty = true;
    }

    private void Start()
    {
        mapleSimulation = GetComponentInParent<Plant>();
        if (mainStem == null && currGen == 0)
        {
            mainStem = this;
            GrowthStartedAt = GetVisualGrowthClock();
            growthGrammar = GetComponent<PlantLSystem>();
            if (growthGrammar == null)
            {
                growthGrammar = gameObject.AddComponent<PlantLSystem>();
            }

            growthGrammar.BeginPlant(this);
            plannedLeafBudget = growthGrammar.CreateRootLeafBudget(this);
        }

        if (modelCyl == null)
        {
            Debug.LogError("Branch requires a cylinder in Model Cyl.", this);
            enabled = false;
            return;
        }

        stemModel = modelCyl.GetComponent<MapleStemModel>();
        originalRadiusX = Mathf.Max(0.01f, modelCyl.localScale.x);
        originalRadiusZ = Mathf.Max(0.01f, modelCyl.localScale.z);

        modelCyl.localScale = new Vector3(
            originalRadiusX
                * cumulativeThicknessScale
                * GetStructuralThicknessFraction(),
            0f,
            originalRadiusZ
                * cumulativeThicknessScale
                * GetStructuralThicknessFraction());
        modelCyl.localPosition = Vector3.zero;
        modelCyl.name = "Stem Cylinder";

        ApplyStemAppearance();
        float weightInterval = GetWeightEvaluationInterval();
        float evaluationPhase = Mathf.Abs(GetInstanceID() % 997) / 997f;
        nextWeightEvaluationAt = Time.time + weightInterval * evaluationPhase;
        nextArcVisualUpdateAt = Time.time + 0.2f * evaluationPhase;
        nextSecondaryGrowthUpdateAt = Time.time + 0.2f * evaluationPhase;
        // This is an upper limit. The available stem length and the minimum
        // internode distance decide how many leaves actually fit.
        runtimeLeafCount = plannedLeafBudget >= 0
            ? plannedLeafBudget
            : Mathf.Max(0, leavesPerStem - currGen * 2);

        if (currGen == 0)
        {
            init = true;
            readyToGrow = true;
        }
    }

    private void Update()
    {
        float visualGrowthDeltaTime = GetVisualGrowthDeltaTime();
        if (mapleSimulation != null && visualGrowthDeltaTime <= 0.000001f)
        {
            return;
        }

        TryStartQueuedGrowth();

        if (!readyToGrow || !init || modelCyl == null)
        {
            return;
        }

        // A fully elongated stem can still mature its remaining supported
        // leaf nodes. "maxed" stops only stem elongation and new branching.
        if (maxed)
        {
            SpawnLeavesReachedByVisibleStem();
            UpdateLongTermSecondaryGrowth();
            return;
        }

        float growthRateMultiplier = mapleSimulation != null
            ? mapleSimulation.BiologicalStructuralGrowthRateMultiplier
            : 1f;
        growthRateMultiplier = Mathf.Max(0f, growthRateMultiplier);
        float allowedScaleY = maxHeight * GetStructuralLengthFraction();
        float nextScaleY = Mathf.MoveTowards(
            modelCyl.localScale.y,
            allowedScaleY,
            vitezaCrestere
                * growthRateMultiplier
                * visualGrowthDeltaTime);
        float targetScaleX = originalRadiusX
            * cumulativeThicknessScale
            * GetStructuralThicknessFraction();
        float targetScaleZ = originalRadiusZ
            * cumulativeThicknessScale
            * GetStructuralThicknessFraction();
        float thicknessGrowthSpeed = Mathf.Max(
            0.01f,
            Mathf.Max(originalRadiusX, originalRadiusZ)
                * cumulativeThicknessScale
                * 0.55f
                * growthRateMultiplier
                * Mathf.Max(
                    1f,
                    vitezaCrestere / Mathf.Max(0.05f, maxHeight)));
        Vector3 scale = modelCyl.localScale;
        scale.y = nextScaleY;
        scale.x = Mathf.MoveTowards(
            scale.x,
            targetScaleX,
            thicknessGrowthSpeed * visualGrowthDeltaTime);
        scale.z = Mathf.MoveTowards(
            scale.z,
            targetScaleZ,
            thicknessGrowthSpeed * visualGrowthDeltaTime);
        modelCyl.localScale = scale;

        // A Unity cylinder is two units high. Moving its centre by scaleY keeps
        // its lower cap fixed at the branch origin while its tip grows outward.
        modelCyl.localPosition = Vector3.up * nextScaleY;
        SpawnLeavesReachedByVisibleStem();

        bool lengthReached = nextScaleY >= allowedScaleY - 0.0001f;
        bool thicknessReached = Mathf.Abs(scale.x - targetScaleX) <= 0.0001f
            && Mathf.Abs(scale.z - targetScaleZ) <= 0.0001f;
        if (!lengthReached || !thicknessReached)
        {
            structuralStageReachedAt = -1f;
            return;
        }

        if (currentStructuralStage < 4)
        {
            if (structuralStageReachedAt < 0f)
            {
                structuralStageReachedAt = GetVisualGrowthClock();
                return;
            }

            float stagePause = secondsBetweenStructuralStages
                / Mathf.Max(0.2f, growthRateMultiplier);
            if (GetVisualGrowthClock() - structuralStageReachedAt < stagePause)
            {
                return;
            }

            currentStructuralStage++;
            structuralStageReachedAt = -1f;
            return;
        }

        maxed = true;
        PlantLSystem.StemPlan[] childPlans = growthGrammar != null
            ? growthGrammar.RewriteStem(this)
            : new PlantLSystem.StemPlan[0];
        if (!jointsStarted
            && nextScaleY > 0.025f
            && currGen < maxGen
            && childPlans.Length > 0)
        {
            jointsStarted = true;
            queuedChildPlans = childPlans;
            QueueChildGrowth();
        }
    }

    private void UpdateLongTermSecondaryGrowth()
    {
        float growthClock = GetVisualGrowthClock();
        float elapsedGrowthTime = GetVisualGrowthDeltaTime();
        if (PlantVisualQuality.LiteModeEnabled)
        {
            if (Time.time < nextSecondaryGrowthUpdateAt)
            {
                return;
            }

            if (lastSecondaryGrowthUpdateAt >= 0f)
            {
                elapsedGrowthTime = Mathf.Max(
                    0f,
                    growthClock - lastSecondaryGrowthUpdateAt);
            }

            nextSecondaryGrowthUpdateAt = Time.time + 0.2f;
        }

        lastSecondaryGrowthUpdateAt = growthClock;
        if (elapsedGrowthTime <= 0.000001f)
        {
            return;
        }

        float orderProgress = Mathf.Clamp01(
            currGen / (float)MaximumBranchGeneration);
        float orderPriority = Mathf.Lerp(1.18f, 0.72f, orderProgress);
        float stemAgeSeconds = GrowthStartedAt >= 0f
            ? Mathf.Max(0f, growthClock - GrowthStartedAt)
            : 0f;
        float ageNormalizationSeconds = Mathf.Max(
            5f,
            secondsToFullSecondaryGrowth * 0.65f);
        float normalizedAge = Mathf.Clamp01(
            stemAgeSeconds / ageNormalizationSeconds);
        secondaryGrowthAgeStrength = normalizedAge
            * normalizedAge
            * (3f - 2f * normalizedAge);
        float youngStemRate = 1f / Mathf.Max(
            1f,
            oldStemGrowthAcceleration);
        float chronologicalAgePriority = Mathf.Lerp(
            youngStemRate,
            oldStemGrowthAcceleration,
            secondaryGrowthAgeStrength);
        float trunkPriority = currGen == 0
            ? trunkSecondaryGrowthPriority
            : 1f;
        float biologicalDrive = mapleSimulation != null
            ? Mathf.Clamp(
                mapleSimulation.BiologicalStructuralGrowthRateMultiplier,
                0.4f,
                1.5f)
            : 1f;
        float developmentalDrive = mapleSimulation != null
            ? Mathf.Lerp(
                0.55f,
                1f,
                Mathf.Clamp01(mapleSimulation.DevelopmentStage / 2f))
            : 1f;
        currentSecondaryGrowthRateMultiplier = orderPriority
            * chronologicalAgePriority
            * trunkPriority
            * biologicalDrive
            * developmentalDrive;
        float progressPerSecond =
            currentSecondaryGrowthRateMultiplier
            / Mathf.Max(5f, secondsToFullSecondaryGrowth);
        // This progress is intentionally not clamped. Length and thickness
        // continue increasing for the complete lifetime of the simulation.
        // Logarithmic shaping keeps growth continuous while preventing a very
        // old tree from exploding in size after only a few extra minutes.
        secondaryGrowthProgress = Mathf.Max(
            0f,
            secondaryGrowthProgress + elapsedGrowthTime * progressPerSecond);
        float unboundedGrowth = Mathf.Log(1f + secondaryGrowthProgress);
        float orderRetention = Mathf.Pow(
            secondaryGrowthRetentionPerOrder,
            currGen);
        float requestedLengthMultiplier = 1f
            + matureLengthIncrease * orderRetention * unboundedGrowth;
        float requestedThicknessMultiplier = 1f
            + matureThicknessIncrease * orderRetention * unboundedGrowth;
        currentMatureLengthMultiplier = Mathf.Max(
            currentMatureLengthMultiplier,
            requestedLengthMultiplier);
        currentMatureThicknessMultiplier = Mathf.Max(
            currentMatureThicknessMultiplier,
            requestedThicknessMultiplier);

        Vector3 scale = modelCyl.localScale;
        float targetScaleY = maxHeight * currentMatureLengthMultiplier;
        float targetScaleX = originalRadiusX
            * cumulativeThicknessScale
            * currentMatureThicknessMultiplier;
        float targetScaleZ = originalRadiusZ
            * cumulativeThicknessScale
            * currentMatureThicknessMultiplier;

        // Never assign a smaller value. Runtime parameter changes therefore
        // cannot make old wood shrink from one frame to the next.
        scale.y = Mathf.Max(scale.y, targetScaleY);
        scale.x = Mathf.Max(scale.x, targetScaleX);
        scale.z = Mathf.Max(scale.z, targetScaleZ);
        modelCyl.localScale = scale;
        modelCyl.localPosition = Vector3.up * scale.y;
    }

    private void LateUpdate()
    {
        UpdateWeightBending();
    }

    public void RestartVisualGrowthPreservingShape()
    {
        if (currGen != 0 || modelCyl == null)
        {
            return;
        }

        var descendants = new List<Branch>();
        for (int index = 0; index < ActiveBranches.Count; index++)
        {
            Branch candidate = ActiveBranches[index];
            if (candidate != null
                && candidate != this
                && candidate.transform.IsChildOf(transform))
            {
                descendants.Add(candidate);
            }
        }

        for (int index = 0; index < descendants.Count; index++)
        {
            Branch descendant = descendants[index];
            descendant.StopAllCoroutines();
            descendant.enabled = false;
        }

        StopAllCoroutines();
        for (int childIndex = transform.childCount - 1;
            childIndex >= 0;
            childIndex--)
        {
            Transform child = transform.GetChild(childIndex);
            if (child != null && child != modelCyl)
            {
                Destroy(child.gameObject);
            }
        }

        PendingBranchGrowth.Remove(this);
        ActiveBranchGrowth.Remove(this);
        queuedChildPlans = null;
        nextLeafIndex = 0;
        nextLeafNodeSupportedSince = -1f;
        leafSpawningComplete = false;
        jointsStarted = false;
        maxed = false;
        currentStructuralStage = 1;
        supportedWeightCacheDirty = true;
        structuralStageReachedAt = -1f;
        currentWeightBendDegrees = 0f;
        targetWeightBendDegrees = 0f;
        nextWeightEvaluationAt = 0f;
        nextArcVisualUpdateAt = 0f;
        nextSecondaryGrowthUpdateAt = 0f;
        lastSecondaryGrowthUpdateAt = -1f;
        currentStemArcDegrees = 0f;
        currentStemArcDirection = Vector3.zero;
        secondaryGrowthProgress = 0f;
        secondaryGrowthAgeStrength = 0f;
        currentSecondaryGrowthRateMultiplier = 1f;
        currentMatureLengthMultiplier = 1f;
        currentMatureThicknessMultiplier = 1f;
        arcAttachments.Clear();
        invalidArcAttachments.Clear();
        cumulativeThicknessScale = 1f;
        transform.localRotation = unloadedLocalRotation;
        modelCyl.localScale = new Vector3(
            originalRadiusX * stageOneThicknessFraction,
            0f,
            originalRadiusZ * stageOneThicknessFraction);
        modelCyl.localPosition = Vector3.zero;
        if (stemModel == null)
        {
            stemModel = modelCyl.GetComponent<MapleStemModel>();
        }

        stemModel?.SetArcBend(0f, Vector3.right);

        if (growthGrammar == null)
        {
            growthGrammar = GetComponent<PlantLSystem>();
        }

        if (growthGrammar != null)
        {
            growthGrammar.RestartPlantPreservingSeed(this);
            plannedLeafBudget = growthGrammar.CreateRootLeafBudget(this);
        }

        runtimeLeafCount = plannedLeafBudget >= 0
            ? plannedLeafBudget
            : Mathf.Max(0, leavesPerStem);
        // Plant.RestartCompleteSimulation resets the DVS clock immediately
        // after this visual reset, so the new visual lifetime starts at zero.
        GrowthStartedAt = 0f;
        init = true;
        readyToGrow = true;
    }

    private void UpdateWeightBending()
    {
        float visualGrowthDeltaTime = GetVisualGrowthDeltaTime();
        if (mapleSimulation != null && visualGrowthDeltaTime <= 0.000001f)
        {
            return;
        }

        if (Time.time >= nextWeightEvaluationAt)
        {
            nextWeightEvaluationAt = Time.time + GetWeightEvaluationInterval();
            EvaluateSupportedWeight();
        }

        currentWeightBendDegrees = Mathf.MoveTowards(
            currentWeightBendDegrees,
            targetWeightBendDegrees,
            weightBendResponse * visualGrowthDeltaTime);
        transform.localRotation = Quaternion.AngleAxis(
            currentWeightBendDegrees,
            weightBendAxis) * unloadedLocalRotation;
        ApplyCircularStemBend();
    }

    private void ApplyCircularStemBend()
    {
        if (modelCyl == null)
        {
            return;
        }

        if (PlantVisualQuality.LiteModeEnabled
            && Time.time < nextArcVisualUpdateAt)
        {
            return;
        }

        nextArcVisualUpdateAt = Time.time
            + (PlantVisualQuality.LiteModeEnabled ? 0.2f : 0f);

        if (stemModel == null)
        {
            stemModel = modelCyl.GetComponent<MapleStemModel>();
        }

        Vector3 localGravity = transform.InverseTransformDirection(
            Vector3.down);
        Vector3 bendDirection = Vector3.ProjectOnPlane(
            localGravity,
            Vector3.up);
        float requestedArcDegrees = Mathf.Min(
            maximumWeightArcDegrees,
            currentWeightBendDegrees * weightArcBendMultiplier);
        if (requestedArcDegrees <= 0.01f
            || bendDirection.sqrMagnitude <= 0.000001f)
        {
            currentStemArcDegrees = 0f;
            currentStemArcDirection = Vector3.zero;
            stemModel?.SetArcBend(0f, Vector3.right);
            UpdateArcAttachments();
            return;
        }

        currentStemArcDegrees = requestedArcDegrees;
        currentStemArcDirection = bendDirection.normalized;
        stemModel?.SetArcBend(
            currentStemArcDegrees,
            currentStemArcDirection);
        UpdateArcAttachments();
    }

    private void RegisterArcAttachment(
        Transform attachment,
        float straightHeight,
        bool followsStemTangent)
    {
        if (attachment == null)
        {
            return;
        }

        float referenceHeight = straightHeight
            / Mathf.Max(1f, currentMatureLengthMultiplier);
        arcAttachments[attachment] = new ArcAttachmentPose(
            referenceHeight,
            attachment.localRotation,
            followsStemTangent);
    }

    private void UpdateArcAttachments()
    {
        if (arcAttachments.Count == 0)
        {
            return;
        }

        float visibleLength = GetVisibleStemLength();
        invalidArcAttachments.Clear();
        foreach (KeyValuePair<Transform, ArcAttachmentPose> entry
            in arcAttachments)
        {
            Transform attachment = entry.Key;
            if (attachment == null)
            {
                invalidArcAttachments.Add(attachment);
                continue;
            }

            ArcAttachmentPose pose = entry.Value;
            float expandedHeight = pose.StraightHeight
                * currentMatureLengthMultiplier;
            attachment.localPosition = GetArcPointAtHeight(
                expandedHeight,
                visibleLength);
            if (pose.FollowsStemTangent
                && currentStemArcDegrees > 0.01f)
            {
                float progress = Mathf.Clamp01(
                    expandedHeight / Mathf.Max(0.0001f, visibleLength));
                Vector3 bendAxis = Vector3.Cross(
                    Vector3.up,
                    currentStemArcDirection).normalized;
                attachment.localRotation = Quaternion.AngleAxis(
                    currentStemArcDegrees * progress,
                    bendAxis) * pose.StraightLocalRotation;
            }
            else if (pose.FollowsStemTangent)
            {
                attachment.localRotation = pose.StraightLocalRotation;
            }
        }

        for (int index = 0; index < invalidArcAttachments.Count; index++)
        {
            arcAttachments.Remove(invalidArcAttachments[index]);
        }
    }

    private Vector3 GetArcPointAtHeight(
        float straightHeight,
        float visibleLength)
    {
        float safeHeight = Mathf.Clamp(straightHeight, 0f, visibleLength);
        if (currentStemArcDegrees <= 0.01f
            || currentStemArcDirection.sqrMagnitude <= 0.000001f)
        {
            return Vector3.up * safeHeight;
        }

        float bendRadians = currentStemArcDegrees * Mathf.Deg2Rad;
        float progress = safeHeight / Mathf.Max(0.0001f, visibleLength);
        float pointAngle = bendRadians * progress;
        float arcRadius = visibleLength / Mathf.Max(0.0001f, bendRadians);
        return Vector3.up * (arcRadius * Mathf.Sin(pointAngle))
            + currentStemArcDirection
                * (arcRadius * (1f - Mathf.Cos(pointAngle)));
    }

    private float GetVisibleStemLength()
    {
        return modelCyl != null
            ? Mathf.Max(0.0001f, Mathf.Abs(modelCyl.localScale.y) * 2f)
            : 0.0001f;
    }

    private void EvaluateSupportedWeight()
    {
        if (modelCyl == null || maximumWeightBendDegrees <= 0f)
        {
            targetWeightBendDegrees = 0f;
            return;
        }

        Vector3 baseDirection = unloadedLocalRotation * Vector3.up;
        Vector3 gravityDirection = transform.parent != null
            ? transform.parent.InverseTransformDirection(Vector3.down)
            : Vector3.down;
        Vector3 downwardDeflection = Vector3.ProjectOnPlane(
            gravityDirection,
            baseDirection);

        // A vertical trunk has no preferred bending plane. Keeping it rigid
        // avoids an arbitrary lean while its oblique branches remain flexible.
        if (downwardDeflection.sqrMagnitude <= 0.00001f)
        {
            targetWeightBendDegrees = 0f;
            return;
        }

        weightBendAxis = Vector3.Cross(
            baseDirection,
            downwardDeflection.normalized).normalized;

        float visibleLength = Mathf.Abs(modelCyl.localScale.y) * 2f;
        float ownGrowth = Mathf.Clamp01(
            visibleLength / Mathf.Max(0.05f, maxHeight * 2f));
        EnsureSupportedWeightCache();
        int supportedLeaves = cachedSupportedLeaves;
        int supportedChildStems = cachedSupportedChildStems;

        float leafLoad = 1f - Mathf.Exp(-supportedLeaves / 5f);
        float childStemLoad = 1f - Mathf.Exp(-supportedChildStems / 2.5f);
        float biomassLoad = mapleSimulation != null
            ? 1f - Mathf.Exp(
                -(mapleSimulation.WoodBiomass + mapleSimulation.LeafBiomass)
                / 9f)
            : ownGrowth;
        float normalizedLoad = Mathf.Clamp01(
            ownGrowth * 0.22f
            + leafLoad * 0.42f
            + childStemLoad * 0.24f
            + biomassLoad * 0.12f);
        float flexibility = currGen == 0
            ? mainTrunkBendMultiplier
            : Mathf.Lerp(
                0.72f,
                1.18f,
                currGen / (float)MaximumBranchGeneration);
        targetWeightBendDegrees = Mathf.Min(
            maximumWeightBendDegrees,
            maximumWeightBendDegrees
                * normalizedLoad
                * flexibility);
    }

    public static void InvalidateSupportedWeightCache()
    {
        supportedWeightCacheDirty = true;
    }

    private static void EnsureSupportedWeightCache()
    {
        float refreshInterval = PlantVisualQuality.LiteModeEnabled
            ? 0.65f
            : 0.18f;
        if (!supportedWeightCacheDirty
            && Time.time < nextSupportedWeightCacheRefreshAt)
        {
            return;
        }

        supportedWeightCacheDirty = false;
        nextSupportedWeightCacheRefreshAt = Time.time + refreshInterval;

        for (int index = ActiveBranches.Count - 1; index >= 0; index--)
        {
            Branch branch = ActiveBranches[index];
            if (branch == null)
            {
                ActiveBranches.RemoveAt(index);
                continue;
            }

            branch.cachedSupportedLeaves = 0;
            branch.cachedSupportedChildStems = 0;
        }

        for (int index = 0; index < ActiveBranches.Count; index++)
        {
            Branch branch = ActiveBranches[index];
            if (branch == null || !branch.isActiveAndEnabled)
            {
                continue;
            }

            int directLeaves = branch.CountDirectLivingLeaves();
            Branch supportedAncestor = branch;
            bool isSourceStem = true;
            while (supportedAncestor != null)
            {
                supportedAncestor.cachedSupportedLeaves += directLeaves;
                if (!isSourceStem)
                {
                    supportedAncestor.cachedSupportedChildStems++;
                }

                isSourceStem = false;
                supportedAncestor = supportedAncestor.supportingStem;
            }
        }
    }

    private static float GetWeightEvaluationInterval()
    {
        return PlantVisualQuality.LiteModeEnabled ? 0.65f : 0.18f;
    }

    private int CountDirectLivingLeaves()
    {
        int count = 0;
        for (int childIndex = 0;
            childIndex < transform.childCount;
            childIndex++)
        {
            Transform child = transform.GetChild(childIndex);
            if (child != null
                && child.gameObject.activeInHierarchy
                && child.GetComponent<Leaf>() != null)
            {
                count++;
            }
        }

        return count;
    }

    private float GetStructuralLengthFraction()
    {
        if (currentStructuralStage <= 1)
        {
            return stageOneLengthFraction;
        }

        if (currentStructuralStage == 2)
        {
            return stageTwoLengthFraction;
        }

        return currentStructuralStage == 3
            ? stageThreeLengthFraction
            : 1f;
    }

    private float GetStructuralThicknessFraction()
    {
        if (currentStructuralStage <= 1)
        {
            return stageOneThicknessFraction;
        }

        if (currentStructuralStage == 2)
        {
            return stageTwoThicknessFraction;
        }

        return currentStructuralStage == 3
            ? stageThreeThicknessFraction
            : 1f;
    }

    public void InitializeFromParent(
        Branch parent,
        PlantLSystem.StemPlan plan)
    {
        supportingStem = parent;
        jointPrefab = parent.jointPrefab;
        currGen = plan.Order;
        maxGen = Mathf.Min(
            parent.maxGen,
            plan.MaximumOrder,
            MaximumBranchGeneration);
        firstOrderBranchesMin = parent.firstOrderBranchesMin;
        firstOrderBranchesMax = parent.firstOrderBranchesMax;
        secondOrderBranchesMin = parent.secondOrderBranchesMin;
        secondOrderBranchesMax = parent.secondOrderBranchesMax;
        thirdOrderBranchesMin = parent.thirdOrderBranchesMin;
        thirdOrderBranchesMax = parent.thirdOrderBranchesMax;
        fourthOrderBranchesMin = parent.fourthOrderBranchesMin;
        fourthOrderBranchesMax = parent.fourthOrderBranchesMax;
        fifthOrderBranchesMin = parent.fifthOrderBranchesMin;
        fifthOrderBranchesMax = parent.fifthOrderBranchesMax;
        lowerChildBranchAngle = parent.lowerChildBranchAngle;
        upperChildBranchAngle = parent.upperChildBranchAngle;
        trunkBranchAttachmentStartFraction =
            parent.trunkBranchAttachmentStartFraction;
        trunkBranchAttachmentEndFraction =
            parent.trunkBranchAttachmentEndFraction;
        childStemAttachmentStartFraction = parent.childStemAttachmentStartFraction;
        childStemAttachmentEndFraction = parent.childStemAttachmentEndFraction;
        childLengthMultiplier = parent.childLengthMultiplier;
        primaryBranchLengthBoost = parent.primaryBranchLengthBoost;
        childThicknessMultiplier = parent.childThicknessMultiplier;
        crownFillerBranchesMin = parent.crownFillerBranchesMin;
        crownFillerBranchesMax = parent.crownFillerBranchesMax;
        crownFillerAttachmentStartFraction =
            parent.crownFillerAttachmentStartFraction;
        crownFillerAttachmentEndFraction =
            parent.crownFillerAttachmentEndFraction;
        crownFillerLowerLengthMultiplier =
            parent.crownFillerLowerLengthMultiplier;
        crownFillerUpperLengthMultiplier =
            parent.crownFillerUpperLengthMultiplier;
        crownFillerLowerTiltDegrees = parent.crownFillerLowerTiltDegrees;
        crownFillerUpperTiltDegrees = parent.crownFillerUpperTiltDegrees;
        crownFillerThicknessMultiplier =
            parent.crownFillerThicknessMultiplier;
        crownFillerDescendantGenerations =
            parent.crownFillerDescendantGenerations;
        if (plan.IsCrownFiller)
        {
            // The small apical system must fill the centre of the canopy,
            // not start a second wide crown. Its descendants attach nearer
            // the base and lose length and thickness faster at every order.
            childStemAttachmentStartFraction = Mathf.Min(
                childStemAttachmentStartFraction,
                0.45f);
            childStemAttachmentEndFraction = Mathf.Min(
                childStemAttachmentEndFraction,
                0.78f);
            childLengthMultiplier = Mathf.Min(
                childLengthMultiplier,
                0.66f);
            childThicknessMultiplier = Mathf.Min(
                childThicknessMultiplier,
                0.62f);
        }

        freeSpaceSquareThicknessMultiplier = parent.freeSpaceSquareThicknessMultiplier;
        randomGrowthZoneRadiusInStemThicknesses =
            parent.randomGrowthZoneRadiusInStemThicknesses;
        growthDirectionCandidateCount = parent.growthDirectionCandidateCount;
        minimumOutwardDivergence = parent.minimumOutwardDivergence;
        canopyBalanceInfluence = parent.canopyBalanceInfluence;
        minimumHigherOrderClearanceInStemThicknesses =
            parent.minimumHigherOrderClearanceInStemThicknesses;
        maximumWeightBendDegrees = parent.maximumWeightBendDegrees;
        mainTrunkBendMultiplier = parent.mainTrunkBendMultiplier;
        weightBendResponse = parent.weightBendResponse;
        weightArcBendMultiplier = parent.weightArcBendMultiplier;
        maximumWeightArcDegrees = parent.maximumWeightArcDegrees;
        delayBranches = parent.delayBranches;
        vitezaCrestere = parent.vitezaCrestere;
        maxHeight = Mathf.Max(
            0.05f,
            parent.maxHeight * plan.LengthMultiplier);
        leavesPerStem = parent.leavesPerStem;
        minimumLeavesPerStem = parent.minimumLeavesPerStem;
        firstLeafHeightFraction = parent.firstLeafHeightFraction;
        lastLeafHeightFraction = parent.lastLeafHeightFraction;
        minimumLeafNodeSpacing = parent.minimumLeafNodeSpacing;
        leafLengthSpacingMultiplier = parent.leafLengthSpacingMultiplier;
        stemRadiusLeafSpacingMultiplier = parent.stemRadiusLeafSpacingMultiplier;
        leafLength = parent.leafLength;
        leafWidthToLength = parent.leafWidthToLength;
        leafGrowthDuration = parent.leafGrowthDuration;
        leafStemAdvanceMargin = parent.leafStemAdvanceMargin;
        leafNodeMaturationDelay = parent.leafNodeMaturationDelay;
        stageOneLengthFraction = parent.stageOneLengthFraction;
        stageTwoLengthFraction = parent.stageTwoLengthFraction;
        stageThreeLengthFraction = parent.stageThreeLengthFraction;
        stageOneThicknessFraction = parent.stageOneThicknessFraction;
        stageTwoThicknessFraction = parent.stageTwoThicknessFraction;
        stageThreeThicknessFraction = parent.stageThreeThicknessFraction;
        secondsBetweenStructuralStages = parent.secondsBetweenStructuralStages;
        matureLengthIncrease = parent.matureLengthIncrease;
        matureThicknessIncrease = parent.matureThicknessIncrease;
        secondaryGrowthRetentionPerOrder =
            parent.secondaryGrowthRetentionPerOrder;
        secondsToFullSecondaryGrowth = parent.secondsToFullSecondaryGrowth;
        oldStemGrowthAcceleration = parent.oldStemGrowthAcceleration;
        trunkSecondaryGrowthPriority =
            parent.trunkSecondaryGrowthPriority;
        stemColor = parent.stemColor;
        leafColor = parent.leafColor;
        mainStem = parent.mainStem != null ? parent.mainStem : parent;
        growthGrammar = parent.growthGrammar;
        mapleSimulation = parent.mapleSimulation;
        plannedLeafBudget = plan.LeafBudget;
        cumulativeThicknessScale = parent.cumulativeThicknessScale
            * plan.ThicknessMultiplier;
        GrowthStartedAt = GetVisualGrowthClock();
        maxed = false;
        jointsStarted = false;
        currentStructuralStage = 1;
        supportedWeightCacheDirty = true;
        structuralStageReachedAt = -1f;
        secondaryGrowthProgress = 0f;
        secondaryGrowthAgeStrength = 0f;
        currentSecondaryGrowthRateMultiplier = 1f;
        currentMatureLengthMultiplier = 1f;
        currentMatureThicknessMultiplier = 1f;
        init = true;
        readyToGrow = true;
    }

    private void QueueChildGrowth()
    {
        if (!PendingBranchGrowth.Contains(this)
            && !ActiveBranchGrowth.Contains(this))
        {
            PendingBranchGrowth.Add(this);
        }
    }

    private static void TryStartQueuedGrowth()
    {
        if (lastGrowthSchedulerFrame == Time.frameCount)
        {
            return;
        }

        lastGrowthSchedulerFrame = Time.frameCount;
        RemoveInvalidGrowthEntries(PendingBranchGrowth);
        RemoveInvalidGrowthEntries(ActiveBranchGrowth);

        if (PendingBranchGrowth.Count == 0)
        {
            return;
        }

        // Any active lower-order parent blocks all higher orders. Parents of
        // the same order may branch together, so the plant develops in broad
        // structural layers without becoming artificially serial and slow.
        int priorityGeneration = int.MaxValue;
        for (int index = 0; index < ActiveBranchGrowth.Count; index++)
        {
            priorityGeneration = Mathf.Min(
                priorityGeneration,
                ActiveBranchGrowth[index].currGen);
        }

        for (int index = 0; index < PendingBranchGrowth.Count; index++)
        {
            priorityGeneration = Mathf.Min(
                priorityGeneration,
                PendingBranchGrowth[index].currGen);
        }

        for (int index = PendingBranchGrowth.Count - 1; index >= 0; index--)
        {
            Branch candidate = PendingBranchGrowth[index];
            if (candidate.currGen != priorityGeneration)
            {
                continue;
            }

            if (candidate.mapleSimulation != null
                && !candidate.mapleSimulation.CanAdvanceVisualGrowth)
            {
                continue;
            }

            PendingBranchGrowth.RemoveAt(index);
            ActiveBranchGrowth.Add(candidate);
            candidate.StartCoroutine(candidate.RunQueuedChildGrowth());
        }
    }

    private static void RemoveInvalidGrowthEntries(List<Branch> entries)
    {
        for (int index = entries.Count - 1; index >= 0; index--)
        {
            Branch entry = entries[index];
            if (entry == null || !entry.isActiveAndEnabled)
            {
                entries.RemoveAt(index);
            }
        }
    }

    private IEnumerator RunQueuedChildGrowth()
    {
        yield return GenJoints(queuedChildPlans);
        queuedChildPlans = null;
        ActiveBranchGrowth.Remove(this);
        TryStartQueuedGrowth();
    }

    private IEnumerator GenJoints(PlantLSystem.StemPlan[] plans)
    {
        if (jointPrefab == null || plans == null || plans.Length == 0)
        {
            yield break;
        }

        float realHeight = Mathf.Max(0.05f, modelCyl.localScale.y * 2f);

        for (int index = 0; index < plans.Length; index++)
        {
            PlantLSystem.StemPlan plan = plans[index];
            float jointHeight = realHeight * plan.AttachmentFraction;

            if (!TrySelectGrowthRotation(
                jointHeight,
                plan.TiltDegrees,
                plan.PreferredAzimuth,
                plan.LengthMultiplier,
                plan.ThicknessMultiplier,
                plan.Order > 1,
                out Quaternion selectedRotation))
            {
                continue;
            }

            GameObject newJoint = Instantiate(jointPrefab, transform);
            newJoint.transform.localPosition = Vector3.up * jointHeight;
            // The L-system supplies the preferred direction and proportions.
            // Spatial planning may rotate that direction to keep the predicted
            // path free and the complete canopy balanced.
            newJoint.transform.localRotation = selectedRotation;
            string stemRole = plan.IsCrownFiller
                ? "Crown Fill"
                : "Stem";
            newJoint.name =
                $"{stemRole} Joint G{plan.Order}-{index + 1}";
            RegisterArcAttachment(
                newJoint.transform,
                jointHeight,
                true);

            // Never instantiate the recursive Branch prefab itself here. A
            // self-reference inside a Unity prefab is remapped to the runtime
            // instance, so cloning it would also clone every stem and leaf it
            // has already generated. Build one clean stem instead.
            var newBranch = new GameObject(
                $"{stemRole} G{plan.Order}-{index + 1}");
            newBranch.transform.SetParent(newJoint.transform, false);
            newBranch.transform.localPosition = Vector3.zero;
            newBranch.transform.localRotation = Quaternion.identity;

            GameObject newCylinder = Instantiate(modelCyl.gameObject, newBranch.transform);
            newCylinder.name = "Stem Cylinder";
            newCylinder.transform.localPosition = Vector3.zero;
            newCylinder.transform.localRotation = Quaternion.identity;
            newCylinder.transform.localScale = new Vector3(
                originalRadiusX,
                0f,
                originalRadiusZ);

            Branch newScript = newBranch.AddComponent<Branch>();
            newScript.modelCyl = newCylinder.transform;
            newScript.InitializeFromParent(this, plan);

            // Preserve the original staged rhythm: the next sibling stem is
            // not created until the current child finishes growing.
            //while (newScript.isActiveAndEnabled && !newScript.maxed)
            //{
            //    yield return null;
            //}

            if (delayBranches > 0f)
            {
                float elapsedDelay = 0f;
                while (elapsedDelay < delayBranches)
                {
                    elapsedDelay += GetVisualGrowthDeltaTime();
                    yield return null;
                }
            }
        }
    }

    private bool TrySelectGrowthRotation(
        float jointHeight,
        float branchTilt,
        float preferredAzimuth,
        float plannedLengthMultiplier,
        float plannedThicknessMultiplier,
        bool requireOpenSpace,
        out Quaternion selectedRotation)
    {
        Branch root = mainStem != null ? mainStem : this;
        Vector3 mainAxisOrigin = root.transform.position;
        Vector3 mainAxisUp = root.transform.up.normalized;
        Vector3 jointWorldPosition = transform.TransformPoint(
            Vector3.up * jointHeight);

        if (requireOpenSpace)
        {
            bool isShaded = IsLightRayBlockedByStemScreen(jointWorldPosition, Vector3.up, 1.5f, this, 1.2f);
            if (isShaded)
            {
                selectedRotation = Quaternion.identity;
                return false;
            }
        }

        float seedAzimuth = preferredAzimuth;
        Vector3 radialOutward = Vector3.ProjectOnPlane(
            jointWorldPosition - mainAxisOrigin,
            mainAxisUp);

        if (radialOutward.sqrMagnitude <= 0.000001f)
        {
            Quaternion seedRotation = Quaternion.AngleAxis(
                seedAzimuth,
                Vector3.up)
                * Quaternion.AngleAxis(branchTilt, Vector3.right);
            radialOutward = Vector3.ProjectOnPlane(
                transform.TransformDirection(seedRotation * Vector3.up),
                mainAxisUp);
        }

        if (radialOutward.sqrMagnitude <= 0.000001f)
        {
            radialOutward = Vector3.ProjectOnPlane(
                transform.forward,
                mainAxisUp);
        }

        radialOutward.Normalize();

        float childLength = Mathf.Max(
            0.05f,
            maxHeight * plannedLengthMultiplier * 2f);
        float childThickness = Mathf.Max(
            0.01f,
            GetStemRadius() * 2f * plannedThicknessMultiplier);
        float squareSide = childThickness
            * freeSpaceSquareThicknessMultiplier;
        CanopyBalanceState balanceState = CalculateCanopyBalanceState(
            mainAxisOrigin,
            mainAxisUp);
        LastCanopySideImbalance = balanceState.NormalizedSideImbalance;
        LastHeavierCanopyDirection = balanceState.HeavierSideDirection;

        Quaternion bestRotation = Quaternion.AngleAxis(
            seedAzimuth,
            Vector3.up)
            * Quaternion.AngleAxis(branchTilt, Vector3.right);
        Vector3 bestDirection = transform.TransformDirection(
            bestRotation * Vector3.up).normalized;
        Vector3 bestEnd = jointWorldPosition + bestDirection * childLength;
        float bestScore = float.PositiveInfinity;
        float bestClearance = 0f;
        float bestFreeSpacePenalty = float.PositiveInfinity;
        float bestBalancePenalty = float.PositiveInfinity;
        float bestDirectionPenalty = float.PositiveInfinity;
        int candidateCount = Mathf.Max(8, growthDirectionCandidateCount);

        for (int candidateIndex = 0;
            candidateIndex < candidateCount;
            candidateIndex++)
        {
            float azimuth = seedAzimuth
                + candidateIndex * (360f / candidateCount);
            Quaternion candidateRotation = Quaternion.AngleAxis(
                azimuth,
                Vector3.up)
                * Quaternion.AngleAxis(branchTilt, Vector3.right);
            Vector3 worldDirection = transform.TransformDirection(
                candidateRotation * Vector3.up).normalized;
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(
                worldDirection,
                mainAxisUp);

            if (horizontalDirection.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            horizontalDirection.Normalize();
            float divergence = Vector3.Dot(
                horizontalDirection,
                radialOutward);
            float upwardGrowth = Vector3.Dot(worldDirection, mainAxisUp);
            float directionPenalty = 0f;
            if (divergence < minimumOutwardDivergence)
            {
                directionPenalty += 10000f
                    + (minimumOutwardDivergence - divergence) * 1000f;
            }

            if (upwardGrowth <= 0.05f)
            {
                directionPenalty += 10000f
                    + (0.05f - upwardGrowth) * 1000f;
            }

            Vector3 candidateEnd = jointWorldPosition
                + worldDirection * childLength;
            float freeSpacePenalty = EvaluateFreeSpacePenalty(
                jointWorldPosition,
                candidateEnd,
                worldDirection,
                mainAxisUp,
                squareSide);
            float distanceFromMainAxis = DistanceFromAxis(
                candidateEnd,
                mainAxisOrigin,
                mainAxisUp);
            float balancePenalty = CalculateCanopyBalancePenalty(
                candidateEnd,
                childLength,
                childThickness,
                mainAxisOrigin,
                mainAxisUp,
                balanceState);
            float clearance = CalculateMinimumBranchClearance(
                jointWorldPosition,
                candidateEnd,
                childThickness * 0.5f);
            float normalizedClearance = Mathf.Clamp(
                clearance / childLength,
                0f,
                2f);
            float balanceUrgency = 1f
                + balanceState.NormalizedSideImbalance * 2f;

            float score = directionPenalty
                + freeSpacePenalty * 1000f
                + balancePenalty
                    * canopyBalanceInfluence
                    * balanceUrgency
                + distanceFromMainAxis
                    / childLength
                    * 0.25f
                - normalizedClearance * 8f
                - divergence * 0.1f
                - upwardGrowth * 0.05f;

            if (score < bestScore)
            {
                bestScore = score;
                bestRotation = candidateRotation;
                bestDirection = worldDirection;
                bestEnd = candidateEnd;
                bestClearance = clearance;
                bestFreeSpacePenalty = freeSpacePenalty;
                bestBalancePenalty = balancePenalty;
                bestDirectionPenalty = directionPenalty;
            }
        }

        LastOptimalClearance = bestClearance;
        LastSelectedClearance = bestClearance;
        LastGrowthUsedRandomZone = false;
        LastRandomGrowthZoneRadius = childThickness
            * randomGrowthZoneRadiusInStemThicknesses;

        float requiredClearance = childThickness
            * minimumHigherOrderClearanceInStemThicknesses;
        bool hasOpenGrowthPath =
            !float.IsPositiveInfinity(bestScore)
            && bestDirectionPenalty <= 0.001f
            && bestFreeSpacePenalty <= 0.001f
            && bestClearance >= requiredClearance;
        if (requireOpenSpace && !hasOpenGrowthPath)
        {
            selectedRotation = bestRotation;
            return false;
        }

        selectedRotation = SelectRandomDirectionAroundOptimalTarget(
            bestRotation,
            bestEnd,
            bestDirection,
            jointWorldPosition,
            childLength,
            childThickness,
            squareSide,
            radialOutward,
            mainAxisOrigin,
            mainAxisUp,
            balanceState,
            bestClearance,
            bestFreeSpacePenalty,
            bestBalancePenalty);
        return true;
    }

    private Quaternion SelectRandomDirectionAroundOptimalTarget(
        Quaternion optimalRotation,
        Vector3 optimalEnd,
        Vector3 optimalDirection,
        Vector3 branchStart,
        float branchLength,
        float branchThickness,
        float freeSpaceSquareSide,
        Vector3 radialOutward,
        Vector3 mainAxisOrigin,
        Vector3 mainAxisUp,
        CanopyBalanceState balanceState,
        float optimalClearance,
        float optimalFreeSpacePenalty,
        float optimalBalancePenalty)
    {
        float zoneRadius = LastRandomGrowthZoneRadius;
        if (zoneRadius <= 0.0001f)
        {
            return optimalRotation;
        }

        for (int attempt = 0; attempt < RandomGrowthZoneSamples; attempt++)
        {
            Vector3 randomOffset = growthGrammar != null
                ? growthGrammar.SampleInsideUnitSphere(this)
                : Random.insideUnitSphere;
            Vector3 target = optimalEnd + randomOffset * zoneRadius;
            Vector3 candidateDirection = target - branchStart;
            if (candidateDirection.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            candidateDirection.Normalize();
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(
                candidateDirection,
                mainAxisUp);
            if (horizontalDirection.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            horizontalDirection.Normalize();
            float divergence = Vector3.Dot(
                horizontalDirection,
                radialOutward);
            float upwardGrowth = Vector3.Dot(
                candidateDirection,
                mainAxisUp);
            if (divergence < minimumOutwardDivergence
                || upwardGrowth <= 0.05f)
            {
                continue;
            }

            Vector3 candidateEnd = branchStart
                + candidateDirection * branchLength;
            float freeSpacePenalty = EvaluateFreeSpacePenalty(
                branchStart,
                candidateEnd,
                candidateDirection,
                mainAxisUp,
                freeSpaceSquareSide);
            float clearance = CalculateMinimumBranchClearance(
                branchStart,
                candidateEnd,
                branchThickness * 0.5f);
            float balancePenalty = CalculateCanopyBalancePenalty(
                candidateEnd,
                branchLength,
                branchThickness,
                mainAxisOrigin,
                mainAxisUp,
                balanceState);

            // The final point is random, but it must stay inside the same safe
            // free-space basin and may not noticeably worsen canopy balance.
            if (freeSpacePenalty > optimalFreeSpacePenalty + 0.001f
                || clearance + zoneRadius < optimalClearance
                || balancePenalty > optimalBalancePenalty + 0.06f)
            {
                continue;
            }

            LastSelectedClearance = clearance;
            LastGrowthUsedRandomZone = true;
            Vector3 localDirection = transform.InverseTransformDirection(
                candidateDirection).normalized;
            return Quaternion.FromToRotation(Vector3.up, localDirection);
        }

        // No safe randomized target was found. The exact optimum is always a
        // better fallback than accepting a collision or worsening imbalance.
        LastSelectedClearance = CalculateMinimumBranchClearance(
            branchStart,
            branchStart + optimalDirection * branchLength,
            branchThickness * 0.5f);
        return optimalRotation;
    }

    private float EvaluateFreeSpacePenalty(
        Vector3 candidateStart,
        Vector3 candidateEnd,
        Vector3 candidateDirection,
        Vector3 mainAxisUp,
        float squareSide)
    {
        float halfSide = Mathf.Max(0.005f, squareSide * 0.5f);
        float candidateLength = Vector3.Distance(
            candidateStart,
            candidateEnd);
        float longitudinalTolerance = candidateLength
            / (FreeSpacePathSamples * 2f);

        Vector3 squareRight = Vector3.Cross(
            candidateDirection,
            mainAxisUp);
        if (squareRight.sqrMagnitude <= 0.000001f)
        {
            squareRight = Vector3.Cross(
                candidateDirection,
                Vector3.forward);
        }

        squareRight.Normalize();
        Vector3 squareUp = Vector3.Cross(
            squareRight,
            candidateDirection).normalized;
        float penalty = 0f;
        float corridorRadius = Mathf.Sqrt(
            halfSide * halfSide * 2f
            + longitudinalTolerance * longitudinalTolerance);

        for (int branchIndex = ActiveBranches.Count - 1;
            branchIndex >= 0;
            branchIndex--)
        {
            Branch other = ActiveBranches[branchIndex];
            if (other == null)
            {
                ActiveBranches.RemoveAt(branchIndex);
                continue;
            }

            if (other == this || other.modelCyl == null)
            {
                continue;
            }

            other.GetGrowthPlanningSegment(
                out Vector3 otherStart,
                out Vector3 otherEnd);
            if ((otherEnd - otherStart).sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            if (!SegmentBoundingSpheresOverlap(
                candidateStart,
                candidateEnd,
                corridorRadius,
                otherStart,
                otherEnd,
                0f))
            {
                continue;
            }

            for (int sampleIndex = 1;
                sampleIndex <= FreeSpacePathSamples;
                sampleIndex++)
            {
                // Ignore the shared attachment cap and inspect the path from
                // just beyond it to the predicted tip.
                float progress = Mathf.Lerp(
                    0.18f,
                    1f,
                    sampleIndex / (float)FreeSpacePathSamples);
                Vector3 samplePosition = Vector3.Lerp(
                    candidateStart,
                    candidateEnd,
                    progress);
                Vector3 closestOtherPoint = ClosestPointOnSegment(
                    samplePosition,
                    otherStart,
                    otherEnd);
                Vector3 offset = closestOtherPoint - samplePosition;
                float rightDistance = Mathf.Abs(Vector3.Dot(
                    offset,
                    squareRight));
                float upDistance = Mathf.Abs(Vector3.Dot(
                    offset,
                    squareUp));
                float longitudinalDistance = Mathf.Abs(Vector3.Dot(
                    offset,
                    candidateDirection));

                if (rightDistance > halfSide
                    || upDistance > halfSide
                    || longitudinalDistance > longitudinalTolerance)
                {
                    continue;
                }

                float squareOccupancy = 1f - Mathf.Clamp01(Mathf.Max(
                    rightDistance,
                    upDistance) / halfSide);
                penalty += 1f + squareOccupancy;
                break;
            }
        }

        return penalty;
    }

    private float CalculateMinimumBranchClearance(
        Vector3 candidateStart,
        Vector3 candidateEnd,
        float candidateRadius)
    {
        Vector3 inspectedStart = Vector3.Lerp(
            candidateStart,
            candidateEnd,
            0.18f);
        float candidateLength = Vector3.Distance(
            inspectedStart,
            candidateEnd);
        float minimumClearance = candidateLength;

        for (int index = ActiveBranches.Count - 1; index >= 0; index--)
        {
            Branch other = ActiveBranches[index];
            if (other == null)
            {
                ActiveBranches.RemoveAt(index);
                continue;
            }

            if (other == this || other.modelCyl == null)
            {
                continue;
            }

            other.GetGrowthPlanningSegment(
                out Vector3 otherStart,
                out Vector3 otherEnd);
            if ((otherEnd - otherStart).sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            float otherRadius = other.GetWorldStemRadius();
            if (!SegmentBoundingSpheresOverlap(
                inspectedStart,
                candidateEnd,
                candidateRadius + minimumClearance,
                otherStart,
                otherEnd,
                otherRadius))
            {
                continue;
            }

            float centerLineDistance = Mathf.Sqrt(
                SegmentSegmentDistanceSquared(
                    inspectedStart,
                    candidateEnd,
                    otherStart,
                    otherEnd));
            float surfaceClearance = centerLineDistance
                - Mathf.Max(0.001f, candidateRadius)
                - otherRadius;
            minimumClearance = Mathf.Min(
                minimumClearance,
                surfaceClearance);
        }

        return Mathf.Max(0f, minimumClearance);
    }

    private CanopyBalanceState CalculateCanopyBalanceState(
        Vector3 mainAxisOrigin,
        Vector3 mainAxisUp)
    {
        Vector3 weightedMoment = Vector3.zero;
        float totalWeight = 0f;

        for (int index = ActiveBranches.Count - 1; index >= 0; index--)
        {
            Branch branch = ActiveBranches[index];
            if (branch == null)
            {
                ActiveBranches.RemoveAt(index);
                continue;
            }

            if (!TryGetCanopyContribution(
                branch,
                mainAxisOrigin,
                mainAxisUp,
                out Vector3 horizontalOffset,
                out float weight))
            {
                continue;
            }

            weightedMoment += horizontalOffset * weight;
            totalWeight += weight;
        }

        Vector3 heavierSideDirection = weightedMoment.sqrMagnitude
                > 0.000001f
            ? weightedMoment.normalized
            : Vector3.zero;
        float heavierSideWeight = 0f;
        float lighterSideWeight = 0f;

        if (heavierSideDirection.sqrMagnitude > 0.000001f)
        {
            for (int index = 0; index < ActiveBranches.Count; index++)
            {
                Branch branch = ActiveBranches[index];
                if (!TryGetCanopyContribution(
                    branch,
                    mainAxisOrigin,
                    mainAxisUp,
                    out Vector3 horizontalOffset,
                    out float weight))
                {
                    continue;
                }

                float sideProjection = Vector3.Dot(
                    horizontalOffset,
                    heavierSideDirection);
                float centerTolerance = branch.GetWorldStemRadius() * 0.5f;
                if (Mathf.Abs(sideProjection) <= centerTolerance)
                {
                    continue;
                }

                if (sideProjection > 0f)
                {
                    heavierSideWeight += weight;
                }
                else
                {
                    lighterSideWeight += weight;
                }
            }
        }

        float sideWeight = heavierSideWeight + lighterSideWeight;
        float normalizedSideImbalance = sideWeight > 0.000001f
            ? Mathf.Abs(heavierSideWeight - lighterSideWeight) / sideWeight
            : 0f;
        return new CanopyBalanceState(
            weightedMoment,
            totalWeight,
            heavierSideDirection,
            heavierSideWeight,
            lighterSideWeight,
            normalizedSideImbalance);
    }

    private float CalculateCanopyBalancePenalty(
        Vector3 candidateEnd,
        float candidateLength,
        float candidateThickness,
        Vector3 mainAxisOrigin,
        Vector3 mainAxisUp,
        CanopyBalanceState balanceState)
    {
        float candidateWeight = Mathf.Max(
            0.000001f,
            candidateLength * candidateThickness * candidateThickness);
        Vector3 candidateOffset = Vector3.ProjectOnPlane(
            candidateEnd - mainAxisOrigin,
            mainAxisUp);
        Vector3 predictedMoment = balanceState.WeightedMoment
            + candidateOffset * candidateWeight;
        float predictedTotalWeight = balanceState.TotalWeight
            + candidateWeight;
        float momentPenalty = predictedTotalWeight > 0.000001f
            ? predictedMoment.magnitude
                / (predictedTotalWeight * Mathf.Max(0.05f, candidateLength))
            : 0f;

        if (balanceState.HeavierSideDirection.sqrMagnitude <= 0.000001f)
        {
            return momentPenalty;
        }

        float heavierSideWeight = balanceState.HeavierSideWeight;
        float lighterSideWeight = balanceState.LighterSideWeight;
        float candidateSide = Vector3.Dot(
            candidateOffset,
            balanceState.HeavierSideDirection);
        if (candidateSide > 0f)
        {
            heavierSideWeight += candidateWeight;
        }
        else
        {
            lighterSideWeight += candidateWeight;
        }

        float predictedSideWeight = heavierSideWeight + lighterSideWeight;
        float sidePenalty = predictedSideWeight > 0.000001f
            ? Mathf.Abs(heavierSideWeight - lighterSideWeight)
                / predictedSideWeight
            : 0f;
        return sidePenalty + momentPenalty;
    }

    private static bool TryGetCanopyContribution(
        Branch branch,
        Vector3 mainAxisOrigin,
        Vector3 mainAxisUp,
        out Vector3 horizontalOffset,
        out float weight)
    {
        horizontalOffset = Vector3.zero;
        weight = 0f;
        if (branch == null || branch.modelCyl == null)
        {
            return false;
        }

        branch.GetGrowthPlanningSegment(
            out Vector3 bottom,
            out Vector3 tip);
        float length = Vector3.Distance(bottom, tip);
        if (length <= 0.0001f)
        {
            return false;
        }

        float thickness = branch.GetWorldStemRadius() * 2f;
        // Cylinder volume without the common PI/4 factor is sufficient as a
        // physical weight proxy: length times squared thickness.
        weight = Mathf.Max(
            0.000001f,
            length * thickness * thickness);
        Vector3 center = (bottom + tip) * 0.5f;
        horizontalOffset = Vector3.ProjectOnPlane(
            center - mainAxisOrigin,
            mainAxisUp);
        return true;
    }

    private void GetGrowthPlanningSegment(
        out Vector3 stemBottom,
        out Vector3 stemTip)
    {
        stemBottom = transform.position;

        // Reserve the complete future path while the stem is growing. This
        // lets same-order stems that start together avoid one another before
        // their cylinders have visibly reached those regions.
        if (readyToGrow && !maxed)
        {
            stemTip = transform.TransformPoint(
                Vector3.up * (maxHeight * 2f));
            return;
        }

        stemTip = transform.TransformPoint(GetArcPointAtHeight(
            GetVisibleStemLength(),
            GetVisibleStemLength()));
    }

    private static Vector3 ClosestPointOnSegment(
        Vector3 point,
        Vector3 segmentStart,
        Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.000001f)
        {
            return segmentStart;
        }

        float progress = Mathf.Clamp01(Vector3.Dot(
            point - segmentStart,
            segment) / lengthSquared);
        return segmentStart + segment * progress;
    }

    private static float DistanceFromAxis(
        Vector3 point,
        Vector3 axisOrigin,
        Vector3 axisDirection)
    {
        Vector3 offset = point - axisOrigin;
        Vector3 closestPoint = axisOrigin
            + axisDirection * Vector3.Dot(offset, axisDirection);
        return Vector3.Distance(point, closestPoint);
    }

    public static bool IsLightRayBlockedByStemScreen(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        float rayDistance,
        Branch ignoredSupportingStem,
        float screenWidthInStemThicknesses)
    {
        if (rayDistance <= 0.0001f
            || rayDirection.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        Vector3 rayEnd = rayOrigin
            + rayDirection.normalized * rayDistance;
        float safeScreenMultiplier = Mathf.Max(
            1f,
            screenWidthInStemThicknesses);

        for (int index = ActiveBranches.Count - 1; index >= 0; index--)
        {
            Branch branch = ActiveBranches[index];
            if (branch == null)
            {
                ActiveBranches.RemoveAt(index);
                continue;
            }

            if (branch == ignoredSupportingStem || branch.modelCyl == null)
            {
                continue;
            }

            branch.GetVisibleStemWorldSegment(
                out Vector3 stemBottom,
                out Vector3 stemTip);
            if ((stemTip - stemBottom).sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            // Width = multiplier * stem thickness, therefore the virtual
            // capsule radius is multiplier * the visible stem radius.
            float screenRadius = branch.GetWorldStemRadius()
                * safeScreenMultiplier;
            if (!SegmentBoundingSpheresOverlap(
                rayOrigin,
                rayEnd,
                0f,
                stemBottom,
                stemTip,
                screenRadius))
            {
                continue;
            }

            float distanceSquared = SegmentSegmentDistanceSquared(
                rayOrigin,
                rayEnd,
                stemBottom,
                stemTip);
            if (distanceSquared <= screenRadius * screenRadius)
            {
                return true;
            }
        }

        return false;
    }

    private void GetVisibleStemWorldSegment(
        out Vector3 stemBottom,
        out Vector3 stemTip)
    {
        stemBottom = transform.position;
        float visibleLength = GetVisibleStemLength();
        stemTip = transform.TransformPoint(GetArcPointAtHeight(
            visibleLength,
            visibleLength));
    }

    private float GetWorldStemRadius()
    {
        if (modelCyl == null)
        {
            return 0.001f;
        }

        float radiusX = modelCyl.TransformVector(Vector3.right).magnitude * 0.5f;
        float radiusZ = modelCyl.TransformVector(Vector3.forward).magnitude * 0.5f;
        return Mathf.Max(0.001f, radiusX, radiusZ);
    }

    private static float SegmentSegmentDistanceSquared(
        Vector3 firstStart,
        Vector3 firstEnd,
        Vector3 secondStart,
        Vector3 secondEnd)
    {
        Vector3 firstDirection = firstEnd - firstStart;
        Vector3 secondDirection = secondEnd - secondStart;
        Vector3 separation = firstStart - secondStart;
        float firstLengthSquared = Vector3.Dot(
            firstDirection,
            firstDirection);
        float secondLengthSquared = Vector3.Dot(
            secondDirection,
            secondDirection);
        float secondProjection = Vector3.Dot(
            secondDirection,
            separation);
        float firstProgress;
        float secondProgress;

        if (firstLengthSquared <= 0.000001f
            && secondLengthSquared <= 0.000001f)
        {
            return separation.sqrMagnitude;
        }

        if (firstLengthSquared <= 0.000001f)
        {
            firstProgress = 0f;
            secondProgress = Mathf.Clamp01(
                secondProjection / secondLengthSquared);
        }
        else
        {
            float firstProjection = Vector3.Dot(
                firstDirection,
                separation);
            if (secondLengthSquared <= 0.000001f)
            {
                secondProgress = 0f;
                firstProgress = Mathf.Clamp01(
                    -firstProjection / firstLengthSquared);
            }
            else
            {
                float directionDot = Vector3.Dot(
                    firstDirection,
                    secondDirection);
                float denominator = firstLengthSquared
                    * secondLengthSquared
                    - directionDot * directionDot;
                firstProgress = denominator > 0.000001f
                    ? Mathf.Clamp01((directionDot * secondProjection
                        - firstProjection * secondLengthSquared) / denominator)
                    : 0f;
                secondProgress = (directionDot * firstProgress
                    + secondProjection) / secondLengthSquared;

                if (secondProgress < 0f)
                {
                    secondProgress = 0f;
                    firstProgress = Mathf.Clamp01(
                        -firstProjection / firstLengthSquared);
                }
                else if (secondProgress > 1f)
                {
                    secondProgress = 1f;
                    firstProgress = Mathf.Clamp01(
                        (directionDot - firstProjection)
                        / firstLengthSquared);
                }
            }
        }

        Vector3 firstClosest = firstStart
            + firstDirection * firstProgress;
        Vector3 secondClosest = secondStart
            + secondDirection * secondProgress;
        return (firstClosest - secondClosest).sqrMagnitude;
    }

    private static bool SegmentBoundingSpheresOverlap(
        Vector3 firstStart,
        Vector3 firstEnd,
        float firstPadding,
        Vector3 secondStart,
        Vector3 secondEnd,
        float secondPadding)
    {
        Vector3 firstCenter = (firstStart + firstEnd) * 0.5f;
        Vector3 secondCenter = (secondStart + secondEnd) * 0.5f;
        float firstRadius = Vector3.Distance(firstStart, firstEnd) * 0.5f
            + Mathf.Max(0f, firstPadding);
        float secondRadius = Vector3.Distance(secondStart, secondEnd) * 0.5f
            + Mathf.Max(0f, secondPadding);
        float combinedRadius = firstRadius + secondRadius;
        return (firstCenter - secondCenter).sqrMagnitude
            <= combinedRadius * combinedRadius;
    }

    private void SpawnLeavesReachedByVisibleStem()
    {
        if (leafSpawningComplete)
        {
            return;
        }

        float fullHeight = maxHeight * 2f;
        float generationScale = Mathf.Pow(childLengthMultiplier, currGen);
        float generationLeafLength = leafLength * Mathf.Pow(0.90f, currGen);
        float supportMargin = Mathf.Max(
            leafStemAdvanceMargin,
            GetStemRadius() * 2f);

        // A real stem is organized in phytomers: one leaf node followed by an
        // internode. Reserve a visibly leaf-free basal section on every stem,
        // then let internode length determine how many leaves can fit.
        float basalClearance = Mathf.Max(
            fullHeight * firstLeafHeightFraction,
            generationLeafLength * 0.5f,
            GetStemRadius() * 3f);
        float minimumLeafHeight = basalClearance;
        float maximumLeafHeight = Mathf.Min(
            fullHeight * lastLeafHeightFraction,
            fullHeight - supportMargin);

        // If this stem is too short to provide solid support above the first
        // leaf node, it does not create unsupported leaves.
        if (maximumLeafHeight < minimumLeafHeight)
        {
            leafSpawningComplete = maxed;
            return;
        }

        float availableLeafZone = maximumLeafHeight - minimumLeafHeight;
        float internodeLength = Mathf.Max(
            minimumLeafNodeSpacing * generationScale,
            generationLeafLength * leafLengthSpacingMultiplier,
            GetStemRadius() * stemRadiusLeafSpacingMultiplier);
        int leavesThatFit = 1 + Mathf.FloorToInt(
            (availableLeafZone + 0.0001f) / internodeLength);
        int leafNodeCount = Mathf.Min(runtimeLeafCount, leavesThatFit);

        if (leafNodeCount <= 0)
        {
            leafSpawningComplete = maxed;
            return;
        }

        if (nextLeafIndex >= leafNodeCount)
        {
            leafSpawningComplete = maxed;
            return;
        }

        // Distribute the nodes across the usable zone. Since leafNodeCount was
        // calculated from internodeLength, adjacent leaves never get closer
        // than the configured biological spacing.
        float actualInternodeLength = leafNodeCount <= 1
            ? 0f
            : availableLeafZone / (leafNodeCount - 1);

        while (nextLeafIndex < leafNodeCount)
        {
            float progress = leafNodeCount <= 1
                ? 0.5f
                : nextLeafIndex / (float)(leafNodeCount - 1);
            float targetHeight = leafNodeCount <= 1
                ? Mathf.Lerp(minimumLeafHeight, maximumLeafHeight, 0.5f)
                : minimumLeafHeight + nextLeafIndex * actualInternodeLength;

            // Read support from the cylinder's actual transform, rather than
            // from the growth value calculated earlier in this frame.
            if (!IsLeafNodeVisiblySupported(targetHeight, supportMargin))
            {
                nextLeafNodeSupportedSince = -1f;
                break;
            }

            // A node must remain covered for a short time. This prevents a
            // leaf from appearing on the exact frame in which a thin stem tip
            // first reaches it, which can look like growth in empty space.
            if (nextLeafNodeSupportedSince < 0f)
            {
                nextLeafNodeSupportedSince = GetVisualGrowthClock();
                break;
            }

            float leafBiologicalRate = mapleSimulation != null
                ? mapleSimulation.BiologicalLeafGrowthRateMultiplier
                : 1f;
            float effectiveMaturationDelay = leafNodeMaturationDelay
                / Mathf.Max(0.2f, leafBiologicalRate);
            if (GetVisualGrowthClock() - nextLeafNodeSupportedSince
                < effectiveMaturationDelay)
            {
                break;
            }

            float baseAzimuth = nextLeafIndex * 90f + currGen * 53f + Random.Range(-10f, 10f);

            CreateLeaf(nextLeafIndex, targetHeight, progress, baseAzimuth);
            CreateLeaf(nextLeafIndex, targetHeight, progress, baseAzimuth + 180f);

            nextLeafIndex++;
            nextLeafNodeSupportedSince = -1f;
        }

        if (maxed && nextLeafIndex >= leafNodeCount)
        {
            int rosetteLeaves = Random.Range(3, 6);
            float tipAzimuthBase = Random.Range(0f, 360f);
            for (int i = 0; i < rosetteLeaves; i++)
            {
                float tipAzimuth = tipAzimuthBase + i * (360f / rosetteLeaves) + Random.Range(-15f, 15f);
                CreateLeaf(nextLeafIndex + i, fullHeight + 0.02f, 1f, tipAzimuth);
            }

            leafSpawningComplete = true;
        }
    }

    private bool IsLeafNodeVisiblySupported(float nodeHeight, float supportMargin)
    {
        // Unity's built-in cylinder extends from -1 to +1 on its local Y axis.
        // Its scaled caps therefore provide the exact visible stem interval.
        float halfVisibleHeight = Mathf.Abs(modelCyl.localScale.y);
        float visibleStemBottom = modelCyl.localPosition.y - halfVisibleHeight;
        float visibleStemTip = modelCyl.localPosition.y + halfVisibleHeight;

        return halfVisibleHeight > 0.0001f
            && visibleStemBottom <= 0.001f
            && nodeHeight >= visibleStemBottom - 0.001f
            && nodeHeight + supportMargin <= visibleStemTip + 0.0001f;
    }

    private void CreateLeaf(int leafIndex, float height, float verticalProgress, float azimuth)
    {
        var leafObject = new GameObject($"Leaf G{currGen}-{leafIndex + 1}");
        leafObject.transform.SetParent(transform, false);
        leafObject.transform.localPosition = Vector3.up * height;

        leafObject.transform.rotation = CreateHorizontalLeafRotation(azimuth);
        RegisterArcAttachment(
            leafObject.transform,
            height,
            false);

        float middleEmphasis = 1f - Mathf.Abs(verticalProgress - 0.45f) / 0.55f;
        float sizeFactor = Mathf.Lerp(0.68f, 1.08f, Mathf.Clamp01(middleEmphasis));
        sizeFactor *= Mathf.Pow(0.90f, currGen);

        Leaf leaf = leafObject.AddComponent<Leaf>();
        float mapleLeafGrowthRate = mapleSimulation != null
            ? mapleSimulation.BiologicalLeafGrowthRateMultiplier
            : 1f;
        leaf.Initialize(
            leafLength * sizeFactor,
            leafLength * leafWidthToLength * sizeFactor,
            GetStemRadius(),
            leafGrowthDuration / Mathf.Max(0.2f, mapleLeafGrowthRate),
            leafColor,
            stemColor);
        supportedWeightCacheDirty = true;
    }

    private float GetVisualGrowthDeltaTime()
    {
        return mapleSimulation != null
            ? mapleSimulation.DvsGrowthDeltaTime
            : Time.deltaTime;
    }

    private float GetVisualGrowthClock()
    {
        return mapleSimulation != null
            ? mapleSimulation.DvsGrowthClock
            : Time.time;
    }

    private Quaternion CreateHorizontalLeafRotation(float azimuth)
    {
        Vector3 localRadial = Quaternion.AngleAxis(
            azimuth,
            Vector3.up) * Vector3.right;
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(
            transform.TransformDirection(localRadial),
            Vector3.up);
        if (horizontalDirection.sqrMagnitude <= 0.00001f)
        {
            horizontalDirection = Quaternion.AngleAxis(
                azimuth,
                Vector3.up) * Vector3.right;
        }

        horizontalDirection.Normalize();
        float downwardAngle = Random.Range(2f, 6f);
        Vector3 leafDirection = (
            horizontalDirection
            - Vector3.up * Mathf.Tan(downwardAngle * Mathf.Deg2Rad))
            .normalized;
        Vector3 leafNormal = Vector3.ProjectOnPlane(
            Vector3.up,
            leafDirection).normalized;
        Vector3 leafForward = Vector3.Cross(
            leafDirection,
            leafNormal).normalized;
        return Quaternion.LookRotation(leafForward, leafNormal);
    }

    private float GetStemRadius()
    {
        return Mathf.Max(
            0.01f,
            Mathf.Max(modelCyl.localScale.x, modelCyl.localScale.z) * 0.5f);
    }

    private void ApplyStemAppearance()
    {
        Renderer stemRenderer = modelCyl.GetComponent<Renderer>();
        if (stemRenderer == null)
        {
            return;
        }

        stemProperties ??= new MaterialPropertyBlock();
        stemRenderer.GetPropertyBlock(stemProperties);
        stemProperties.SetColor("_BaseColor", stemColor);
        stemProperties.SetColor("_Color", stemColor);
        stemRenderer.SetPropertyBlock(stemProperties);
        PlantVisualQuality.ApplyRenderer(stemRenderer);
    }

    private void OnValidate()
    {
        delayBranches = Mathf.Max(0f, delayBranches);
        vitezaCrestere = Mathf.Max(0.01f, vitezaCrestere);
        maxHeight = Mathf.Max(0.05f, maxHeight);
        maxGen = Mathf.Clamp(maxGen, 0, MaximumBranchGeneration);
        stageOneLengthFraction = Mathf.Clamp(
            stageOneLengthFraction,
            0.1f,
            0.45f);
        stageTwoLengthFraction = Mathf.Clamp(
            stageTwoLengthFraction,
            stageOneLengthFraction + 0.05f,
            0.7f);
        stageThreeLengthFraction = Mathf.Clamp(
            stageThreeLengthFraction,
            stageTwoLengthFraction + 0.05f,
            0.9f);
        stageOneThicknessFraction = Mathf.Clamp(
            stageOneThicknessFraction,
            0.15f,
            0.6f);
        stageTwoThicknessFraction = Mathf.Clamp(
            stageTwoThicknessFraction,
            stageOneThicknessFraction + 0.05f,
            0.8f);
        stageThreeThicknessFraction = Mathf.Clamp(
            stageThreeThicknessFraction,
            stageTwoThicknessFraction + 0.05f,
            0.95f);
        secondsBetweenStructuralStages = Mathf.Max(
            0f,
            secondsBetweenStructuralStages);
        matureLengthIncrease = Mathf.Clamp(matureLengthIncrease, 0f, 0.8f);
        matureThicknessIncrease = Mathf.Clamp(
            matureThicknessIncrease,
            0f,
            1.6f);
        secondaryGrowthRetentionPerOrder = Mathf.Clamp(
            secondaryGrowthRetentionPerOrder,
            0.35f,
            1f);
        secondsToFullSecondaryGrowth = Mathf.Max(
            5f,
            secondsToFullSecondaryGrowth);
        oldStemGrowthAcceleration = Mathf.Clamp(
            oldStemGrowthAcceleration,
            1f,
            3f);
        trunkSecondaryGrowthPriority = Mathf.Clamp(
            trunkSecondaryGrowthPriority,
            1f,
            2f);
        currentStructuralStage = Mathf.Clamp(currentStructuralStage, 1, 4);
        ClampBranchRange(
            ref firstOrderBranchesMin,
            ref firstOrderBranchesMax);
        ClampBranchRange(
            ref secondOrderBranchesMin,
            ref secondOrderBranchesMax);
        ClampBranchRange(
            ref thirdOrderBranchesMin,
            ref thirdOrderBranchesMax);
        ClampBranchRange(
            ref fourthOrderBranchesMin,
            ref fourthOrderBranchesMax);
        ClampBranchRange(
            ref fifthOrderBranchesMin,
            ref fifthOrderBranchesMax);
        upperChildBranchAngle = Mathf.Clamp(
            upperChildBranchAngle,
            5f,
            50f);
        lowerChildBranchAngle = Mathf.Clamp(
            lowerChildBranchAngle,
            Mathf.Max(30f, upperChildBranchAngle + 5f),
            80f);
        trunkBranchAttachmentStartFraction = Mathf.Clamp(
            trunkBranchAttachmentStartFraction,
            0.05f,
            0.7f);
        trunkBranchAttachmentEndFraction = Mathf.Clamp(
            trunkBranchAttachmentEndFraction,
            Mathf.Max(
                0.25f,
                trunkBranchAttachmentStartFraction + 0.2f),
            0.98f);
        childStemAttachmentStartFraction = Mathf.Clamp(
            childStemAttachmentStartFraction,
            0.05f,
            0.85f);
        childStemAttachmentEndFraction = Mathf.Clamp(
            childStemAttachmentEndFraction,
            Mathf.Max(0.15f, childStemAttachmentStartFraction + 0.05f),
            1f);
        childLengthMultiplier = Mathf.Clamp(
            childLengthMultiplier,
            0.35f,
            0.95f);
        primaryBranchLengthBoost = Mathf.Clamp(
            primaryBranchLengthBoost,
            0.8f,
            1.4f);
        ClampCrownFillerRange(
            ref crownFillerBranchesMin,
            ref crownFillerBranchesMax);
        crownFillerAttachmentStartFraction = Mathf.Clamp(
            crownFillerAttachmentStartFraction,
            0.65f,
            0.95f);
        crownFillerAttachmentEndFraction = Mathf.Clamp(
            crownFillerAttachmentEndFraction,
            Mathf.Max(
                0.8f,
                crownFillerAttachmentStartFraction + 0.03f),
            0.99f);
        crownFillerLowerLengthMultiplier = Mathf.Clamp(
            crownFillerLowerLengthMultiplier,
            0.2f,
            0.7f);
        crownFillerUpperLengthMultiplier = Mathf.Clamp(
            crownFillerUpperLengthMultiplier,
            0.15f,
            Mathf.Min(0.6f, crownFillerLowerLengthMultiplier));
        crownFillerLowerTiltDegrees = Mathf.Clamp(
            crownFillerLowerTiltDegrees,
            20f,
            75f);
        crownFillerUpperTiltDegrees = Mathf.Clamp(
            crownFillerUpperTiltDegrees,
            15f,
            Mathf.Min(65f, crownFillerLowerTiltDegrees));
        crownFillerThicknessMultiplier = Mathf.Clamp(
            crownFillerThicknessMultiplier,
            0.3f,
            0.8f);
        crownFillerDescendantGenerations = Mathf.Clamp(
            crownFillerDescendantGenerations,
            0,
            2);
        freeSpaceSquareThicknessMultiplier = Mathf.Max(
            1f,
            freeSpaceSquareThicknessMultiplier);
        randomGrowthZoneRadiusInStemThicknesses = Mathf.Max(
            0f,
            randomGrowthZoneRadiusInStemThicknesses);
        growthDirectionCandidateCount = Mathf.Clamp(
            growthDirectionCandidateCount,
            8,
            32);
        minimumOutwardDivergence = Mathf.Clamp(
            minimumOutwardDivergence,
            0f,
            0.8f);
        canopyBalanceInfluence = Mathf.Max(0f, canopyBalanceInfluence);
        minimumHigherOrderClearanceInStemThicknesses = Mathf.Clamp(
            minimumHigherOrderClearanceInStemThicknesses,
            0.5f,
            4f);
        maximumWeightBendDegrees = Mathf.Clamp(
            maximumWeightBendDegrees,
            0f,
            18f);
        mainTrunkBendMultiplier = Mathf.Clamp(
            mainTrunkBendMultiplier,
            0f,
            0.35f);
        weightBendResponse = Mathf.Max(0.1f, weightBendResponse);
        weightArcBendMultiplier = Mathf.Clamp(
            weightArcBendMultiplier,
            0f,
            1.5f);
        maximumWeightArcDegrees = Mathf.Clamp(
            maximumWeightArcDegrees,
            0f,
            12f);
        leavesPerStem = Mathf.Clamp(leavesPerStem, 4, 64);
        minimumLeavesPerStem = Mathf.Clamp(
            minimumLeavesPerStem,
            4,
            leavesPerStem);
        firstLeafHeightFraction = Mathf.Clamp(firstLeafHeightFraction, 0.2f, 0.5f);
        lastLeafHeightFraction = Mathf.Clamp(
            lastLeafHeightFraction,
            Mathf.Max(0.55f, firstLeafHeightFraction + 0.1f),
            0.98f);
        minimumLeafNodeSpacing = Mathf.Max(0.05f, minimumLeafNodeSpacing);
        leafLengthSpacingMultiplier = Mathf.Clamp(
            leafLengthSpacingMultiplier,
            0.3f,
            0.8f);
        stemRadiusLeafSpacingMultiplier = Mathf.Clamp(
            stemRadiusLeafSpacingMultiplier,
            1f,
            4f);
        leafLength = Mathf.Max(0.05f, leafLength);
        leafWidthToLength = Mathf.Clamp(leafWidthToLength, 0.4f, 1.1f);
        leafGrowthDuration = Mathf.Max(0.05f, leafGrowthDuration);
        leafStemAdvanceMargin = Mathf.Clamp(leafStemAdvanceMargin, 0.01f, 0.4f);
        leafNodeMaturationDelay = Mathf.Clamp(leafNodeMaturationDelay, 0f, 2f);
    }

    private static void ClampBranchRange(ref int minimum, ref int maximum)
    {
        minimum = Mathf.Clamp(
            minimum,
            0,
            MaximumChildBranchesPerStem);
        maximum = Mathf.Clamp(
            maximum,
            minimum,
            MaximumChildBranchesPerStem);
    }

    private static void ClampCrownFillerRange(
        ref int minimum,
        ref int maximum)
    {
        minimum = Mathf.Clamp(minimum, 0, 6);
        maximum = Mathf.Clamp(maximum, minimum, 6);
    }

}
