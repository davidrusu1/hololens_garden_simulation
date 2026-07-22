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
        [SerializeField, Min(0.1f)] private float sunflowerScale = 0.72f;
        [SerializeField, Min(0.1f)] private float cassavaScale = 1f;

        private readonly List<GameObject> plantObjects = new List<GameObject>();
        private readonly List<StemGrowthController> sunflowers =
            new List<StemGrowthController>();
        private readonly List<CassavaPlantGrowthController> cassavas =
            new List<CassavaPlantGrowthController>();
        private Transform generatedField;
        private float simulationSpeed = 1f;

        public int SunflowerCount => sunflowerCount;
        public int CassavaCount => cassavaCount;
        public int TotalPlantCount => sunflowerCount + cassavaCount;
        public int RowCount => Mathf.CeilToInt(TotalPlantCount / (float)MaxPlantsPerRow);

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
        }

        public void ResetAllGrowth()
        {
            foreach (StemGrowthController sunflower in sunflowers)
            {
                if (sunflower == null)
                {
                    continue;
                }

                sunflower.SetAutomaticStart(false);
                sunflower.SetStage(1);
                sunflower.StopGrowth();
            }

            foreach (CassavaPlantGrowthController cassava in cassavas)
            {
                cassava?.ResetGrowth();
            }
        }

        public void StartAllGrowth()
        {
            foreach (StemGrowthController sunflower in sunflowers)
            {
                sunflower?.StartGrowth();
            }

            foreach (CassavaPlantGrowthController cassava in cassavas)
            {
                cassava?.StartGrowth();
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

        public void NotifyFloweringStarted()
        {
            foreach (StemGrowthController sunflower in sunflowers)
            {
                sunflower?.NotifyFloweringStarted();
            }
        }

        public void CompleteVisualMaturity()
        {
            foreach (StemGrowthController sunflower in sunflowers)
            {
                sunflower?.CompleteVisualMaturity();
            }

            foreach (CassavaPlantGrowthController cassava in cassavas)
            {
                cassava?.CompleteVisualMaturity();
            }
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
            plant.transform.localScale = Vector3.one * sunflowerScale;

            StemGrowthController controller = plant.GetComponent<StemGrowthController>();
            if (controller != null)
            {
                controller.SetAutomaticStart(false);
                controller.SetSimulationSpeed(simulationSpeed);
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
            plant.transform.localScale = Vector3.one * cassavaScale;
            CassavaPlantGrowthController controller =
                plant.AddComponent<CassavaPlantGrowthController>();
            controller.SetAutomaticStart(false);
            controller.SetSimulationSpeed(simulationSpeed);
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
