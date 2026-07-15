using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ICI.PlantGrowth
{
    [DisallowMultipleComponent]
    public sealed class LeafGrowthController : MonoBehaviour
    {
        private const int LeafStageCount = 3;

        [Header("Leaf models (stage 1 to stage 3)")]
        [SerializeField]
        private GameObject[] leafStagePrefabs = new GameObject[LeafStageCount];

        [Header("Leaf placement")]
        [SerializeField, Min(1)]
        private int leavesPerStemSegment = 2;

        [SerializeField, Range(0f, 89f)]
        [Tooltip("Optional upward tilt measured from a perfectly horizontal leaf.")]
        private float leafAngle = 0f;

        [SerializeField, Min(0f)]
        private float verticalSpawnMargin = 0.12f;

        [SerializeField]
        [Tooltip("Moves each leaf joint down so its petiole overlaps the supporting stem piece.")]
        private float leafConnectionHeightOffset = -0.04f;

        [SerializeField, Min(0f)]
        [Tooltip("Extends the petiole into the leaf blade so no scale or rendering gap remains.")]
        private float leafPetioleOverlap = 0.025f;

        [SerializeField, Range(1f, 2f)]
        [Tooltip("Slightly reinforces the petiole at the connection with the leaf blade.")]
        private float leafPetioleThicknessMultiplier = 1.15f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Places the petiole base inside the main stem. One starts it on the stem axis and guarantees there is no floating gap.")]
        private float leafStemInsertionRatio = 1f;

        [SerializeField, Range(0.01f, 0.25f)]
        [Tooltip("Fraction of the petiole tip and leaf base used to align their connection axes.")]
        private float leafConnectionBandFraction = 0.08f;

        [SerializeField, Range(0f, 45f)]
        private float azimuthVariation = 12f;

        [SerializeField, Min(1)]
        private int maximumLeafCount = 12;

        [Header("Leaf timing")]
        [SerializeField, Min(0f)]
        private float delayBetweenLeaves = 1f;

        [SerializeField, Min(0.05f)]
        private float leafGrowthDuration = 3f;

        [SerializeField, Min(0f)]
        private float delayBetweenLeafStages = 0.75f;

        private readonly List<GameObject> leafPivots = new List<GameObject>();
        private Transform growthParent;
        private int createdLeafCount;
        private Material[] stemMaterials;

        public int CreatedLeafCount => createdLeafCount;

        public void Initialize(Transform parent)
        {
            growthParent = parent != null ? parent : transform;
        }

        public void SetStemMaterials(Material[] materials)
        {
            stemMaterials = materials;
        }

        public void SpawnLeavesForCompletedStem(
            float stemBaseHeight,
            float stemTopHeight,
            float stemRadius)
        {
            if (!HasAllLeafPrefabs() || createdLeafCount >= maximumLeafCount)
            {
                return;
            }

            if (growthParent == null)
            {
                growthParent = transform;
            }

            float availableHeight = stemTopHeight - stemBaseHeight;
            float margin = Mathf.Min(verticalSpawnMargin, availableHeight * 0.25f);
            float lowerHeight = stemBaseHeight + margin;
            float upperHeight = stemTopHeight - margin;
            if (upperHeight <= lowerHeight)
            {
                return;
            }

            int count = Mathf.Min(
                leavesPerStemSegment,
                maximumLeafCount - createdLeafCount);
            float firstAzimuth = Random.Range(0f, 360f);

            for (int index = 0; index < count; index++)
            {
                float heightFraction = (index + 1f) / (count + 1f);
                float leafHeight = Mathf.Lerp(lowerHeight, upperHeight, heightFraction);
                float evenlySpacedAzimuth = firstAzimuth + index * (360f / count);
                float azimuth = evenlySpacedAzimuth
                                + Random.Range(-azimuthVariation, azimuthVariation);
                float delay = index * delayBetweenLeaves;

                StartCoroutine(GrowLeafAtJoint(
                    leafHeight,
                    azimuth,
                    Mathf.Max(stemRadius, 0.001f),
                    delay));
                createdLeafCount++;
            }
        }

        public void ResetLeaves()
        {
            StopAllCoroutines();

            foreach (GameObject pivot in leafPivots)
            {
                if (pivot != null)
                {
                    Destroy(pivot);
                }
            }

            leafPivots.Clear();
            createdLeafCount = 0;
        }

        private IEnumerator GrowLeafAtJoint(
            float height,
            float azimuth,
            float stemRadius,
            float initialDelay)
        {
            if (initialDelay > 0f)
            {
                yield return new WaitForSeconds(initialDelay);
            }

            var pivot = new GameObject($"Leaf Joint {createdLeafCount}");
            pivot.transform.SetParent(growthParent, false);

            Quaternion aroundStem = Quaternion.Euler(0f, azimuth, 0f);
            Vector3 radialDirection = aroundStem * Vector3.right;
            float radialDistance = stemRadius * (1f - leafStemInsertionRatio);
            pivot.transform.localPosition = new Vector3(
                radialDirection.x * radialDistance,
                height + leafConnectionHeightOffset,
                radialDirection.z * radialDistance);
            OrientLeafHorizontally(pivot.transform, radialDirection);
            leafPivots.Add(pivot);

            GameObject activeLeaf = null;
            float previousSize = 0f;

            for (int stage = 0; stage < LeafStageCount; stage++)
            {
                if (activeLeaf != null)
                {
                    Destroy(activeLeaf);
                }

                activeLeaf = Instantiate(leafStagePrefabs[stage], pivot.transform, false);
                activeLeaf.name = $"Leaf Stage {stage + 1}";
                CopyPrefabTransform(activeLeaf.transform, leafStagePrefabs[stage].transform);
                ReinforceLeafConnection(activeLeaf);
                ApplyStemColorToPetiole(activeLeaf);

                Vector3 targetScale = activeLeaf.transform.localScale;
                float naturalSize = Mathf.Max(MeasureLeafSize(activeLeaf), 0.0001f);
                float continuityRatio = previousSize > 0f
                    ? previousSize / naturalSize
                    : 0.05f;
                float targetRatio = Mathf.Max(1f, continuityRatio);
                Vector3 startScale = targetScale * Mathf.Min(continuityRatio, targetRatio);
                Vector3 finalScale = targetScale * targetRatio;
                activeLeaf.transform.localScale = startScale;

                float elapsed = 0f;
                while (elapsed < leafGrowthDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / leafGrowthDuration);
                    activeLeaf.transform.localScale = Vector3.Lerp(
                        startScale,
                        finalScale,
                        Mathf.SmoothStep(0f, 1f, progress));
                    yield return null;
                }

                activeLeaf.transform.localScale = finalScale;
                previousSize = MeasureLeafSize(activeLeaf);

                if (stage < LeafStageCount - 1 && delayBetweenLeafStages > 0f)
                {
                    yield return new WaitForSeconds(delayBetweenLeafStages);
                }
            }
        }

        private void OrientLeafHorizontally(Transform pivot, Vector3 radialDirection)
        {
            Camera activeCamera = Camera.main;
            Vector3 localDirectionToCamera;

            if (activeCamera != null)
            {
                Vector3 worldDirectionToCamera = activeCamera.transform.position - pivot.position;
                worldDirectionToCamera.y = 0f;
                localDirectionToCamera = growthParent.InverseTransformDirection(
                    worldDirectionToCamera.normalized);
            }
            else
            {
                localDirectionToCamera = Vector3.back;
            }

            if (localDirectionToCamera.sqrMagnitude <= 0.0001f)
            {
                localDirectionToCamera = Vector3.back;
            }

            // The blade lies in its local XY plane. Local Z faces the camera and
            // local Y follows the camera's horizontal axis, so it never appears
            // rolled or edge-on in the plant view.
            Vector3 horizontalDirection = Vector3.Cross(
                Vector3.up,
                localDirectionToCamera).normalized;
            if (Vector3.Dot(horizontalDirection, radialDirection) < 0f)
            {
                horizontalDirection = -horizontalDirection;
            }

            pivot.localRotation = Quaternion.LookRotation(
                localDirectionToCamera,
                horizontalDirection)
                * Quaternion.Euler(leafAngle, 0f, 0f);
        }

        private void ApplyStemColorToPetiole(GameObject leaf)
        {
            if (stemMaterials == null || stemMaterials.Length == 0)
            {
                return;
            }

            foreach (Renderer leafRenderer in leaf.GetComponentsInChildren<Renderer>(true))
            {
                if (leafRenderer.name.Contains("Petiole"))
                {
                    leafRenderer.sharedMaterials = stemMaterials;
                }
            }
        }

        private void ReinforceLeafConnection(GameObject leaf)
        {
            MeshFilter petioleMesh = null;
            MeshFilter bladeMesh = null;

            foreach (MeshFilter meshFilter in leaf.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.name.Contains("Petiole"))
                {
                    petioleMesh = meshFilter;
                }
                else if (meshFilter.name.Contains("Serrated Leaf Blade"))
                {
                    bladeMesh = meshFilter;
                }
            }

            if (petioleMesh == null || petioleMesh.sharedMesh == null)
            {
                return;
            }

            Transform petiole = petioleMesh.transform;
            Vector3 petioleScale = petiole.localScale;
            petioleScale.x *= leafPetioleThicknessMultiplier;
            petioleScale.z *= leafPetioleThicknessMultiplier;
            petiole.localScale = petioleScale;

            if (bladeMesh == null
                || bladeMesh.sharedMesh == null)
            {
                return;
            }

            AlignLeafBladeWithPetiole(leaf, petioleMesh, bladeMesh);

            if (!TryGetMeshYRange(petioleMesh, leaf.transform, out _, out float petioleTop)
                || !TryGetMeshYRange(bladeMesh, leaf.transform, out float bladeBase, out _))
            {
                return;
            }

            float requiredPetioleTop = bladeBase + leafPetioleOverlap;
            float missingOverlap = requiredPetioleTop - petioleTop;
            if (missingOverlap <= 0f)
            {
                return;
            }

            float petioleOrigin = leaf.transform
                .InverseTransformPoint(petiole.position).y;
            float lengthFromOrigin = petioleTop - petioleOrigin;
            if (lengthFromOrigin <= 0.0001f)
            {
                return;
            }

            petioleScale = petiole.localScale;
            petioleScale.y *= 1f + missingOverlap / lengthFromOrigin;
            petiole.localScale = petioleScale;
        }

        private void AlignLeafBladeWithPetiole(
            GameObject leaf,
            MeshFilter petioleMesh,
            MeshFilter bladeMesh)
        {
            if (!TryGetMeshBandCenter(
                    petioleMesh,
                    leaf.transform,
                    true,
                    out Vector3 petioleTipCenter)
                || !TryGetMeshBandCenter(
                    bladeMesh,
                    leaf.transform,
                    false,
                    out Vector3 bladeBaseCenter))
            {
                return;
            }

            Vector3 alignmentOffset = petioleTipCenter - bladeBaseCenter;
            alignmentOffset.y = 0f;

            MoveTransformInReference(
                bladeMesh.transform,
                leaf.transform,
                alignmentOffset);

            foreach (Transform child in leaf.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("Attached Vein System"))
                {
                    MoveTransformInReference(
                        child,
                        leaf.transform,
                        alignmentOffset);
                    break;
                }
            }
        }

        private bool TryGetMeshBandCenter(
            MeshFilter meshFilter,
            Transform reference,
            bool topBand,
            out Vector3 center)
        {
            center = Vector3.zero;
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            Vector3[] vertices = meshFilter.sharedMesh.vertices;
            if (vertices.Length == 0)
            {
                return false;
            }

            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            foreach (Vector3 vertex in vertices)
            {
                float y = reference.InverseTransformPoint(
                    meshFilter.transform.TransformPoint(vertex)).y;
                minimumY = Mathf.Min(minimumY, y);
                maximumY = Mathf.Max(maximumY, y);
            }

            float bandHeight = Mathf.Max(
                (maximumY - minimumY) * leafConnectionBandFraction,
                0.0001f);
            float bandMinimum = topBand ? maximumY - bandHeight : minimumY;
            float bandMaximum = topBand ? maximumY : minimumY + bandHeight;
            Vector3 minimum = new Vector3(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);
            Vector3 maximum = new Vector3(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);

            foreach (Vector3 vertex in vertices)
            {
                Vector3 point = reference.InverseTransformPoint(
                    meshFilter.transform.TransformPoint(vertex));
                if (point.y < bandMinimum || point.y > bandMaximum)
                {
                    continue;
                }

                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }

            if (float.IsInfinity(minimum.x))
            {
                return false;
            }

            center = (minimum + maximum) * 0.5f;
            return true;
        }

        private static void MoveTransformInReference(
            Transform target,
            Transform reference,
            Vector3 localOffset)
        {
            Vector3 positionInReference = reference.InverseTransformPoint(target.position);
            target.position = reference.TransformPoint(positionInReference + localOffset);
        }

        private static bool TryGetMeshYRange(
            MeshFilter meshFilter,
            Transform reference,
            out float minimumY,
            out float maximumY)
        {
            minimumY = float.PositiveInfinity;
            maximumY = float.NegativeInfinity;

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            foreach (Vector3 vertex in meshFilter.sharedMesh.vertices)
            {
                float y = reference.InverseTransformPoint(
                    meshFilter.transform.TransformPoint(vertex)).y;
                minimumY = Mathf.Min(minimumY, y);
                maximumY = Mathf.Max(maximumY, y);
            }

            return !float.IsInfinity(minimumY) && !float.IsInfinity(maximumY);
        }

        private static float MeasureLeafSize(GameObject leaf)
        {
            bool hasBounds = false;
            Bounds bounds = default;

            foreach (Renderer leafRenderer in leaf.GetComponentsInChildren<Renderer>())
            {
                if (!hasBounds)
                {
                    bounds = leafRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(leafRenderer.bounds);
                }
            }

            return hasBounds ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : 0f;
        }

        private bool HasAllLeafPrefabs()
        {
            if (leafStagePrefabs == null || leafStagePrefabs.Length != LeafStageCount)
            {
                return false;
            }

            foreach (GameObject prefab in leafStagePrefabs)
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
            leavesPerStemSegment = Mathf.Max(1, leavesPerStemSegment);
            verticalSpawnMargin = Mathf.Max(0f, verticalSpawnMargin);
            leafPetioleOverlap = Mathf.Max(0f, leafPetioleOverlap);
            leafPetioleThicknessMultiplier = Mathf.Clamp(
                leafPetioleThicknessMultiplier,
                1f,
                2f);
            leafStemInsertionRatio = Mathf.Clamp01(leafStemInsertionRatio);
            leafConnectionBandFraction = Mathf.Clamp(
                leafConnectionBandFraction,
                0.01f,
                0.25f);
            maximumLeafCount = Mathf.Max(1, maximumLeafCount);
            delayBetweenLeaves = Mathf.Max(0f, delayBetweenLeaves);
            leafGrowthDuration = Mathf.Max(0.05f, leafGrowthDuration);
            delayBetweenLeafStages = Mathf.Max(0f, delayBetweenLeafStages);

            if (leafStagePrefabs == null || leafStagePrefabs.Length != LeafStageCount)
            {
                System.Array.Resize(ref leafStagePrefabs, LeafStageCount);
            }
        }
    }
}
