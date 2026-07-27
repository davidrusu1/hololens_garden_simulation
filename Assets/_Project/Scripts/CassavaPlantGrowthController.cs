using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ICI.PlantGrowth.MixedCrops
{
    /// <summary>
    /// Procedural cassava whose architecture is driven by days after planting.
    /// The same biological clock used by the sunflower simulation therefore
    /// keeps cassava slower during establishment and growing after sunflower
    /// physiological maturity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CassavaPlantGrowthController : MonoBehaviour
    {
        private sealed class BranchPlan
        {
            public Transform Parent;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public int Generation;
            public float Length;
            public float Radius;
            public int Seed;
        }

        private sealed class LeafVisual
        {
            public Transform Root;
            public float FinalScale;
            public float StartDay;
            public float EndDay;
        }

        private sealed class BranchVisual
        {
            public BranchPlan Plan;
            public Transform Root;
            public Transform Stem;
            public float StartDay;
            public float EndDay;
            public readonly List<LeafVisual> Leaves = new List<LeafVisual>();
        }

        private const float GoldenAngle = 137.5f;
        private const int CanopyElevationBandCount = 3;

        [Header("Biological calendar (days after planting)")]
        [SerializeField, Min(0f)] private float emergenceDay = 8f;
        [SerializeField, Min(1f)] private float firstForkDay = 45f;
        [SerializeField, Min(1f)] private float secondForkDay = 90f;
        [SerializeField, Min(1f)] private float fullCanopyDay = 150f;
        [SerializeField, Min(1f)] private float harvestMaturityDay = 270f;
        [SerializeField, Min(1f)] private float leafExpansionDays = 12f;

        [Header("Standalone preview")]
        [SerializeField] private bool startAutomatically;
        [SerializeField, Min(0.1f)] private float standaloneDaysPerSecond = 12f;

        [Header("Cassava architecture")]
        [SerializeField, Range(1, 2)] private int forkGenerations = 2;
        [SerializeField, Range(2, 3)] private int branchesPerFork = 3;
        [SerializeField, Min(0.2f)] private float mainStemLength = 0.92f;
        [SerializeField, Min(0.005f)] private float mainStemRadius = 0.052f;
        [SerializeField, Range(0.45f, 0.8f)] private float childLengthMultiplier = 0.64f;
        [SerializeField, Range(0.45f, 0.85f)] private float childRadiusMultiplier = 0.69f;
        [SerializeField, Range(25f, 65f)] private float firstForkTilt = 43f;
        [SerializeField, Range(25f, 70f)] private float secondForkTilt = 49f;
        [SerializeField, Range(3, 12)] private int mainStemLeaves = 7;
        [SerializeField, Range(3, 9)] private int leavesPerBranch = 5;
        [SerializeField] private int randomSeed = 1976;

        [Header("Adaptive canopy gap filling")]
        [SerializeField, Range(16, 96)]
        [Tooltip("Number of dome directions evaluated whenever a new branch or leaf is planned.")]
        private int canopyDirectionCandidates = 48;
        [SerializeField, Range(6, 16)]
        [Tooltip("Horizontal sectors used to detect uncovered areas around the canopy.")]
        private int canopyAzimuthSectors = 8;
        [SerializeField, Range(0.35f, 1f)]
        [Tooltip("How strongly branches bend toward uncovered canopy directions.")]
        private float branchGapFillingStrength = 0.76f;
        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("How strongly leaves point toward uncovered canopy directions.")]
        private float leafGapFillingStrength = 0.9f;
        [SerializeField, Range(0f, 0.3f)]
        [Tooltip("Lowest upward component allowed for a planned canopy direction. Low values include lateral gaps.")]
        private float minimumCanopyDirectionY = 0.06f;
        [SerializeField, Range(0.1f, 0.45f)]
        [Tooltip("Desired share of coverage allocated to the lateral rim of the dome.")]
        private float sideCoverageTarget = 0.3f;
        [SerializeField, Range(0.1f, 0.45f)]
        [Tooltip("Desired share of coverage allocated to the top of the dome.")]
        private float topCoverageTarget = 0.3f;

        [Header("Palmate leaves")]
        [SerializeField, Min(0.03f)] private float petioleLength = 0.105f;
        [SerializeField, Min(0.05f)] private float leafLobeLength = 0.19f;
        [SerializeField, Min(0.01f)] private float leafLobeWidth = 0.046f;

        [Header("Appearance")]
        [SerializeField] private Color youngStemColor = new Color(0.22f, 0.48f, 0.15f, 1f);
        [SerializeField] private Color matureStemColor = new Color(0.42f, 0.3f, 0.13f, 1f);
        [SerializeField] private Color leafColor = new Color(0.09f, 0.64f, 0.17f, 1f);

        private readonly List<BranchVisual> branchVisuals = new List<BranchVisual>();
        private readonly List<Vector3> plannedCanopyDirections = new List<Vector3>();
        private Transform generatedPlant;
        private Material stemMaterial;
        private Material leafMaterial;
        private Mesh palmateLeafMesh;
        private Coroutine standaloneGrowthRoutine;
        private float playbackSpeed = 1f;
        private float biologicalAgeDays;

        public bool IsGrowing => biologicalAgeDays >= emergenceDay
            && biologicalAgeDays < fullCanopyDay;
        public float BiologicalAgeDays => biologicalAgeDays;
        public float FullCanopyDay => fullCanopyDay;
        public float HarvestMaturityDay => harvestMaturityDay;
        public float CanopyProgress => Mathf.InverseLerp(emergenceDay, fullCanopyDay, biologicalAgeDays);
        public float PlannedCanopyCoverageRatio => CalculatePlannedCanopyCoverageRatio();
        public bool PlannedCanopyCoversSideAndTop =>
            HasPlannedCoverageInBand(0) && HasPlannedCoverageInBand(2);

        /// <summary>
        /// Nominal top height before the field-level metres calibration is
        /// applied. Child branches use their vertical projection.
        /// </summary>
        public float EstimatedMatureHeightMeters
        {
            get
            {
                float height = mainStemLength;
                float parentLength = mainStemLength;
                if (forkGenerations >= 1)
                {
                    parentLength *= childLengthMultiplier;
                    height += parentLength * Mathf.Cos(firstForkTilt * Mathf.Deg2Rad);
                }

                if (forkGenerations >= 2)
                {
                    parentLength *= childLengthMultiplier;
                    height += parentLength * Mathf.Cos(secondForkTilt * Mathf.Deg2Rad);
                }

                float canopyAllowance = leafLobeLength * 0.65f
                    + petioleLength * 0.25f;
                return Mathf.Max(0.1f, height + canopyAllowance);
            }
        }

        public string GrowthStageLabel
        {
            get
            {
                if (biologicalAgeDays < emergenceDay)
                {
                    return "Butaș plantat";
                }
                if (biologicalAgeDays < firstForkDay)
                {
                    return "Instalare și alungirea tulpinii";
                }
                if (biologicalAgeDays < secondForkDay)
                {
                    return "Prima ramificare";
                }
                if (biologicalAgeDays < fullCanopyDay)
                {
                    return "Dezvoltarea coroanei";
                }
                if (biologicalAgeDays < harvestMaturityDay)
                {
                    return "Coroană completă, rădăcini în îngroșare";
                }

                return "Maturitate de recoltare";
            }
        }

        private void Awake()
        {
            NormalizeCalendar();
            PrepareSharedResources();
            RebuildArchitecture();
            ApplyBiologicalAge();
        }

        private void Start()
        {
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
            DestroyRuntimeObject(stemMaterial);
            DestroyRuntimeObject(leafMaterial);
            DestroyRuntimeObject(palmateLeafMesh);
        }

        private void OnValidate()
        {
            NormalizeCalendar();
        }

        public void SetAutomaticStart(bool enabled)
        {
            startAutomatically = enabled;
        }

        public void SetSimulationSpeed(float speedMultiplier)
        {
            playbackSpeed = Mathf.Max(0.1f, speedMultiplier);
        }

        /// <summary>Used by the mixed field's shared day clock.</summary>
        public void SetBiologicalAgeDays(float daysAfterPlanting)
        {
            StopStandaloneRoutine();
            biologicalAgeDays = Mathf.Max(0f, daysAfterPlanting);
            ApplyBiologicalAge();
        }

        /// <summary>Standalone preview for scenes without a phenology clock.</summary>
        public void StartGrowth()
        {
            ResetGrowth();
            if (isActiveAndEnabled)
            {
                standaloneGrowthRoutine = StartCoroutine(RunStandaloneGrowth());
            }
        }

        public void StopGrowth()
        {
            StopStandaloneRoutine();
        }

        public void ResetGrowth()
        {
            StopStandaloneRoutine();
            biologicalAgeDays = 0f;
            ApplyBiologicalAge();
        }

        public void CompleteVisualMaturity()
        {
            SetBiologicalAgeDays(fullCanopyDay);
        }

        private IEnumerator RunStandaloneGrowth()
        {
            while (biologicalAgeDays < fullCanopyDay)
            {
                biologicalAgeDays += Time.deltaTime
                    * playbackSpeed
                    * standaloneDaysPerSecond;
                ApplyBiologicalAge();
                yield return null;
            }

            biologicalAgeDays = fullCanopyDay;
            ApplyBiologicalAge();
            standaloneGrowthRoutine = null;
        }

        private void StopStandaloneRoutine()
        {
            if (standaloneGrowthRoutine == null)
            {
                return;
            }

            StopCoroutine(standaloneGrowthRoutine);
            standaloneGrowthRoutine = null;
        }

        private void RebuildArchitecture()
        {
            branchVisuals.Clear();
            plannedCanopyDirections.Clear();
            RecreateGeneratedPlantRoot();
            BuildBranchVisual(new BranchPlan
            {
                Parent = generatedPlant,
                LocalPosition = Vector3.zero,
                LocalRotation = Quaternion.identity,
                Generation = 0,
                Length = mainStemLength,
                Radius = mainStemRadius,
                Seed = randomSeed
            });
        }

        private void BuildBranchVisual(BranchPlan plan)
        {
            var random = new System.Random(plan.Seed);
            Transform branchRoot = CreateBranchRoot(plan);
            GameObject stemObject = CreateStem(branchRoot, plan.Radius, 0.001f);
            GetGenerationWindow(plan.Generation, out float startDay, out float endDay);

            var visual = new BranchVisual
            {
                Plan = plan,
                Root = branchRoot,
                Stem = stemObject.transform,
                StartDay = startDay,
                EndDay = endDay
            };
            branchVisuals.Add(visual);

            int leafBudget = GetLeafBudget(plan.Generation);
            float firstLeafFraction = plan.Generation == 0 ? 0.2f : 0.32f;
            float lastLeafFraction = plan.Generation < forkGenerations ? 0.86f : 0.96f;
            for (int leafIndex = 0; leafIndex < leafBudget; leafIndex++)
            {
                float fraction = leafBudget == 1
                    ? lastLeafFraction
                    : Mathf.Lerp(
                        firstLeafFraction,
                        lastLeafFraction,
                        leafIndex / (float)(leafBudget - 1));
                float leafStartDay = Mathf.Lerp(startDay, endDay, fraction);
                Quaternion leafRotation = CalculateGapFillingLeafRotation(
                    branchRoot,
                    random,
                    plan.Generation,
                    leafIndex,
                    out Vector3 leafCanopyDirection);
                Transform leaf = CreateLeaf(
                    branchRoot,
                    plan.Length * fraction,
                    leafRotation);
                RegisterCanopyDirection(leafCanopyDirection);
                visual.Leaves.Add(new LeafVisual
                {
                    Root = leaf,
                    FinalScale = CalculateLeafScale(random, plan.Generation),
                    StartDay = leafStartDay,
                    EndDay = Mathf.Min(endDay + leafExpansionDays, leafStartDay + leafExpansionDays)
                });
            }

            if (plan.Generation >= forkGenerations)
            {
                return;
            }

            foreach (BranchPlan child in CreateChildPlans(plan, branchRoot, random))
            {
                BuildBranchVisual(child);
            }
        }

        private void ApplyBiologicalAge()
        {
            foreach (BranchVisual visual in branchVisuals)
            {
                float branchProgress = SmoothProgress(
                    visual.StartDay,
                    visual.EndDay,
                    biologicalAgeDays);
                bool visible = branchProgress > 0.0001f;
                visual.Root.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                float currentRadius = visual.Plan.Radius * Mathf.Lerp(0.52f, 1f, branchProgress);
                SetStemLength(
                    visual.Stem,
                    currentRadius,
                    visual.Plan.Length * branchProgress);

                foreach (LeafVisual leaf in visual.Leaves)
                {
                    float leafProgress = SmoothProgress(
                        leaf.StartDay,
                        leaf.EndDay,
                        biologicalAgeDays);
                    leaf.Root.gameObject.SetActive(leafProgress > 0.0001f);
                    leaf.Root.localScale = Vector3.one * leaf.FinalScale * leafProgress;
                }
            }

            float maturityProgress = SmoothProgress(emergenceDay, fullCanopyDay, biologicalAgeDays);
            SetMaterialColor(stemMaterial, Color.Lerp(youngStemColor, matureStemColor, maturityProgress));
        }

        private void GetGenerationWindow(int generation, out float startDay, out float endDay)
        {
            if (generation <= 0)
            {
                startDay = emergenceDay;
                endDay = firstForkDay;
            }
            else if (generation == 1)
            {
                startDay = firstForkDay;
                endDay = secondForkDay;
            }
            else
            {
                startDay = secondForkDay;
                endDay = fullCanopyDay;
            }
        }

        private Transform CreateBranchRoot(BranchPlan plan)
        {
            var rootObject = new GameObject($"Cassava Branch G{plan.Generation}");
            Transform branchRoot = rootObject.transform;
            branchRoot.SetParent(plan.Parent, false);
            branchRoot.localPosition = plan.LocalPosition;
            branchRoot.localRotation = plan.LocalRotation;
            return branchRoot;
        }

        private GameObject CreateStem(Transform parent, float radius, float length)
        {
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Woody Stem";
            stem.transform.SetParent(parent, false);
            SetStemLength(stem.transform, radius, length);
            RemoveCollider(stem);
            stem.GetComponent<Renderer>().sharedMaterial = stemMaterial;
            return stem;
        }

        private static void SetStemLength(Transform stem, float radius, float length)
        {
            float visibleLength = Mathf.Max(0.001f, length);
            stem.localScale = new Vector3(radius, visibleLength * 0.5f, radius);
            stem.localPosition = Vector3.up * (visibleLength * 0.5f);
        }

        private List<BranchPlan> CreateChildPlans(
            BranchPlan parent,
            Transform branchRoot,
            System.Random random)
        {
            var children = new List<BranchPlan>(branchesPerFork);
            int childGeneration = parent.Generation + 1;
            float baseAzimuth = NextFloat(random, 0f, 120f);
            float tilt = childGeneration == 1 ? firstForkTilt : secondForkTilt;

            for (int index = 0; index < branchesPerFork; index++)
            {
                float azimuth = baseAzimuth
                    + index * (360f / branchesPerFork)
                    + NextFloat(random, -6f, 6f);
                float variedTilt = tilt + NextFloat(random, -4f, 4f);
                Quaternion naturalLocalRotation =
                    Quaternion.AngleAxis(azimuth, Vector3.up)
                    * Quaternion.AngleAxis(variedTilt, Vector3.forward);
                Vector3 naturalPlantDirection = BranchLocalToPlantDirection(
                    branchRoot,
                    naturalLocalRotation * Vector3.up);
                Vector3 gapDirection = FindLeastCoveredDomeDirection(
                    random,
                    0.985f);
                Vector3 chosenPlantDirection = ClampDomeDirection(
                    Vector3.Slerp(
                        naturalPlantDirection,
                        gapDirection,
                        branchGapFillingStrength),
                    0.985f);
                Vector3 chosenParentDirection = PlantToBranchLocalDirection(
                    branchRoot,
                    chosenPlantDirection);

                children.Add(new BranchPlan
                {
                    Parent = branchRoot,
                    LocalPosition = Vector3.up * parent.Length,
                    LocalRotation = Quaternion.FromToRotation(
                        Vector3.up,
                        chosenParentDirection),
                    Generation = childGeneration,
                    Length = parent.Length
                        * childLengthMultiplier
                        * NextFloat(random, 0.92f, 1.08f),
                    Radius = parent.Radius * childRadiusMultiplier,
                    Seed = random.Next()
                });
                RegisterCanopyDirection(chosenPlantDirection);
            }

            return children;
        }

        private int GetLeafBudget(int generation)
        {
            return generation == 0
                ? mainStemLeaves
                : Mathf.Max(3, leavesPerBranch - generation + 1);
        }

        private Quaternion CalculateGapFillingLeafRotation(
            Transform branch,
            System.Random random,
            int generation,
            int leafIndex,
            out Vector3 chosenPlantDirection)
        {
            float naturalAzimuth = leafIndex * GoldenAngle
                + generation * 18f
                + NextFloat(random, -7f, 7f);
            float naturalLift = NextFloat(random, -18f, -7f);
            Quaternion naturalLocalRotation =
                Quaternion.AngleAxis(naturalAzimuth, Vector3.up)
                * Quaternion.AngleAxis(naturalLift, Vector3.forward);
            Vector3 naturalPlantDirection = BranchLocalToPlantDirection(
                branch,
                naturalLocalRotation * Vector3.right);
            Vector3 gapDirection = FindLeastCoveredDomeDirection(random, 0.92f);
            chosenPlantDirection = ClampDomeDirection(
                Vector3.Slerp(
                    naturalPlantDirection,
                    gapDirection,
                    leafGapFillingStrength),
                0.92f);

            Vector3 localDirection = PlantToBranchLocalDirection(
                branch,
                chosenPlantDirection);
            Vector3 localPlantUp = PlantToBranchLocalDirection(
                branch,
                Vector3.up);
            Vector3 localLeafUp = Vector3.ProjectOnPlane(
                localPlantUp,
                localDirection);
            if (localLeafUp.sqrMagnitude <= 0.0001f)
            {
                localLeafUp = Vector3.ProjectOnPlane(
                    Vector3.forward,
                    localDirection);
            }
            localLeafUp.Normalize();
            Vector3 localForward = Vector3.Cross(
                localDirection,
                localLeafUp).normalized;
            return Quaternion.LookRotation(localForward, localLeafUp);
        }

        private static float CalculateLeafScale(System.Random random, int generation)
        {
            return NextFloat(random, 0.88f, 1.08f) * Mathf.Pow(0.92f, generation);
        }

        private Transform CreateLeaf(
            Transform branch,
            float height,
            Quaternion localRotation)
        {
            var leafRootObject = new GameObject("Palmate Cassava Leaf");
            Transform leafRoot = leafRootObject.transform;
            leafRoot.SetParent(branch, false);
            leafRoot.localPosition = Vector3.up * height;
            leafRoot.localRotation = localRotation;

            GameObject petiole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            petiole.name = "Long Petiole";
            petiole.transform.SetParent(leafRoot, false);
            petiole.transform.localPosition = Vector3.right * (petioleLength * 0.5f);
            petiole.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            petiole.transform.localScale = new Vector3(0.008f, petioleLength * 0.5f, 0.008f);
            petiole.GetComponent<Renderer>().sharedMaterial = stemMaterial;
            RemoveCollider(petiole);

            var blade = new GameObject("Seven Lobe Leaf Blade");
            blade.transform.SetParent(leafRoot, false);
            blade.AddComponent<MeshFilter>().sharedMesh = palmateLeafMesh;
            blade.AddComponent<MeshRenderer>().sharedMaterial = leafMaterial;
            leafRoot.localScale = Vector3.zero;
            return leafRoot;
        }

        private Vector3 FindLeastCoveredDomeDirection(
            System.Random random,
            float maximumDirectionY)
        {
            int candidateCount = Mathf.Max(16, canopyDirectionCandidates);
            float phase = NextFloat(random, 0f, 360f);
            Vector3 bestDirection = Vector3.up;
            float bestScore = float.NegativeInfinity;

            for (int index = 0; index < candidateCount; index++)
            {
                float normalized = (index + 0.5f) / candidateCount;
                float y = Mathf.Lerp(
                    minimumCanopyDirectionY,
                    maximumDirectionY,
                    normalized);
                float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float azimuth = (phase + index * GoldenAngle) * Mathf.Deg2Rad;
                Vector3 candidate = new Vector3(
                    Mathf.Sin(azimuth) * radial,
                    y,
                    Mathf.Cos(azimuth) * radial);
                float score = ScoreCanopyDirection(candidate);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestDirection = candidate;
            }

            return bestDirection.normalized;
        }

        private float ScoreCanopyDirection(Vector3 candidate)
        {
            GetCanopyCell(
                candidate,
                out int candidateBand,
                out int candidateSector);
            int cellOccupancy = 0;
            int neighborOccupancy = 0;
            int bandOccupancy = 0;
            float nearestAngle = plannedCanopyDirections.Count == 0 ? 120f : 180f;
            float localDensity = 0f;

            foreach (Vector3 occupiedDirection in plannedCanopyDirections)
            {
                GetCanopyCell(
                    occupiedDirection,
                    out int occupiedBand,
                    out int occupiedSector);
                if (occupiedBand == candidateBand)
                {
                    bandOccupancy++;
                    int sectorDistance = Mathf.Abs(
                        occupiedSector - candidateSector);
                    sectorDistance = Mathf.Min(
                        sectorDistance,
                        canopyAzimuthSectors - sectorDistance);
                    if (sectorDistance == 0)
                    {
                        cellOccupancy++;
                    }
                    else if (sectorDistance == 1)
                    {
                        neighborOccupancy++;
                    }
                }

                float dot = Mathf.Clamp(
                    Vector3.Dot(candidate, occupiedDirection),
                    -1f,
                    1f);
                nearestAngle = Mathf.Min(
                    nearestAngle,
                    Mathf.Acos(dot) * Mathf.Rad2Deg);
                localDensity += Mathf.Exp((dot - 1f) / 0.08f);
            }

            float desiredBandCount = GetDesiredCoverageShare(candidateBand)
                * (plannedCanopyDirections.Count + 1f);
            float bandDeficit = desiredBandCount - bandOccupancy;
            return nearestAngle * 1.25f
                + bandDeficit * 28f
                - cellOccupancy * 42f
                - neighborOccupancy * 7f
                - localDensity * 16f;
        }

        private float GetDesiredCoverageShare(int band)
        {
            float side = Mathf.Max(0.1f, sideCoverageTarget);
            float top = Mathf.Max(0.1f, topCoverageTarget);
            float middle = Mathf.Max(0.1f, 1f - side - top);
            float total = side + middle + top;
            if (band <= 0)
            {
                return side / total;
            }
            if (band >= CanopyElevationBandCount - 1)
            {
                return top / total;
            }

            return middle / total;
        }

        private void RegisterCanopyDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            plannedCanopyDirections.Add(direction.normalized);
        }

        private Vector3 BranchLocalToPlantDirection(
            Transform branch,
            Vector3 localDirection)
        {
            Vector3 worldDirection = branch.TransformDirection(localDirection);
            return generatedPlant.InverseTransformDirection(
                worldDirection).normalized;
        }

        private Vector3 PlantToBranchLocalDirection(
            Transform branch,
            Vector3 plantDirection)
        {
            Vector3 worldDirection = generatedPlant.TransformDirection(
                plantDirection);
            return branch.InverseTransformDirection(
                worldDirection).normalized;
        }

        private Vector3 ClampDomeDirection(
            Vector3 direction,
            float maximumDirectionY)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.up;
            }

            direction.Normalize();
            float y = Mathf.Clamp(
                direction.y,
                minimumCanopyDirectionY,
                maximumDirectionY);
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            if (horizontal.sqrMagnitude <= 0.0001f)
            {
                horizontal = Vector3.right;
            }
            horizontal.Normalize();
            float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return horizontal * radial + Vector3.up * y;
        }

        private float CalculatePlannedCanopyCoverageRatio()
        {
            if (plannedCanopyDirections.Count == 0)
            {
                return 0f;
            }

            var occupiedCells = new HashSet<int>();
            foreach (Vector3 direction in plannedCanopyDirections)
            {
                GetCanopyCell(direction, out int band, out int sector);
                occupiedCells.Add(band * canopyAzimuthSectors + sector);
            }

            int totalCells = canopyAzimuthSectors * CanopyElevationBandCount;
            return occupiedCells.Count / (float)Mathf.Max(1, totalCells);
        }

        private bool HasPlannedCoverageInBand(int targetBand)
        {
            foreach (Vector3 direction in plannedCanopyDirections)
            {
                GetCanopyCell(direction, out int band, out _);
                if (band == targetBand)
                {
                    return true;
                }
            }

            return false;
        }

        private void GetCanopyCell(
            Vector3 direction,
            out int band,
            out int sector)
        {
            Vector3 normalized = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.up;
            band = normalized.y < 0.38f
                ? 0
                : normalized.y < 0.72f
                    ? 1
                    : 2;
            float azimuth = Mathf.Repeat(
                Mathf.Atan2(normalized.x, normalized.z) * Mathf.Rad2Deg,
                360f);
            sector = Mathf.Clamp(
                Mathf.FloorToInt(
                    azimuth / 360f * canopyAzimuthSectors),
                0,
                canopyAzimuthSectors - 1);
        }

        private void PrepareSharedResources()
        {
            Shader shader = Shader.Find("HDRP/Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");

            stemMaterial = new Material(shader)
            {
                name = "Cassava Runtime Stem Material",
                hideFlags = HideFlags.DontSave
            };
            SetMaterialColor(stemMaterial, youngStemColor);

            leafMaterial = new Material(shader)
            {
                name = "Cassava Runtime Leaf Material",
                hideFlags = HideFlags.DontSave
            };
            SetMaterialColor(leafMaterial, leafColor);
            SetDoubleSided(leafMaterial);

            palmateLeafMesh = BuildPalmateLeafMesh();
            palmateLeafMesh.hideFlags = HideFlags.DontSave;
        }

        private Mesh BuildPalmateLeafMesh()
        {
            const int lobeCount = 7;
            const int segmentsPerLobe = 7;
            float[] angles = { 0f, -28f, 28f, -55f, 55f, -82f, 82f };
            var vertices = new List<Vector3>(lobeCount * (segmentsPerLobe + 1) * 2);
            var uv = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(lobeCount * segmentsPerLobe * 6);
            Vector3 center = Vector3.right * petioleLength;

            for (int lobe = 0; lobe < lobeCount; lobe++)
            {
                float angle = angles[lobe] * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = new Vector3(-direction.z, 0f, direction.x);
                float lengthScale = 1f - Mathf.Abs(angles[lobe]) / 260f;
                int baseVertex = vertices.Count;

                for (int segment = 0; segment <= segmentsPerLobe; segment++)
                {
                    float progress = segment / (float)segmentsPerLobe;
                    float halfWidth = leafLobeWidth
                        * 0.5f
                        * Mathf.Sin(progress * Mathf.PI)
                        * lengthScale;
                    float lift = Mathf.Sin(progress * Mathf.PI) * 0.012f;
                    Vector3 axis = center
                        + direction * (leafLobeLength * lengthScale * progress);
                    vertices.Add(axis - side * halfWidth + Vector3.up * lift);
                    vertices.Add(axis + side * halfWidth + Vector3.up * lift);
                    uv.Add(new Vector2(progress, 0f));
                    uv.Add(new Vector2(progress, 1f));

                    if (segment == segmentsPerLobe)
                    {
                        continue;
                    }

                    int vertex = baseVertex + segment * 2;
                    triangles.Add(vertex);
                    triangles.Add(vertex + 2);
                    triangles.Add(vertex + 1);
                    triangles.Add(vertex + 1);
                    triangles.Add(vertex + 2);
                    triangles.Add(vertex + 3);
                }
            }

            var mesh = new Mesh { name = "Procedural Seven-Lobe Cassava Leaf" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void RecreateGeneratedPlantRoot()
        {
            if (generatedPlant != null)
            {
                DestroyRuntimeObject(generatedPlant.gameObject);
            }

            generatedPlant = new GameObject("Generated Cassava Plant").transform;
            generatedPlant.SetParent(transform, false);
        }

        private void NormalizeCalendar()
        {
            emergenceDay = Mathf.Max(0f, emergenceDay);
            firstForkDay = Mathf.Max(emergenceDay + 1f, firstForkDay);
            secondForkDay = Mathf.Max(firstForkDay + 1f, secondForkDay);
            fullCanopyDay = Mathf.Max(secondForkDay + 1f, fullCanopyDay);
            harvestMaturityDay = Mathf.Max(fullCanopyDay + 1f, harvestMaturityDay);
            leafExpansionDays = Mathf.Max(1f, leafExpansionDays);
            canopyDirectionCandidates = Mathf.Clamp(
                canopyDirectionCandidates,
                16,
                96);
            canopyAzimuthSectors = Mathf.Clamp(
                canopyAzimuthSectors,
                6,
                16);
            branchGapFillingStrength = Mathf.Clamp(
                branchGapFillingStrength,
                0.35f,
                1f);
            leafGapFillingStrength = Mathf.Clamp(
                leafGapFillingStrength,
                0.5f,
                1f);
            minimumCanopyDirectionY = Mathf.Clamp(
                minimumCanopyDirectionY,
                0f,
                0.3f);
            sideCoverageTarget = Mathf.Clamp(
                sideCoverageTarget,
                0.1f,
                0.45f);
            topCoverageTarget = Mathf.Clamp(
                topCoverageTarget,
                0.1f,
                0.45f);
        }

        private static float SmoothProgress(float start, float end, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.18f);
            }
        }

        private static void SetDoubleSided(Material material)
        {
            if (material.HasProperty("_CullMode"))
            {
                material.SetFloat("_CullMode", 0f);
            }
            if (material.HasProperty("_CullModeForward"))
            {
                material.SetFloat("_CullModeForward", 0f);
            }
            if (material.HasProperty("_DoubleSidedEnable"))
            {
                material.SetFloat("_DoubleSidedEnable", 1f);
            }
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyRuntimeObject(collider);
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static float NextFloat(System.Random random, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }
    }
}
