using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class MapleStemModel : MonoBehaviour
{
    private const int RadialSegments = 12;
    private const int HeightRings = 13;
    private static Mesh sharedMapleStemMesh;
    private static Vector3[] straightVertices;

    private Mesh curvedStemMesh;
    private Vector3[] curvedVertices;
    private float lastBendDegrees = float.NegativeInfinity;
    private Vector3 lastBendDirection;
    private Vector3 lastLocalScale;
    private Vector3 lastLocalPosition;
    private Quaternion lastLocalRotation;

    public float CurrentArcBendDegrees { get; private set; }
    public float CurrentTipLateralOffset { get; private set; }
    public bool UsesCurvedMesh => curvedStemMesh != null
        && GetComponent<MeshFilter>()?.sharedMesh == curvedStemMesh;

    private void Awake()
    {
        ApplyMapleStemMesh();
    }

    private void OnEnable()
    {
        ApplyMapleStemMesh();
    }

    private void ApplyMapleStemMesh()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null)
        {
            return;
        }

        EnsureSharedMesh();
        filter.sharedMesh = sharedMapleStemMesh;
    }

    private static void EnsureSharedMesh()
    {
        if (sharedMapleStemMesh == null)
        {
            sharedMapleStemMesh = BuildMapleStemMesh();
            straightVertices = sharedMapleStemMesh.vertices;
        }
    }

    public void SetArcBend(float bendDegrees, Vector3 parentLocalBendDirection)
    {
        EnsureSharedMesh();
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null || sharedMapleStemMesh == null)
        {
            return;
        }

        float safeBendDegrees = Mathf.Clamp(bendDegrees, 0f, 24f);
        Vector3 bendDirection = Vector3.ProjectOnPlane(
            parentLocalBendDirection,
            Vector3.up);
        if (safeBendDegrees <= 0.01f
            || bendDirection.sqrMagnitude <= 0.000001f)
        {
            filter.sharedMesh = sharedMapleStemMesh;
            CurrentArcBendDegrees = 0f;
            CurrentTipLateralOffset = 0f;
            lastBendDegrees = 0f;
            lastBendDirection = Vector3.zero;
            lastLocalScale = transform.localScale;
            lastLocalPosition = transform.localPosition;
            lastLocalRotation = transform.localRotation;
            return;
        }

        bendDirection.Normalize();
        float bendThreshold = PlantVisualQuality.LiteModeEnabled
            ? 0.08f
            : 0.005f;
        float transformThresholdSquared = PlantVisualQuality.LiteModeEnabled
            ? 0.000004f
            : 0.00000001f;
        float rotationThreshold = PlantVisualQuality.LiteModeEnabled
            ? 0.05f
            : 0.005f;
        bool geometryUnchanged = Mathf.Abs(
                lastBendDegrees - safeBendDegrees) <= bendThreshold
            && Vector3.Dot(lastBendDirection, bendDirection) >= 0.9999f
            && (lastLocalScale - transform.localScale).sqrMagnitude
                <= transformThresholdSquared
            && (lastLocalPosition - transform.localPosition).sqrMagnitude
                <= transformThresholdSquared
            && Quaternion.Angle(lastLocalRotation, transform.localRotation)
                <= rotationThreshold;
        if (geometryUnchanged && filter.sharedMesh == curvedStemMesh)
        {
            return;
        }

        EnsureCurvedMesh();
        DeformAsCircularArc(safeBendDegrees, bendDirection);
        filter.sharedMesh = curvedStemMesh;
        lastBendDegrees = safeBendDegrees;
        lastBendDirection = bendDirection;
        lastLocalScale = transform.localScale;
        lastLocalPosition = transform.localPosition;
        lastLocalRotation = transform.localRotation;
    }

    private void EnsureCurvedMesh()
    {
        if (curvedStemMesh != null)
        {
            return;
        }

        curvedStemMesh = Instantiate(sharedMapleStemMesh);
        curvedStemMesh.name = "Procedural Maple Curved Stem";
        curvedStemMesh.hideFlags = HideFlags.DontSave;
        curvedStemMesh.MarkDynamic();
        curvedVertices = new Vector3[straightVertices.Length];
    }

    private void DeformAsCircularArc(
        float bendDegrees,
        Vector3 bendDirection)
    {
        float bendRadians = bendDegrees * Mathf.Deg2Rad;
        Vector3 scale = transform.localScale;
        float visibleLength = Mathf.Max(0.0001f, Mathf.Abs(scale.y) * 2f);
        float arcRadius = visibleLength / Mathf.Max(0.0001f, bendRadians);
        Vector3 bendAxis = Vector3.Cross(
            Vector3.up,
            bendDirection).normalized;
        Quaternion modelRotation = transform.localRotation;
        Quaternion inverseModelRotation = Quaternion.Inverse(modelRotation);
        Vector3 modelPosition = transform.localPosition;

        for (int index = 0; index < straightVertices.Length; index++)
        {
            Vector3 straightVertex = straightVertices[index];
            float progress = Mathf.Clamp01((straightVertex.y + 1f) * 0.5f);
            float pointAngleRadians = bendRadians * progress;
            float pointAngleDegrees = bendDegrees * progress;
            Vector3 centerlinePoint = Vector3.up
                    * (arcRadius * Mathf.Sin(pointAngleRadians))
                + bendDirection
                    * (arcRadius * (1f - Mathf.Cos(pointAngleRadians)));

            Vector3 straightRadialInModel = new Vector3(
                straightVertex.x * scale.x,
                0f,
                straightVertex.z * scale.z);
            Vector3 straightRadialInParent = modelRotation
                * straightRadialInModel;
            Vector3 curvedRadialInParent = Quaternion.AngleAxis(
                pointAngleDegrees,
                bendAxis) * straightRadialInParent;
            Vector3 parentLocalPoint = centerlinePoint
                + curvedRadialInParent;
            Vector3 scaledModelPoint = inverseModelRotation
                * (parentLocalPoint - modelPosition);

            curvedVertices[index] = new Vector3(
                SafeDivide(scaledModelPoint.x, scale.x),
                SafeDivide(scaledModelPoint.y, scale.y),
                SafeDivide(scaledModelPoint.z, scale.z));
        }

        curvedStemMesh.vertices = curvedVertices;
        curvedStemMesh.RecalculateNormals();
        curvedStemMesh.RecalculateBounds();
        CurrentArcBendDegrees = bendDegrees;
        CurrentTipLateralOffset = arcRadius
            * (1f - Mathf.Cos(bendRadians));
    }

    private static float SafeDivide(float value, float divisor)
    {
        if (Mathf.Abs(divisor) <= 0.000001f)
        {
            return 0f;
        }

        return value / divisor;
    }

    private static Mesh BuildMapleStemMesh()
    {
        int sideVertexCount = HeightRings * RadialSegments;
        var vertices = new Vector3[sideVertexCount + 2];
        var uv = new Vector2[vertices.Length];
        int sideTriangleIndexCount = (HeightRings - 1)
            * RadialSegments
            * 6;
        int capTriangleIndexCount = RadialSegments * 6;
        var triangles = new int[
            sideTriangleIndexCount + capTriangleIndexCount];

        for (int ring = 0; ring < HeightRings; ring++)
        {
            float heightProgress = ring / (float)(HeightRings - 1);
            float y = Mathf.Lerp(-1f, 1f, heightProgress);
            float taper = Mathf.Lerp(1f, 0.69f, heightProgress);
            float trunkBulge = 1f
                + Mathf.Sin(heightProgress * Mathf.PI) * 0.035f;

            for (int segment = 0; segment < RadialSegments; segment++)
            {
                float angleProgress = segment / (float)RadialSegments;
                float angle = angleProgress * Mathf.PI * 2f;
                float barkRidge = 1f
                    + Mathf.Sin(angle * 3f + heightProgress * 5.5f) * 0.025f
                    + Mathf.Sin(angle * 5f - heightProgress * 3f) * 0.012f;
                float radius = 0.5f * taper * trunkBulge * barkRidge;
                int vertex = ring * RadialSegments + segment;
                vertices[vertex] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    y,
                    Mathf.Sin(angle) * radius);
                uv[vertex] = new Vector2(angleProgress, heightProgress);
            }
        }

        int triangleIndex = 0;
        for (int ring = 0; ring < HeightRings - 1; ring++)
        {
            for (int segment = 0; segment < RadialSegments; segment++)
            {
                int nextSegment = (segment + 1) % RadialSegments;
                int lower = ring * RadialSegments + segment;
                int lowerNext = ring * RadialSegments + nextSegment;
                int upper = (ring + 1) * RadialSegments + segment;
                int upperNext = (ring + 1) * RadialSegments + nextSegment;

                triangles[triangleIndex++] = lower;
                triangles[triangleIndex++] = upper;
                triangles[triangleIndex++] = lowerNext;
                triangles[triangleIndex++] = lowerNext;
                triangles[triangleIndex++] = upper;
                triangles[triangleIndex++] = upperNext;
            }
        }

        int bottomCenter = sideVertexCount;
        int topCenter = sideVertexCount + 1;
        vertices[bottomCenter] = Vector3.down;
        vertices[topCenter] = Vector3.up;
        uv[bottomCenter] = new Vector2(0.5f, 0.5f);
        uv[topCenter] = new Vector2(0.5f, 0.5f);

        int topRingStart = (HeightRings - 1) * RadialSegments;
        for (int segment = 0; segment < RadialSegments; segment++)
        {
            int nextSegment = (segment + 1) % RadialSegments;
            triangles[triangleIndex++] = bottomCenter;
            triangles[triangleIndex++] = nextSegment;
            triangles[triangleIndex++] = segment;

            triangles[triangleIndex++] = topCenter;
            triangles[triangleIndex++] = topRingStart + segment;
            triangles[triangleIndex++] = topRingStart + nextSegment;
        }

        var mesh = new Mesh
        {
            name = "Procedural Maple Tapered Stem",
            hideFlags = HideFlags.DontSave
        };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (curvedStemMesh != null)
        {
            Destroy(curvedStemMesh);
        }
    }
}
