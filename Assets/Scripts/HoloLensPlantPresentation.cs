using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class HoloLensPlantPresentation : MonoBehaviour
{
    [Header("Stable placement")]
    [SerializeField]
    private bool placeRelativeToViewerOnStart = true;

    [SerializeField, Min(0.5f)]
    private float distanceFromViewer = 2f;

    [SerializeField, Min(0f)]
    private float baseBelowEyeLevel = 0.75f;

    [Header("Orange plant pot")]
    [SerializeField]
    private bool createOrangePot = true;

    [SerializeField, Min(0.1f)]
    private float potHeight = 0.42f;

    [SerializeField, Min(0.05f)]
    private float potTopRadius = 0.34f;

    [SerializeField, Min(0.05f)]
    private float potBottomRadius = 0.25f;

    [SerializeField, Range(12, 64)]
    private int potSegments = 32;

    private Camera viewCamera;
    private GameObject potRoot;
    private Material orangePotMaterial;
    private Material soilMaterial;
    private Mesh potBodyMesh;
    private bool potVisible = true;

    public bool PlacementCompleted { get; private set; }

    private IEnumerator Start()
    {
        // Head tracking needs a couple of frames before its first reliable pose.
        yield return null;
        yield return null;

        ResolveViewCamera();
        if (placeRelativeToViewerOnStart && viewCamera != null)
        {
            PlacePlantRelativeToViewer();
        }

        if (createOrangePot)
        {
            BuildOrangePot();
        }

        PlacementCompleted = true;
    }

    private void OnDestroy()
    {
        if (potRoot != null)
        {
            Destroy(potRoot);
        }

        if (potBodyMesh != null)
        {
            Destroy(potBodyMesh);
        }

        if (orangePotMaterial != null)
        {
            Destroy(orangePotMaterial);
        }

        if (soilMaterial != null)
        {
            Destroy(soilMaterial);
        }
    }

    public void SetPotVisible(bool visible)
    {
        potVisible = visible;
        if (potRoot != null)
        {
            potRoot.SetActive(potVisible);
        }
    }

    private void ResolveViewCamera()
    {
        if (Camera.main != null
            && Camera.main.enabled
            && Camera.main.gameObject.activeInHierarchy)
        {
            viewCamera = Camera.main;
            return;
        }

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int index = 0; index < cameras.Length; index++)
        {
            Camera candidate = cameras[index];
            if (candidate.enabled && candidate.gameObject.activeInHierarchy)
            {
                viewCamera = candidate;
                return;
            }
        }
    }

    private void PlacePlantRelativeToViewer()
    {
        Transform cameraTransform = viewCamera.transform;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(
            cameraTransform.forward,
            Vector3.up);
        if (horizontalForward.sqrMagnitude < 0.001f)
        {
            horizontalForward = Vector3.forward;
        }

        horizontalForward.Normalize();
        Vector3 targetBase =
            cameraTransform.position
            + horizontalForward * distanceFromViewer
            - Vector3.up * baseBelowEyeLevel;

        transform.SetPositionAndRotation(
            targetBase,
            Quaternion.LookRotation(horizontalForward, Vector3.up));
    }

    private void BuildOrangePot()
    {
        if (potRoot != null)
        {
            return;
        }

        potRoot = new GameObject("Orange Plant Pot");
        potRoot.transform.SetParent(transform, false);

        orangePotMaterial = CreateRuntimeMaterial(
            "Runtime Orange Pot",
            new Color(0.92f, 0.31f, 0.055f, 1f),
            0.16f);
        soilMaterial = CreateRuntimeMaterial(
            "Runtime Pot Soil",
            new Color(0.16f, 0.075f, 0.035f, 1f),
            0.02f);

        CreatePotBody();
        CreatePotRimAndSoil();
        potRoot.SetActive(potVisible);
    }

    private void CreatePotBody()
    {
        GameObject bodyObject = new GameObject(
            "Tapered Orange Pot",
            typeof(MeshFilter),
            typeof(MeshRenderer));
        bodyObject.transform.SetParent(potRoot.transform, false);

        potBodyMesh = BuildFrustumMesh(
            Mathf.Max(12, potSegments),
            Mathf.Max(0.05f, potBottomRadius),
            Mathf.Max(potBottomRadius, potTopRadius),
            Mathf.Max(0.1f, potHeight));
        bodyObject.GetComponent<MeshFilter>().sharedMesh = potBodyMesh;

        MeshRenderer renderer = bodyObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = orangePotMaterial;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
    }

    private void CreatePotRimAndSoil()
    {
        const float topHeight = 0.025f;
        float rimHeight = Mathf.Clamp(potHeight * 0.12f, 0.035f, 0.065f);

        GameObject rim = CreateCylinder(
            "Orange Pot Rim",
            potRoot.transform,
            new Vector3(0f, topHeight - rimHeight * 0.5f, 0f),
            potTopRadius * 1.08f,
            rimHeight,
            orangePotMaterial);

        GameObject soil = CreateCylinder(
            "Soil",
            potRoot.transform,
            new Vector3(0f, topHeight + 0.006f, 0f),
            potTopRadius * 0.82f,
            0.018f,
            soilMaterial);

        rim.transform.localRotation = Quaternion.identity;
        soil.transform.localRotation = Quaternion.identity;
    }

    private static Mesh BuildFrustumMesh(
        int segments,
        float bottomRadius,
        float topRadius,
        float height)
    {
        int ringVertexCount = segments + 1;
        Vector3[] vertices = new Vector3[ringVertexCount * 2 + 1];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 9];
        float bottomY = 0.025f - height;
        float topY = 0.025f;

        for (int index = 0; index <= segments; index++)
        {
            float fraction = index / (float)segments;
            float angle = fraction * Mathf.PI * 2f;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);

            vertices[index] =
                new Vector3(cosine * bottomRadius, bottomY, sine * bottomRadius);
            vertices[ringVertexCount + index] =
                new Vector3(cosine * topRadius, topY, sine * topRadius);
            uv[index] = new Vector2(fraction, 0f);
            uv[ringVertexCount + index] = new Vector2(fraction, 1f);
        }

        int centerIndex = vertices.Length - 1;
        vertices[centerIndex] = new Vector3(0f, bottomY, 0f);
        uv[centerIndex] = new Vector2(0.5f, 0.5f);

        int triangleIndex = 0;
        for (int index = 0; index < segments; index++)
        {
            int bottomLeft = index;
            int bottomRight = index + 1;
            int topLeft = ringVertexCount + index;
            int topRight = ringVertexCount + index + 1;

            triangles[triangleIndex++] = bottomLeft;
            triangles[triangleIndex++] = topLeft;
            triangles[triangleIndex++] = topRight;
            triangles[triangleIndex++] = bottomLeft;
            triangles[triangleIndex++] = topRight;
            triangles[triangleIndex++] = bottomRight;

            triangles[triangleIndex++] = centerIndex;
            triangles[triangleIndex++] = bottomRight;
            triangles[triangleIndex++] = bottomLeft;
        }

        Mesh mesh = new Mesh
        {
            name = "Runtime Tapered Plant Pot"
        };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static GameObject CreateCylinder(
        string objectName,
        Transform parent,
        Vector3 localPosition,
        float radius,
        float height,
        Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(
            PrimitiveType.Cylinder);
        cylinder.name = objectName;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = localPosition;
        cylinder.transform.localScale =
            new Vector3(radius * 2f, height * 0.5f, radius * 2f);

        Collider collider = cylinder.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = cylinder.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.receiveShadows = true;
        return cylinder;
    }

    private static Material CreateRuntimeMaterial(
        string materialName,
        Color color,
        float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        return material;
    }
}
