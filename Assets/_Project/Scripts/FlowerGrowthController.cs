using System.Collections;
using UnityEngine;

namespace ICI.PlantGrowth
{
    [DisallowMultipleComponent]
    public sealed class FlowerGrowthController : MonoBehaviour
    {
        private const int FlowerStageCount = 3;

        [Header("Flower models (stage 1 to stage 3)")]
        [SerializeField]
        private GameObject[] flowerStagePrefabs = new GameObject[FlowerStageCount];

        [Header("Growth settings")]
        [SerializeField, Min(0.01f)]
        private float secondsBetweenGrowthSteps = 3f;

        [SerializeField, Min(0.1f)]
        private float simulationSpeedMultiplier = 1f;

        [SerializeField, Min(1f)]
        private float scaleMultiplier = 1.2f;

        [SerializeField, Min(1)]
        private int growthStepsBeforeNextStage = 3;

        [SerializeField, Min(0.1f)]
        [Tooltip("Additional uniform scale applied to every flower stage.")]
        private float flowerSizeMultiplier = 1.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Vertical overlap between the flower peduncle and the top of the main stem.")]
        private float peduncleStemOverlap = 0.015f;

        [SerializeField, Range(0.01f, 0.25f)]
        [Tooltip("Lowest fraction of the peduncle used to measure its real base diameter.")]
        private float peduncleBaseBandFraction = 0.06f;

        private Transform growthParent;
        private GameObject activeFlower;
        private Coroutine growthRoutine;
        private int currentStage;
        private Material[] stemMaterials;

        public int CurrentStage => currentStage;
        public bool HasStarted => growthRoutine != null || activeFlower != null;
        public bool IsComplete => activeFlower != null
            && growthRoutine == null
            && currentStage >= FlowerStageCount;

        public void SetSimulationSpeed(float speedMultiplier)
        {
            simulationSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
        }

        public void Initialize(Transform parent)
        {
            growthParent = parent != null ? parent : transform;
        }

        public void SetStemMaterials(Material[] materials)
        {
            stemMaterials = materials;
        }

        public void StartFlowerGrowth(float stemTopHeight, float stemTopThickness)
        {
            if (HasStarted || !HasAllFlowerPrefabs())
            {
                return;
            }

            if (growthParent == null)
            {
                growthParent = transform;
            }

            growthRoutine = StartCoroutine(GrowFlower(stemTopHeight, stemTopThickness));
        }

        public void CompleteGrowthImmediately(
            float stemTopHeight,
            float stemTopThickness)
        {
            if (!HasAllFlowerPrefabs())
            {
                return;
            }

            if (growthParent == null)
            {
                growthParent = transform;
            }

            if (growthRoutine != null)
            {
                StopCoroutine(growthRoutine);
                growthRoutine = null;
            }

            float previousSize = 0f;
            for (int stage = 1; stage <= FlowerStageCount; stage++)
            {
                currentStage = stage;
                ShowStage(stage, stemTopHeight, stemTopThickness, previousSize);

                if (stage >= FlowerStageCount || activeFlower == null)
                {
                    continue;
                }

                for (int step = 0; step < growthStepsBeforeNextStage; step++)
                {
                    activeFlower.transform.localScale *= scaleMultiplier;
                    MatchPeduncleBaseThickness(activeFlower, stemTopThickness);
                    AlignPeduncleBaseToStem(activeFlower, stemTopHeight);
                }

                previousSize = MeasureFlowerSize(activeFlower);
            }

            currentStage = FlowerStageCount;
        }

        public void ResetFlower()
        {
            if (growthRoutine != null)
            {
                StopCoroutine(growthRoutine);
                growthRoutine = null;
            }

            if (activeFlower != null)
            {
                Destroy(activeFlower);
                activeFlower = null;
            }

            currentStage = 0;
        }

        private IEnumerator GrowFlower(float stemTopHeight, float stemTopThickness)
        {
            currentStage = 1;
            ShowStage(currentStage, stemTopHeight, stemTopThickness, 0f);

            while (currentStage < FlowerStageCount)
            {
                for (int step = 0; step < growthStepsBeforeNextStage; step++)
                {
                    yield return WaitForSimulationSeconds(secondsBetweenGrowthSteps);

                    if (activeFlower == null)
                    {
                        growthRoutine = null;
                        yield break;
                    }

                    activeFlower.transform.localScale *= scaleMultiplier;
                    MatchPeduncleBaseThickness(activeFlower, stemTopThickness);
                    AlignPeduncleBaseToStem(activeFlower, stemTopHeight);
                }

                float previousSize = MeasureFlowerSize(activeFlower);
                currentStage++;
                ShowStage(currentStage, stemTopHeight, stemTopThickness, previousSize);
            }

            // Stage 3 is terminal and no longer changes size.
            growthRoutine = null;
        }

        private IEnumerator WaitForSimulationSeconds(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                remaining -= Time.deltaTime * Mathf.Max(0.1f, simulationSpeedMultiplier);
                yield return null;
            }
        }

        private void ShowStage(
            int stage,
            float stemTopHeight,
            float stemTopThickness,
            float minimumSize)
        {
            if (activeFlower != null)
            {
                Destroy(activeFlower);
            }

            GameObject prefab = flowerStagePrefabs[stage - 1];
            activeFlower = Instantiate(prefab, growthParent, false);
            activeFlower.name = $"Flower Stage {stage}";
            CopyPrefabTransform(activeFlower.transform, prefab.transform);
            activeFlower.transform.localScale *= flowerSizeMultiplier;
            activeFlower.transform.localPosition += Vector3.up * stemTopHeight;
            FaceActiveCamera(activeFlower.transform);
            ApplyStemColorToPeduncle(activeFlower);

            if (minimumSize > 0f)
            {
                float naturalSize = Mathf.Max(MeasureFlowerSize(activeFlower), 0.0001f);
                if (naturalSize < minimumSize)
                {
                    activeFlower.transform.localScale *= minimumSize / naturalSize;
                }
            }

            MatchPeduncleBaseThickness(activeFlower, stemTopThickness);
            AlignPeduncleBaseToStem(activeFlower, stemTopHeight);
        }

        private void MatchPeduncleBaseThickness(GameObject flower, float targetLocalThickness)
        {
            if (targetLocalThickness <= 0f)
            {
                return;
            }

            for (int pass = 0; pass < 3; pass++)
            {
                float currentLocalThickness = MeasurePeduncleBaseThickness(flower);
                if (currentLocalThickness <= 0.0001f)
                {
                    return;
                }

                float thicknessFactor = targetLocalThickness / currentLocalThickness;
                if (Mathf.Abs(1f - thicknessFactor) <= 0.001f)
                {
                    break;
                }

                foreach (MeshFilter meshFilter in GetPeduncleMeshes(flower))
                {
                    Vector3 scale = meshFilter.transform.localScale;
                    scale.x *= thicknessFactor;
                    scale.z *= thicknessFactor;
                    meshFilter.transform.localScale = scale;
                }
            }
        }

        private float MeasurePeduncleBaseThickness(GameObject flower)
        {
            Transform reference = growthParent != null ? growthParent : transform;
            var points = new System.Collections.Generic.List<Vector3>();

            foreach (MeshFilter meshFilter in GetPeduncleMeshes(flower))
            {
                if (meshFilter.sharedMesh == null)
                {
                    continue;
                }

                foreach (Vector3 vertex in meshFilter.sharedMesh.vertices)
                {
                    points.Add(reference.InverseTransformPoint(
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

            float bandMaximum = minimumY
                + (maximumY - minimumY) * peduncleBaseBandFraction;
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumZ = float.PositiveInfinity;
            float maximumZ = float.NegativeInfinity;

            foreach (Vector3 point in points)
            {
                if (point.y > bandMaximum)
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
                return 0f;
            }

            return Mathf.Max(maximumX - minimumX, maximumZ - minimumZ);
        }

        private void AlignPeduncleBaseToStem(GameObject flower, float stemTopHeight)
        {
            Transform reference = growthParent != null ? growthParent : transform;
            float peduncleBaseHeight = float.PositiveInfinity;

            foreach (MeshFilter meshFilter in GetPeduncleMeshes(flower))
            {
                if (meshFilter.sharedMesh == null)
                {
                    continue;
                }

                foreach (Vector3 vertex in meshFilter.sharedMesh.vertices)
                {
                    float y = reference.InverseTransformPoint(
                        meshFilter.transform.TransformPoint(vertex)).y;
                    peduncleBaseHeight = Mathf.Min(peduncleBaseHeight, y);
                }
            }

            if (float.IsInfinity(peduncleBaseHeight))
            {
                return;
            }

            float targetBaseHeight = stemTopHeight - peduncleStemOverlap;
            flower.transform.localPosition += Vector3.up
                * (targetBaseHeight - peduncleBaseHeight);
        }

        private static MeshFilter[] GetPeduncleMeshes(GameObject flower)
        {
            return System.Array.FindAll(
                flower.GetComponentsInChildren<MeshFilter>(true),
                meshFilter => meshFilter.name.Contains("Peduncle"));
        }

        private static void FaceActiveCamera(Transform flower)
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null)
            {
                flower.localRotation *= Quaternion.Euler(0f, 180f, 0f);
                return;
            }

            Vector3 directionToCamera = activeCamera.transform.position - flower.position;
            directionToCamera.y = 0f;
            if (directionToCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            flower.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
        }

        private void ApplyStemColorToPeduncle(GameObject flower)
        {
            if (stemMaterials == null || stemMaterials.Length == 0)
            {
                return;
            }

            foreach (Renderer flowerRenderer in flower.GetComponentsInChildren<Renderer>(true))
            {
                if (flowerRenderer.name.Contains("Peduncle"))
                {
                    flowerRenderer.sharedMaterials = stemMaterials;
                }
            }
        }

        private static float MeasureFlowerSize(GameObject flower)
        {
            bool hasBounds = false;
            Bounds bounds = default;

            foreach (Renderer flowerRenderer in flower.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = flowerRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(flowerRenderer.bounds);
                }
            }

            return hasBounds
                ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z)
                : 0f;
        }

        private bool HasAllFlowerPrefabs()
        {
            if (flowerStagePrefabs == null || flowerStagePrefabs.Length != FlowerStageCount)
            {
                return false;
            }

            foreach (GameObject prefab in flowerStagePrefabs)
            {
                if (prefab == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void CopyPrefabTransform(Transform instance, Transform prefab)
        {
            instance.localPosition = prefab.localPosition;
            instance.localRotation = prefab.localRotation;
            instance.localScale = prefab.localScale;
        }

        private void OnValidate()
        {
            secondsBetweenGrowthSteps = Mathf.Max(0.01f, secondsBetweenGrowthSteps);
            simulationSpeedMultiplier = Mathf.Max(0.1f, simulationSpeedMultiplier);
            scaleMultiplier = Mathf.Max(1f, scaleMultiplier);
            growthStepsBeforeNextStage = Mathf.Max(1, growthStepsBeforeNextStage);
            flowerSizeMultiplier = Mathf.Max(0.1f, flowerSizeMultiplier);
            peduncleStemOverlap = Mathf.Max(0f, peduncleStemOverlap);
            peduncleBaseBandFraction = Mathf.Clamp(
                peduncleBaseBandFraction,
                0.01f,
                0.25f);

            if (flowerStagePrefabs == null || flowerStagePrefabs.Length != FlowerStageCount)
            {
                System.Array.Resize(ref flowerStagePrefabs, FlowerStageCount);
            }
        }
    }
}
