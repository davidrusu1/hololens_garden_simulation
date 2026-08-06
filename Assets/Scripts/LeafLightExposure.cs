using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LeafLightExposure : MonoBehaviour
{
    private static readonly RaycastHit[] OcclusionHits = new RaycastHit[64];
    private static Light cachedPrimaryLight;
    private static int lightEvaluationBudgetFrame = -1;
    private static int lightEvaluationsThisFrame;

    [Header("Light evaluation")]
    [SerializeField, Range(0.0001f, 0.05f)]
    private float minimumIlluminatedFraction = 0.001f;

    [SerializeField, Min(0.25f)]
    private float evaluationInterval = 2f;

    [SerializeField, Range(1, 5)]
    private int darkEvaluationsBeforeShedding = 1;

    [SerializeField, Min(1f)]
    private float directionalLightRayDistance = 100f;

    [SerializeField]
    private LayerMask occlusionMask = ~0;

    [SerializeField, Min(1f)]
    [Tooltip("Virtual shadow width of every stem, expressed in visible stem thicknesses.")]
    private float stemScreenThicknessMultiplier = 5f;

    [Header("Calculated values")]
    [SerializeField]
    private float totalLeafArea;

    [SerializeField]
    private float illuminatedLeafArea;

    [SerializeField]
    private float illuminatedFraction;

    [SerializeField]
    private bool receivesLight;

    private Transform blade;
    private MeshCollider bladeCollider;
    private Vector3[] bladeVertices;
    private int[] bladeTriangles;
    private int illuminatedTriangleIndexCount;
    private int consecutiveDarkEvaluations;
    private bool shedding;
    private Branch supportingStem;

    public float TotalLeafArea => totalLeafArea;
    public float IlluminatedLeafArea => illuminatedLeafArea;
    public float IlluminatedFraction => illuminatedFraction;
    public bool ReceivesLight => receivesLight;

    public void Initialize(Transform leafBlade, float leafGrowthDuration)
    {
        blade = leafBlade;
        supportingStem = GetComponentInParent<Branch>();
        MeshFilter meshFilter = blade != null
            ? blade.GetComponent<MeshFilter>()
            : null;
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;

        if (blade == null || mesh == null)
        {
            Debug.LogError("Leaf light evaluation requires a blade mesh.", this);
            enabled = false;
            return;
        }

        bladeVertices = mesh.vertices;
        bladeTriangles = mesh.triangles;
        // The volumetric maple mesh stores the upper face first, followed by
        // its underside and edge wall. Photosynthesis is evaluated on the
        // upper blade only, so adding thickness does not double its leaf area.
        illuminatedTriangleIndexCount = mesh.name == "Procedural Maple Leaf"
            && mesh.vertexCount >= 42
                ? ((mesh.vertexCount / 2) - 1) * 3
                : bladeTriangles.Length;

        bladeCollider = blade.GetComponent<MeshCollider>();
        if (bladeCollider == null)
        {
            bladeCollider = blade.gameObject.AddComponent<MeshCollider>();
        }

        bladeCollider.sharedMesh = mesh;
        bladeCollider.convex = false;
        bladeCollider.isTrigger = false;

        float firstEvaluationDelay = Mathf.Max(0.05f, leafGrowthDuration)
            + Random.Range(0.15f, 0.45f);
        StartCoroutine(EvaluateLightRepeatedly(firstEvaluationDelay));
    }

    private IEnumerator EvaluateLightRepeatedly(float firstEvaluationDelay)
    {
        yield return new WaitForSeconds(firstEvaluationDelay);

        while (!shedding)
        {
            while (!TryAcquireLightEvaluationSlot())
            {
                yield return null;
            }

            EvaluateIlluminatedArea();

            if (receivesLight)
            {
                consecutiveDarkEvaluations = 0;
            }
            else
            {
                consecutiveDarkEvaluations++;
                if (consecutiveDarkEvaluations >= darkEvaluationsBeforeShedding)
                {
                    RemoveLeafImmediately();
                    yield break;
                }
            }

            float staggeredInterval = evaluationInterval
                * (PlantVisualQuality.LiteModeEnabled ? 3f : 1f)
                * Random.Range(0.85f, 1.15f);
            yield return new WaitForSeconds(staggeredInterval);
        }
    }

    private static bool TryAcquireLightEvaluationSlot()
    {
        if (!PlantVisualQuality.LiteModeEnabled)
        {
            return true;
        }

        if (lightEvaluationBudgetFrame != Time.frameCount)
        {
            lightEvaluationBudgetFrame = Time.frameCount;
            lightEvaluationsThisFrame = 0;
        }

        const int MaximumLiteEvaluationsPerFrame = 1;
        if (lightEvaluationsThisFrame >= MaximumLiteEvaluationsPerFrame)
        {
            return false;
        }

        lightEvaluationsThisFrame++;
        return true;
    }

    private void EvaluateIlluminatedArea()
    {
        totalLeafArea = 0f;
        illuminatedLeafArea = 0f;
        illuminatedFraction = 0f;
        receivesLight = false;

        Light primaryLight = FindPrimaryLight();
        if (primaryLight == null
            || blade == null
            || bladeVertices == null
            || bladeTriangles == null)
        {
            return;
        }

        // The procedural blade contains two triangles for every longitudinal
        // segment. Treating the pair as one sample keeps the calculation light
        // enough for many leaves while still measuring the whole blade.
        for (int triangle = 0;
            triangle + 5 < illuminatedTriangleIndexCount;
            triangle += 6)
        {
            AccumulatePatchLighting(primaryLight, triangle);
        }

        if (totalLeafArea <= 0.000001f)
        {
            return;
        }

        illuminatedFraction = illuminatedLeafArea / totalLeafArea;
        float minimumArea = Mathf.Max(
            0.000001f,
            totalLeafArea * minimumIlluminatedFraction);
        receivesLight = illuminatedLeafArea >= minimumArea;
    }

    private void AccumulatePatchLighting(Light primaryLight, int triangleStart)
    {
        Vector3 weightedCentroid = Vector3.zero;
        Vector3 weightedNormal = Vector3.zero;
        float patchArea = 0f;

        for (int offset = 0; offset < 6; offset += 3)
        {
            Vector3 a = blade.TransformPoint(
                bladeVertices[bladeTriangles[triangleStart + offset]]);
            Vector3 b = blade.TransformPoint(
                bladeVertices[bladeTriangles[triangleStart + offset + 1]]);
            Vector3 c = blade.TransformPoint(
                bladeVertices[bladeTriangles[triangleStart + offset + 2]]);

            Vector3 cross = Vector3.Cross(b - a, c - a);
            float triangleArea = cross.magnitude * 0.5f;
            if (triangleArea <= 0.0000001f)
            {
                continue;
            }

            patchArea += triangleArea;
            weightedCentroid += ((a + b + c) / 3f) * triangleArea;
            weightedNormal += cross;
        }

        if (patchArea <= 0.0000001f || weightedNormal.sqrMagnitude <= 0.0000001f)
        {
            return;
        }

        Vector3 samplePosition = weightedCentroid / patchArea;
        Vector3 sampleNormal = weightedNormal.normalized;
        totalLeafArea += patchArea;

        if (!TryGetDirectionToLight(
                primaryLight,
                samplePosition,
                out Vector3 directionToLight,
                out float rayDistance))
        {
            return;
        }

        // Leaves are rendered on both sides, so either face can intercept light.
        float incidence = Mathf.Abs(Vector3.Dot(sampleNormal, directionToLight));
        if (incidence <= 0.0001f)
        {
            return;
        }

        float surfaceOffset = Mathf.Max(0.002f, Mathf.Sqrt(patchArea) * 0.015f);
        Vector3 rayOrigin = samplePosition + directionToLight * surfaceOffset;
        if (IsOccluded(rayOrigin, directionToLight, rayDistance))
        {
            return;
        }

        illuminatedLeafArea += patchArea * incidence;
    }

    private bool IsOccluded(Vector3 origin, Vector3 direction, float distance)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            OcclusionHits,
            distance,
            occlusionMask,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < hitCount; index++)
        {
            Collider hitCollider = OcclusionHits[index].collider;
            if (hitCollider == null
                || hitCollider == bladeCollider
                || hitCollider.transform == transform
                || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            Branch hitStem = hitCollider.GetComponentInParent<Branch>();
            if (hitStem == supportingStem)
            {
                continue;
            }

            return true;
        }

        return Branch.IsLightRayBlockedByStemScreen(
            origin,
            direction,
            distance,
            supportingStem,
            stemScreenThicknessMultiplier);
    }

    private bool TryGetDirectionToLight(
        Light lightSource,
        Vector3 samplePosition,
        out Vector3 directionToLight,
        out float rayDistance)
    {
        if (lightSource.type == LightType.Directional)
        {
            directionToLight = -lightSource.transform.forward;
            rayDistance = directionalLightRayDistance;
            return true;
        }

        Vector3 lightOffset = lightSource.transform.position - samplePosition;
        float distance = lightOffset.magnitude;
        if (distance <= 0.0001f || distance > lightSource.range)
        {
            directionToLight = Vector3.up;
            rayDistance = 0f;
            return false;
        }

        if (lightSource.type == LightType.Spot)
        {
            Vector3 lightToSample = -lightOffset / distance;
            float coneLimit = Mathf.Cos(lightSource.spotAngle * 0.5f * Mathf.Deg2Rad);
            if (Vector3.Dot(lightSource.transform.forward, lightToSample) < coneLimit)
            {
                directionToLight = Vector3.up;
                rayDistance = 0f;
                return false;
            }
        }

        directionToLight = lightOffset / distance;
        rayDistance = Mathf.Max(0.001f, distance - 0.01f);
        return true;
    }

    private Light FindPrimaryLight()
    {
        if (IsUsableLight(RenderSettings.sun))
        {
            cachedPrimaryLight = RenderSettings.sun;
            return cachedPrimaryLight;
        }

        if (IsUsableLight(cachedPrimaryLight))
        {
            return cachedPrimaryLight;
        }

        Light[] lights = FindObjectsOfType<Light>();
        Light bestLight = null;
        float bestScore = -1f;
        foreach (Light candidate in lights)
        {
            if (!IsUsableLight(candidate))
            {
                continue;
            }

            float directionalBonus = candidate.type == LightType.Directional
                ? 1000f
                : 0f;
            float score = directionalBonus + candidate.intensity;
            if (score > bestScore)
            {
                bestScore = score;
                bestLight = candidate;
            }
        }

        cachedPrimaryLight = bestLight;
        return cachedPrimaryLight;
    }

    private static bool IsUsableLight(Light candidate)
    {
        return candidate != null
            && candidate.isActiveAndEnabled
            && candidate.intensity > 0.0001f;
    }

    private void RemoveLeafImmediately()
    {
        if (shedding)
        {
            return;
        }

        shedding = true;
        receivesLight = false;
        // Deactivation removes rendering, collision, LateUpdate and all
        // remaining coroutine work immediately. Destroy then releases the
        // hierarchy at the end of the current frame, without spawning a
        // Rigidbody or simulating a falling leaf.
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public void PrepareForSeasonalFall()
    {
        shedding = true;
        receivesLight = false;
        StopAllCoroutines();
        if (bladeCollider != null)
        {
            bladeCollider.enabled = false;
        }

        enabled = false;
    }

    private void OnValidate()
    {
        minimumIlluminatedFraction = Mathf.Clamp(
            minimumIlluminatedFraction,
            0.0001f,
            0.05f);
        evaluationInterval = Mathf.Max(0.25f, evaluationInterval);
        darkEvaluationsBeforeShedding = Mathf.Clamp(
            darkEvaluationsBeforeShedding,
            1,
            5);
        directionalLightRayDistance = Mathf.Max(1f, directionalLightRayDistance);
        stemScreenThicknessMultiplier = Mathf.Max(
            1f,
            stemScreenThicknessMultiplier);
    }
}
