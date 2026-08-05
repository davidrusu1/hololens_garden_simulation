using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class Leaf : MonoBehaviour
{
    private static Material sharedLeafMaterial;
    private static Material sharedPetioleMaterial;
    private static Material sharedVeinMaterial;
    private Branch supportingBranch;
    private Vector3 horizontalGrowthDirection;
    private float downwardGrowthAngle;
    private bool orientationInitialized;
    private Plant mapleSimulation;
    private Mesh bladeMesh;
    private Mesh veinMesh;
    private float nextOrientationUpdateAt;

    public void Initialize(
        float length,
        float width,
        float stemRadius,
        float growthDuration,
        Color leafColor,
        Color stemColor)
    {
        supportingBranch = GetComponentInParent<Branch>();
        mapleSimulation = GetComponentInParent<Plant>();
        Vector3 initialDirection = transform.right.normalized;
        horizontalGrowthDirection = Vector3.ProjectOnPlane(
            initialDirection,
            Vector3.up);
        if (horizontalGrowthDirection.sqrMagnitude <= 0.00001f)
        {
            horizontalGrowthDirection = Vector3.ProjectOnPlane(
                transform.forward,
                Vector3.up);
        }

        horizontalGrowthDirection.Normalize();
        float baseAngle = -Mathf.Asin(Mathf.Clamp(
                Vector3.Dot(initialDirection, Vector3.up),
                -1f,
                1f)) * Mathf.Rad2Deg;

        downwardGrowthAngle = Mathf.Clamp(
            baseAngle + Random.Range(-10f, 15f),
            -15f,
            25f);
        orientationInitialized = true;
        float orientationPhase = Mathf.Abs(GetInstanceID() % 991) / 991f;
        nextOrientationUpdateAt = Time.time + orientationPhase * 0.2f;

        float safeLength = Mathf.Max(0.05f, length);
        float petioleLength = Mathf.Max(stemRadius * 1.5f, safeLength * 0.23f);
        float bladeStart = Mathf.Max(
            stemRadius * 0.25f,
            petioleLength
            - stemRadius * 0.35f
            - Mathf.Max(0.012f, safeLength * 0.04f));

        Transform petioleGrowth = CreatePetiole(
            petioleLength,
            safeLength,
            stemRadius,
            stemColor);
        Transform blade = CreateBlade(bladeStart, safeLength, width, leafColor);

        LeafLightExposure lightExposure = gameObject.AddComponent<LeafLightExposure>();
        lightExposure.Initialize(blade, growthDuration);

        transform.localScale = Vector3.one;
        StartCoroutine(Grow(growthDuration, petioleGrowth, blade, bladeStart));
    }

    private void LateUpdate()
    {
        if (!orientationInitialized
            || supportingBranch == null
            || !transform.IsChildOf(supportingBranch.transform))
        {
            return;
        }

        if (PlantVisualQuality.LiteModeEnabled
            && Time.time < nextOrientationUpdateAt)
        {
            return;
        }

        nextOrientationUpdateAt = Time.time
            + (PlantVisualQuality.LiteModeEnabled ? 0.2f : 0f);
        Vector3 leafDirection = (
            horizontalGrowthDirection
            - Vector3.up * Mathf.Tan(downwardGrowthAngle * Mathf.Deg2Rad))
            .normalized;
        Vector3 leafNormal = Vector3.ProjectOnPlane(
            Vector3.up,
            leafDirection).normalized;
        Vector3 leafForward = Vector3.Cross(
            leafDirection,
            leafNormal).normalized;
        transform.rotation = Quaternion.LookRotation(
            leafForward,
            leafNormal);
    }

    private Transform CreatePetiole(
        float petioleLength,
        float leafLength,
        float stemRadius,
        Color stemColor)
    {
        var growthRoot = new GameObject("Petiole Growth").transform;
        growthRoot.SetParent(transform, false);

        GameObject petiole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        petiole.name = "Leaf Petiole";
        petiole.transform.SetParent(growthRoot, false);
        petiole.transform.localPosition = Vector3.right
            * (petioleLength * 0.5f - stemRadius * 0.35f);
        petiole.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        float radius = Mathf.Max(0.012f, leafLength * 0.028f);
        petiole.transform.localScale = new Vector3(
            radius,
            petioleLength * 0.5f,
            radius);

        Collider petioleCollider = petiole.GetComponent<Collider>();
        if (petioleCollider != null)
        {
            Destroy(petioleCollider);
        }

        Renderer renderer = petiole.GetComponent<Renderer>();
        renderer.sharedMaterial = GetOrCreateMaterial(
            ref sharedPetioleMaterial,
            stemColor,
            false);
        PlantVisualQuality.ApplyRenderer(renderer);
        return growthRoot;
    }

    private Transform CreateBlade(
        float bladeStart,
        float length,
        float width,
        Color leafColor)
    {
        var blade = new GameObject("Leaf Blade");
        blade.transform.SetParent(transform, false);
        blade.transform.localPosition = Vector3.right * bladeStart;

        var filter = blade.AddComponent<MeshFilter>();
        var renderer = blade.AddComponent<MeshRenderer>();
        float bladeThickness = Mathf.Clamp(
            length * 0.018f,
            0.004f,
            0.014f);
        bladeMesh = BuildMapleLeafMesh(
            length,
            width,
            bladeThickness);
        filter.sharedMesh = bladeMesh;
        renderer.sharedMaterial = GetOrCreateMaterial(
            ref sharedLeafMaterial,
            leafColor,
            true);
        veinMesh = CreateMapleVeins(
            blade.transform,
            length,
            width,
            bladeThickness,
            Color.Lerp(leafColor, new Color(0.025f, 0.12f, 0.035f, 1f), 0.55f));
        PlantVisualQuality.ApplyRenderer(renderer);
        return blade.transform;
    }

    private static Mesh BuildMapleLeafMesh(
        float length,
        float width,
        float thickness)
    {
        // Twenty contour points form the five characteristic maple lobes.
        // The alternating tips and sinuses also give the edge a lightly
        // serrated silhouette without requiring a texture.
        Vector2[] normalizedOutline =
        {
            new Vector2(0.00f, 0.00f),
            new Vector2(0.12f, -0.08f),
            new Vector2(0.20f, -0.32f),
            new Vector2(0.29f, -0.24f),
            new Vector2(0.39f, -0.19f),
            new Vector2(0.50f, -0.34f),
            new Vector2(0.68f, -0.50f),
            new Vector2(0.65f, -0.30f),
            new Vector2(0.66f, -0.16f),
            new Vector2(0.82f, -0.20f),
            new Vector2(1.00f, 0.00f),
            new Vector2(0.82f, 0.20f),
            new Vector2(0.66f, 0.16f),
            new Vector2(0.65f, 0.30f),
            new Vector2(0.68f, 0.50f),
            new Vector2(0.50f, 0.34f),
            new Vector2(0.39f, 0.19f),
            new Vector2(0.29f, 0.24f),
            new Vector2(0.20f, 0.32f),
            new Vector2(0.12f, 0.08f)
        };

        int outlineCount = normalizedOutline.Length;
        int topCenter = 0;
        int topOutlineStart = 1;
        int bottomCenter = outlineCount + 1;
        int bottomOutlineStart = bottomCenter + 1;
        var vertices = new Vector3[(outlineCount + 1) * 2];
        var uv = new Vector2[vertices.Length];
        int faceTriangleIndices = outlineCount * 3;
        int edgeTriangleIndices = outlineCount * 6;
        var triangles = new int[
            faceTriangleIndices * 2 + edgeTriangleIndices];
        float halfThickness = thickness * 0.5f;

        Vector3 centerSurface = new Vector3(
            length * 0.46f,
            GetLeafSurfaceHeight(length, width, length * 0.46f, 0f),
            0f);
        vertices[topCenter] = centerSurface + Vector3.up * halfThickness;
        vertices[bottomCenter] = centerSurface - Vector3.up * halfThickness;
        uv[topCenter] = new Vector2(0.46f, 0.5f);
        uv[bottomCenter] = uv[topCenter];

        for (int point = 0; point < outlineCount; point++)
        {
            Vector2 outlinePoint = normalizedOutline[point];
            Vector3 surfacePoint = new Vector3(
                outlinePoint.x * length,
                GetLeafSurfaceHeight(
                    length,
                    width,
                    outlinePoint.x * length,
                    outlinePoint.y * width),
                outlinePoint.y * width);
            vertices[topOutlineStart + point] = surfacePoint
                + Vector3.up * halfThickness;
            vertices[bottomOutlineStart + point] = surfacePoint
                - Vector3.up * halfThickness;
            Vector2 pointUv = new Vector2(
                outlinePoint.x,
                outlinePoint.y + 0.5f);
            uv[topOutlineStart + point] = pointUv;
            uv[bottomOutlineStart + point] = pointUv;

            int nextPoint = (point + 1) % outlineCount;
            int triangle = point * 3;
            triangles[triangle] = topCenter;
            // Clockwise in X/Z would point the normal down. Reverse the fan
            // so the maple blade receives sunlight on its upper face.
            triangles[triangle + 1] = topOutlineStart + nextPoint;
            triangles[triangle + 2] = topOutlineStart + point;

            int bottomTriangle = faceTriangleIndices + triangle;
            triangles[bottomTriangle] = bottomCenter;
            triangles[bottomTriangle + 1] = bottomOutlineStart + point;
            triangles[bottomTriangle + 2] = bottomOutlineStart + nextPoint;

            int edgeTriangle = faceTriangleIndices * 2 + point * 6;
            int topPoint = topOutlineStart + point;
            int topNext = topOutlineStart + nextPoint;
            int bottomPoint = bottomOutlineStart + point;
            int bottomNext = bottomOutlineStart + nextPoint;
            triangles[edgeTriangle] = topPoint;
            triangles[edgeTriangle + 1] = topNext;
            triangles[edgeTriangle + 2] = bottomPoint;
            triangles[edgeTriangle + 3] = bottomPoint;
            triangles[edgeTriangle + 4] = topNext;
            triangles[edgeTriangle + 5] = bottomNext;
        }

        var mesh = new Mesh { name = "Procedural Maple Leaf" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float GetLeafSurfaceHeight(
        float length,
        float width,
        float x,
        float z)
    {
        float lengthProgress = Mathf.Clamp01(x / Mathf.Max(0.001f, length));
        float halfWidth = Mathf.Max(0.001f, width * 0.5f);
        float sideProgress = Mathf.Clamp01(Mathf.Abs(z) / halfWidth);

        // The midrib follows a true circular arc. Its tangent becomes gently
        // steeper toward the tip, suggesting the blade's own weight rather
        // than folding the whole leaf along one straight line.
        const float DownwardArcDegrees = 20f;
        float arcRadians = DownwardArcDegrees * Mathf.Deg2Rad;
        float arcRadius = length / Mathf.Max(0.001f, Mathf.Sin(arcRadians));
        float clampedX = Mathf.Min(
            Mathf.Abs(x),
            arcRadius - 0.0001f);
        float circularSag = Mathf.Sqrt(
            Mathf.Max(0f, arcRadius * arcRadius - clampedX * clampedX))
            - arcRadius;
        float sideCup = -length
            * 0.022f
            * sideProgress
            * sideProgress
            * Mathf.Sin(lengthProgress * Mathf.PI);
        return circularSag + sideCup;
    }

    private static Mesh CreateMapleVeins(
        Transform blade,
        float length,
        float width,
        float bladeThickness,
        Color veinColor)
    {
        var veinObject = new GameObject("Maple Leaf Veins");
        veinObject.transform.SetParent(blade, false);
        veinObject.transform.localPosition = Vector3.up
            * (bladeThickness * 0.5f + 0.002f);

        var filter = veinObject.AddComponent<MeshFilter>();
        var renderer = veinObject.AddComponent<MeshRenderer>();
        Vector3 veinOrigin = new Vector3(
            length * 0.025f,
            GetLeafSurfaceHeight(length, width, length * 0.025f, 0f),
            0f);
        Vector3[] veinTips =
        {
            CreateSurfacePoint(length, width, 0.97f, 0f),
            CreateSurfacePoint(length, width, 0.67f, -0.48f),
            CreateSurfacePoint(length, width, 0.67f, 0.48f),
            CreateSurfacePoint(length, width, 0.20f, -0.30f),
            CreateSurfacePoint(length, width, 0.20f, 0.30f)
        };
        float halfVeinWidth = Mathf.Max(0.0012f, length * 0.0045f);
        var vertices = new Vector3[veinTips.Length * 4];
        var triangles = new int[veinTips.Length * 6];

        for (int vein = 0; vein < veinTips.Length; vein++)
        {
            Vector3 direction = veinTips[vein] - veinOrigin;
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized
                * halfVeinWidth;
            int vertex = vein * 4;
            vertices[vertex] = veinOrigin - side;
            vertices[vertex + 1] = veinOrigin + side;
            vertices[vertex + 2] = veinTips[vein] - side * 0.35f;
            vertices[vertex + 3] = veinTips[vein] + side * 0.35f;

            int triangle = vein * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex + 1;
            triangles[triangle + 4] = vertex + 3;
            triangles[triangle + 5] = vertex + 2;
        }

        var mesh = new Mesh
        {
            name = "Procedural Maple Leaf Veins",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = GetOrCreateMaterial(
            ref sharedVeinMaterial,
            veinColor,
            true);
        PlantVisualQuality.ApplyRenderer(renderer);
        return mesh;
    }

    private static Vector3 CreateSurfacePoint(
        float length,
        float width,
        float normalizedX,
        float normalizedZ)
    {
        float x = length * normalizedX;
        float z = width * normalizedZ;
        return new Vector3(
            x,
            GetLeafSurfaceHeight(length, width, x, z) + 0.003f,
            z);
    }

    private static Material GetOrCreateMaterial(
        ref Material material,
        Color color,
        bool illuminateBothSides)
    {
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader)
            {
                name = "Runtime Plant Material",
                hideFlags = HideFlags.DontSave
            };
            material.SetFloat("_Cull", 0f);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (illuminateBothSides && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(
                "_EmissionColor",
                new Color(
                    color.r * 0.22f,
                    color.g * 0.22f,
                    color.b * 0.22f,
                    color.a));
        }

        return material;
    }

    private IEnumerator Grow(
        float duration,
        Transform petioleGrowth,
        Transform blade,
        float bladeFinalPosition)
    {
        float safeDuration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        float lastVisualGrowthUpdateAt = Time.time;
        float nextVisualGrowthUpdateAt = Time.time;

        petioleGrowth.localScale = new Vector3(0.01f, 1f, 1f);
        blade.localScale = new Vector3(0.01f, 0.18f, 0.01f);
        blade.localPosition = Vector3.zero;

        while (elapsed < safeDuration)
        {
            if (PlantVisualQuality.LiteModeEnabled
                && Time.time < nextVisualGrowthUpdateAt)
            {
                yield return null;
                continue;
            }

            float elapsedVisualTime = PlantVisualQuality.LiteModeEnabled
                ? Mathf.Max(
                    Time.deltaTime,
                    Time.time - lastVisualGrowthUpdateAt)
                : Time.deltaTime;
            lastVisualGrowthUpdateAt = Time.time;
            nextVisualGrowthUpdateAt = Time.time
                + (PlantVisualQuality.LiteModeEnabled ? 0.08f : 0f);
            float playbackSpeed = mapleSimulation != null
                ? Mathf.Max(0.25f, mapleSimulation.PlaybackSpeed)
                : 1f;
            elapsed += elapsedVisualTime * playbackSpeed;
            float progress = Mathf.Clamp01(elapsed / safeDuration);

            float petioleProgress = Mathf.Clamp01(progress / 0.42f);
            float smoothPetiole = petioleProgress
                * petioleProgress
                * (3f - 2f * petioleProgress);
            petioleGrowth.localScale = new Vector3(
                Mathf.Lerp(0.01f, 1f, smoothPetiole),
                1f,
                1f);

            float bladeProgress = Mathf.Clamp01((progress - 0.24f) / 0.76f);
            float smoothBlade = bladeProgress
                * bladeProgress
                * (3f - 2f * bladeProgress);
            blade.localPosition = Vector3.right
                * (bladeFinalPosition * smoothPetiole);
            blade.localScale = new Vector3(
                Mathf.Lerp(0.01f, 1f, smoothBlade),
                Mathf.Lerp(0.18f, 1f, smoothBlade),
                Mathf.Lerp(0.01f, 1f, smoothBlade));
            yield return null;
        }

        petioleGrowth.localScale = Vector3.one;
        blade.localPosition = Vector3.right * bladeFinalPosition;
        blade.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        Branch.InvalidateSupportedWeightCache();
        if (bladeMesh != null)
        {
            Destroy(bladeMesh);
        }

        if (veinMesh != null)
        {
            Destroy(veinMesh);
        }
    }
}
