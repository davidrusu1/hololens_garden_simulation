using System.Collections.Generic;
using UnityEngine;

namespace ICI.PlantGrowth.MixedCrops
{
    /// <summary>
    /// Creates an alternating, centered grid of sunflowers and cassava plants.
    /// Every row is capped at five plants and all visual growth is synchronized
    /// with the existing phenology simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MixedCropFieldController : MonoBehaviour
    {
        public const int MaxPlantsPerType = 10;
        public const int MaxPlantsPerRow = 5;

        private enum PlantType
        {
            Sunflower,
            Cassava
        }

        [Header("Plant source")]
        [SerializeField] private GameObject sunflowerVisualPrefab;

        [Header("Default field")]
        [SerializeField, Range(0, MaxPlantsPerType)] private int sunflowerCount = 3;
        [SerializeField, Range(0, MaxPlantsPerType)] private int cassavaCount = 3;

        [Header("Layout")]
        [SerializeField, Min(0.4f)] private float horizontalSpacing = 1.25f;
        [SerializeField, Min(0.4f)] private float rowSpacing = 1.3f;
        [SerializeField] private bool staggerAlternateRows = true;
        [SerializeField] private Vector3 localOrigin = Vector3.zero;
        [Header("Real plant dimensions")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Target full mature sunflower height in metres, including the flower head.")]
        private float sunflowerMatureHeightMeters = 2.35f;
        [SerializeField, Min(0.5f)]
        [Tooltip("Measured full height of the authored mature sunflower prefab before field scaling.")]
        private float sunflowerAuthoredMatureHeightMeters = 4.26f;
        [SerializeField, Min(0.5f)]
        [Tooltip("Target mature cassava canopy height in metres.")]
        private float cassavaMatureHeightMeters = 1.85f;

        private readonly List<GameObject> plantObjects = new List<GameObject>();
        private readonly List<StemGrowthController> sunflowers =
            new List<StemGrowthController>();
        private readonly List<CassavaPlantGrowthController> cassavas =
            new List<CassavaPlantGrowthController>();
        private Transform generatedField;
        private float simulationSpeed = 1f;
        private float currentSimulationDay;
        private float currentSunflowerDevelopmentStage = -0.1f;

        public int SunflowerCount => sunflowerCount;
        public int CassavaCount => cassavaCount;
        public int TotalPlantCount => sunflowerCount + cassavaCount;
        public int RowCount => Mathf.CeilToInt(TotalPlantCount / (float)MaxPlantsPerRow);
        public float SunflowerMatureHeightMeters => sunflowerMatureHeightMeters;
        public float CassavaMatureHeightMeters => cassavaMatureHeightMeters;
        public float CurrentSimulationDay => currentSimulationDay;
        public float SunflowerDevelopmentStage =>
            currentSunflowerDevelopmentStage;
        public float CassavaCanopyProgress => cassavas.Count > 0 && cassavas[0] != null
            ? cassavas[0].CanopyProgress
            : 0f;
        public float CassavaPlannedCanopyCoverageRatio =>
            cassavas.Count > 0 && cassavas[0] != null
                ? cassavas[0].PlannedCanopyCoverageRatio
                : 0f;
        public bool CassavaPlannedCanopyCoversSideAndTop =>
            cassavas.Count > 0
            && cassavas.TrueForAll(cassava => cassava != null
                && cassava.PlannedCanopyCoversSideAndTop);
        public string CassavaGrowthStage => cassavas.Count > 0 && cassavas[0] != null
            ? cassavas[0].GrowthStageLabel
            : "Fără cassava în câmp";
        public bool SunflowerStemsStillGrowing => sunflowers.Exists(
            sunflower => sunflower != null && sunflower.IsGrowing);
        public bool SunflowerStemsReady => sunflowers.Count > 0
            && sunflowers.TrueForAll(sunflower => sunflower != null
                && sunflower.HasReachedMatureStemDimensions);
        public bool SunflowerFlowersStarted => sunflowers.Count > 0
            && sunflowers.TrueForAll(sunflower => sunflower != null
                && sunflower.HasFlowerStarted);
        public bool SunflowerFlowersComplete => sunflowers.Count > 0
            && sunflowers.TrueForAll(sunflower => sunflower != null
                && sunflower.IsFlowerComplete);

        private void Awake()
        {
            RebuildField();
        }

        public void ConfigureCounts(int requestedSunflowers, int requestedCassavas)
        {
            int clampedSunflowers = Mathf.Clamp(
                requestedSunflowers,
                0,
                MaxPlantsPerType);
            int clampedCassavas = Mathf.Clamp(
                requestedCassavas,
                0,
                MaxPlantsPerType);

            if (sunflowerCount == clampedSunflowers
                && cassavaCount == clampedCassavas
                && generatedField != null)
            {
                return;
            }

            sunflowerCount = clampedSunflowers;
            cassavaCount = clampedCassavas;
            RebuildField();
        }

        public void RebuildField()
        {
            ClearField();
            var generatedFieldObject = new GameObject("Generated Mixed Crop Field");
            generatedField = generatedFieldObject.transform;
            generatedField.SetParent(transform, false);
            generatedField.localPosition = localOrigin;

            List<PlantType> sequence = BuildAlternatingSequence(
                sunflowerCount,
                cassavaCount);
            for (int index = 0; index < sequence.Count; index++)
            {
                int row = index / MaxPlantsPerRow;
                int column = index % MaxPlantsPerRow;
                int itemsInRow = Mathf.Min(
                    MaxPlantsPerRow,
                    sequence.Count - row * MaxPlantsPerRow);
                float staggerOffset = staggerAlternateRows && row % 2 == 1
                    ? horizontalSpacing * 0.5f
                    : 0f;
                Vector3 localPosition = new Vector3(
                    (column - (itemsInRow - 1) * 0.5f) * horizontalSpacing
                        + staggerOffset,
                    0f,
                    row * rowSpacing);

                if (sequence[index] == PlantType.Sunflower)
                {
                    CreateSunflower(index, localPosition);
                }
                else
                {
                    CreateCassava(index, localPosition);
                }
            }

            SetSimulationSpeed(simulationSpeed);
            SetSimulationDay(currentSimulationDay);
            SetSunflowerDevelopmentStage(currentSunflowerDevelopmentStage);
        }

        public void ResetAllGrowth()
        {
            currentSimulationDay = 0f;
            currentSunflowerDevelopmentStage = -0.1f;
            foreach (StemGrowthController sunflower in sunflowers)
            {
                if (sunflower == null)
                {
                    continue;
                }

                sunflower.SetAutomaticStart(false);
                sunflower.SetDevelopmentStage(currentSunflowerDevelopmentStage);
            }

            foreach (CassavaPlantGrowthController cassava in cassavas)
            {
                cassava?.ResetGrowth();
            }
        }

        public void StartAllGrowth()
        {
            currentSunflowerDevelopmentStage = Mathf.Max(
                0f,
                currentSunflowerDevelopmentStage);
            foreach (StemGrowthController sunflower in sunflowers)
            {
                sunflower?.SetDevelopmentStage(
                    currentSunflowerDevelopmentStage);
            }
        }

        public void StopAllGrowth()
        {
            foreach (StemGrowthController sunflower in sunflowers)
            {
                sunflower?.StopGrowth();
            }

            foreach (CassavaPlantGrowthController cassava in cassavas)
            {
                cassava?.StopGrowth();
            }
        }

        public void SetSimulationSpeed(float speedMultiplier)
        {
            simulationSpeed = Mathf.Max(0.1f, speedMultiplier);
            foreach (StemGrowthController sunflower in sunflowers)
            {
                sunflower?.SetSimulationSpeed(simulationSpeed);
            }

            foreach (CassavaPlantGrowthController cassava in cassavas)
            {
                cassava?.SetSimulationSpeed(simulationSpeed);
            }
        }

        /// <summary>
        /// Sends one shared biological clock to every cassava plant. Sunflower
        /// flowering and maturity remain controlled by its temperature-based DVS.
        /// </summary>
        public void SetSimulationDay(float daysAfterSowing)
        {
            currentSimulationDay = Mathf.Max(0f, daysAfterSowing);
            foreach (CassavaPlantGrowthController cassava in cassavas)
            {
                cassava?.SetBiologicalAgeDays(currentSimulationDay);
            }
        }

        /// <summary>
        /// DVS is the only visual clock for sunflower. Vegetative structures
        /// follow DVS 0..1 and the flower follows DVS 1..2.
        /// </summary>
        public void SetSunflowerDevelopmentStage(float developmentStage)
        {
            currentSunflowerDevelopmentStage = Mathf.Clamp(
                developmentStage,
                -0.1f,
                2f);
            foreach (StemGrowthController sunflower in sunflowers)
            {
                sunflower?.SetDevelopmentStage(
                    currentSunflowerDevelopmentStage);
            }
        }

        public void NotifyFloweringStarted()
        {
            SetSunflowerDevelopmentStage(
                Mathf.Max(1f, currentSunflowerDevelopmentStage));
        }

        public void CompleteVisualMaturity()
        {
            SetSunflowerDevelopmentStage(2f);
        }

        private static List<PlantType> BuildAlternatingSequence(
            int requestedSunflowers,
            int requestedCassavas)
        {
            var sequence = new List<PlantType>(
                requestedSunflowers + requestedCassavas);
            int remainingSunflowers = requestedSunflowers;
            int remainingCassavas = requestedCassavas;
            PlantType next = remainingSunflowers >= remainingCassavas
                ? PlantType.Sunflower
                : PlantType.Cassava;

            while (remainingSunflowers > 0 || remainingCassavas > 0)
            {
                if (next == PlantType.Sunflower && remainingSunflowers > 0)
                {
                    sequence.Add(PlantType.Sunflower);
                    remainingSunflowers--;
                    next = PlantType.Cassava;
                }
                else if (next == PlantType.Cassava && remainingCassavas > 0)
                {
                    sequence.Add(PlantType.Cassava);
                    remainingCassavas--;
                    next = PlantType.Sunflower;
                }
                else if (remainingSunflowers > 0)
                {
                    sequence.Add(PlantType.Sunflower);
                    remainingSunflowers--;
                }
                else
                {
                    sequence.Add(PlantType.Cassava);
                    remainingCassavas--;
                }
            }

            return sequence;
        }

        private void CreateSunflower(int index, Vector3 localPosition)
        {
            if (sunflowerVisualPrefab == null)
            {
                Debug.LogWarning(
                    $"[{nameof(MixedCropFieldController)}] Sunflower visual prefab is missing.",
                    this);
                return;
            }

            GameObject plant = Instantiate(sunflowerVisualPrefab, generatedField);
            plant.name = $"{index + 1:00} - Floarea-soarelui";
            plant.transform.localPosition = localPosition;
            plant.transform.localRotation = Quaternion.Euler(0f, (index % 4) * 12f - 18f, 0f);

            StemGrowthController controller = plant.GetComponent<StemGrowthController>();
            float physicalScale = sunflowerMatureHeightMeters
                / Mathf.Max(0.1f, sunflowerAuthoredMatureHeightMeters);
            plant.transform.localScale = Vector3.one * physicalScale;
            if (controller != null)
            {
                controller.SetAutomaticStart(false);
                controller.SetSimulationSpeed(simulationSpeed);
                controller.SetDevelopmentStage(
                    currentSunflowerDevelopmentStage);
                sunflowers.Add(controller);
            }

            plantObjects.Add(plant);
            plant.SetActive(true);
        }

        private void CreateCassava(int index, Vector3 localPosition)
        {
            var plant = new GameObject($"{index + 1:00} - Cassava");
            plant.transform.SetParent(generatedField, false);
            plant.transform.localPosition = localPosition;
            plant.transform.localRotation = Quaternion.Euler(0f, (index % 5) * 17f, 0f);
            CassavaPlantGrowthController controller =
                plant.AddComponent<CassavaPlantGrowthController>();
            float physicalScale = cassavaMatureHeightMeters
                / controller.EstimatedMatureHeightMeters;
            plant.transform.localScale = Vector3.one * physicalScale;
            controller.SetAutomaticStart(false);
            controller.SetSimulationSpeed(simulationSpeed);
            controller.SetBiologicalAgeDays(currentSimulationDay);
            cassavas.Add(controller);
            plantObjects.Add(plant);
        }

        private void ClearField()
        {
            sunflowers.Clear();
            cassavas.Clear();
            plantObjects.Clear();

            if (generatedField == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedField.gameObject);
            }
            else
            {
                DestroyImmediate(generatedField.gameObject);
            }

            generatedField = null;
        }
    }
}
