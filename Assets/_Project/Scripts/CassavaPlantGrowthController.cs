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

        [Header("Palmate leaves")]
        [SerializeField, Min(0.03f)] private float petioleLength = 0.105f;
        [SerializeField, Min(0.05f)] private float leafLobeLength = 0.19f;
        [SerializeField, Min(0.01f)] private float leafLobeWidth = 0.046f;

        [Header("Appearance")]
        [SerializeField] private Color youngStemColor = new Color(0.22f, 0.48f, 0.15f, 1f);
        [SerializeField] private Color matureStemColor = new Color(0.42f, 0.3f, 0.13f, 1f);
        [SerializeField] private Color leafColor = new Color(0.09f, 0.64f, 0.17f, 1f);

        private readonly List<BranchVisual> branchVisuals = new List<BranchVisual>();
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

                return Mathf.Max(0.1f, height);
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
                Transform leaf = CreateLeaf(
                    branchRoot,
                    plan.Length * fraction,
                    CalculateLeafAzimuth(random, plan.Generation, leafIndex));
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

        private IEnumerable<BranchPlan> CreateChildPlans(
            BranchPlan parent,
            Transform branchRoot,
            System.Random random)
        {
            int childGeneration = parent.Generation + 1;
            float baseAzimuth = NextFloat(random, 0f, 120f);
            float tilt = childGeneration == 1 ? firstForkTilt : secondForkTilt;

            for (int index = 0; index < branchesPerFork; index++)
            {
                float azimuth = baseAzimuth
                    + index * (360f / branchesPerFork)
                    + NextFloat(random, -6f, 6f);
                float variedTilt = tilt + NextFloat(random, -4f, 4f);
                yield return new BranchPlan
                {
                    Parent = branchRoot,
                    LocalPosition = Vector3.up * parent.Length,
                    LocalRotation = Quaternion.AngleAxis(azimuth, Vector3.up)
                        * Quaternion.AngleAxis(variedTilt, Vector3.forward),
                    Generation = childGeneration,
                    Length = parent.Length
                        * childLengthMultiplier
                        * NextFloat(random, 0.92f, 1.08f),
                    Radius = parent.Radius * childRadiusMultiplier,
                    Seed = random.Next()
                };
            }
        }

        private int GetLeafBudget(int generation)
        {
            return generation == 0
                ? mainStemLeaves
                : Mathf.Max(3, leavesPerBranch - generation + 1);
        }

        private static float CalculateLeafAzimuth(
            System.Random random,
            int generation,
            int leafIndex)
        {
            return leafIndex * GoldenAngle
                + generation * 18f
                + NextFloat(random, -7f, 7f);
        }

        private static float CalculateLeafScale(System.Random random, int generation)
        {
            return NextFloat(random, 0.88f, 1.08f) * Mathf.Pow(0.92f, generation);
        }

        private Transform CreateLeaf(Transform branch, float height, float azimuth)
        {
            var leafRootObject = new GameObject("Palmate Cassava Leaf");
            Transform leafRoot = leafRootObject.transform;
            leafRoot.SetParent(branch, false);
            leafRoot.localPosition = Vector3.up * height;
            leafRoot.localRotation = Quaternion.AngleAxis(azimuth, Vector3.up)
                * Quaternion.AngleAxis(-13f, Vector3.forward);

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
