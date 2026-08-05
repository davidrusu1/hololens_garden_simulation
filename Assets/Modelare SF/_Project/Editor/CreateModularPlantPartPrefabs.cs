using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CreateModularPlantPartPrefabs
{
    private const string MaterialsFolder = "Assets/_Project/Materials";
    private const string ModelsFolder = "Assets/_Project/Models";
    private const string PrefabsFolder = "Assets/_Project/Prefabs";

    [MenuItem("Tools/Plant AR/Create Modular Plant Part Prefabs")]
    public static void CreateAll()
    {
        EnsureFolder("Assets", "_Project");
        EnsureFolder("Assets/_Project", "Materials");
        EnsureFolder("Assets/_Project", "Models");
        EnsureFolder("Assets/_Project", "Prefabs");

        var stemMaterial = GetOrCreateMaterial("MAT_ModularStem_Green", new Color(0.18f, 0.55f, 0.20f));
        var leafMaterial = GetOrCreateMaterial("MAT_ModularLeaf_Surface", new Color(0.16f, 0.58f, 0.22f));
        var veinMaterial = GetOrCreateMaterial("MAT_ModularLeaf_Veins", new Color(0.08f, 0.34f, 0.12f));
        var petalMaterial = GetOrCreateMaterial("MAT_ModularFlower_Petals", new Color(0.95f, 0.36f, 0.62f));
        var centerMaterial = GetOrCreateMaterial("MAT_ModularFlower_Center", new Color(1.00f, 0.77f, 0.10f));

        CreateThinStem(stemMaterial);
        CreateGenericLeaf(leafMaterial, veinMaterial);
        CreateFivePetalFlower(petalMaterial, centerMaterial, stemMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var flowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsFolder + "/SimpleFivePetalFlower.prefab");
        Selection.activeObject = flowerPrefab;
        EditorGUIUtility.PingObject(flowerPrefab);
        Debug.Log("Created modular plant part prefabs in " + PrefabsFolder);
    }

    private static void CreateThinStem(Material stemMaterial)
    {
        var root = new GameObject("ThinGreenStem");
        CreateTaperedCylinder(
            "Stem Tapered Cylinder",
            root.transform,
            new Vector3(0f, 0f, 0f),
            new Vector3(0.012f, 1f, 0f),
            0.034f,
            0.016f,
            stemMaterial);
        SavePrefabAndDestroy(root, PrefabsFolder + "/ThinGreenStem.prefab");
    }

    private static void CreateGenericLeaf(Material leafMaterial, Material veinMaterial)
    {
        var root = new GameObject("GenericLeafWithVeins");
        var leafSurface = new GameObject("Leaf Surface");
        leafSurface.transform.SetParent(root.transform, false);

        var mesh = GetOrCreateLeafMesh();
        var meshFilter = leafSurface.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var meshRenderer = leafSurface.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = leafMaterial;

        CreateTaperedCylinder(
            "Central Vein",
            root.transform,
            LeafCenterPoint(0.07f, -0.034f),
            LeafCenterPoint(0.95f, -0.034f),
            0.012f,
            0.004f,
            veinMaterial);

        for (var i = 1; i <= 5; i++)
        {
            var t = 0.18f + (i * 0.12f);
            var anchor = LeafCenterPoint(t, -0.038f);
            var leftEnd = LeafEdgePoint(t + 0.075f, -0.78f, -0.038f);
            var rightEnd = LeafEdgePoint(t + 0.075f, 0.78f, -0.038f);

            CreateTaperedCylinder("Left Vein " + i, root.transform, anchor, leftEnd, 0.006f, 0.002f, veinMaterial);
            CreateTaperedCylinder("Right Vein " + i, root.transform, anchor, rightEnd, 0.006f, 0.002f, veinMaterial);
        }

        CreateLeafOutline(root.transform, veinMaterial);

        SavePrefabAndDestroy(root, PrefabsFolder + "/GenericLeafWithVeins.prefab");
    }

    private static void CreateFivePetalFlower(Material petalMaterial, Material centerMaterial, Material stemMaterial)
    {
        var root = new GameObject("SimpleFivePetalFlower");

        for (var i = 0; i < 5; i++)
        {
            var angle = 90f + (i * 72f);
            var radians = angle * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);

            var petal = CreatePrimitive("Petal " + (i + 1), PrimitiveType.Sphere, root.transform, petalMaterial);
            petal.transform.localPosition = direction * 0.13f;
            petal.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            petal.transform.localScale = new Vector3(0.16f, 0.30f, 0.035f);
        }

        var center = CreatePrimitive("Flower Center", PrimitiveType.Sphere, root.transform, centerMaterial);
        center.transform.localScale = new Vector3(0.15f, 0.15f, 0.06f);

        CreateTaperedCylinder(
            "Short Flower Stem",
            root.transform,
            new Vector3(0f, -0.44f, 0f),
            new Vector3(0f, -0.05f, 0f),
            0.026f,
            0.014f,
            stemMaterial);
        SavePrefabAndDestroy(root, PrefabsFolder + "/SimpleFivePetalFlower.prefab");
    }

    private static Mesh GetOrCreateLeafMesh()
    {
        const string meshPath = ModelsFolder + "/MSH_GenericLeafSurface.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = "MSH_GenericLeafSurface"
            };
            AssetDatabase.CreateAsset(mesh, meshPath);
        }

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const int rows = 18;
        const int columns = 10;

        for (var row = 0; row <= rows; row++)
        {
            var t = row / (float)rows;

            for (var column = 0; column <= columns; column++)
            {
                var side = Mathf.Lerp(-1f, 1f, column / (float)columns);
                var center = LeafSurfacePoint(t, side, 0f);
                var thickness = LeafHalfThickness(t, side);
                vertices.Add(new Vector3(center.x, center.y, -thickness));
            }
        }

        for (var row = 0; row <= rows; row++)
        {
            var t = row / (float)rows;

            for (var column = 0; column <= columns; column++)
            {
                var side = Mathf.Lerp(-1f, 1f, column / (float)columns);
                var center = LeafSurfacePoint(t, side, 0f);
                var thickness = LeafHalfThickness(t, side);
                vertices.Add(new Vector3(center.x, center.y, thickness));
            }
        }

        var rowSize = columns + 1;
        var bottomOffset = (rows + 1) * rowSize;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var a = row * rowSize + column;
                var b = a + 1;
                var c = (row + 1) * rowSize + column;
                var d = c + 1;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);

                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);

                triangles.Add(b + bottomOffset);
                triangles.Add(c + bottomOffset);
                triangles.Add(a + bottomOffset);

                triangles.Add(d + bottomOffset);
                triangles.Add(c + bottomOffset);
                triangles.Add(b + bottomOffset);
            }
        }

        for (var row = 0; row < rows; row++)
        {
            AddLeafSideWall(triangles, row * rowSize, (row + 1) * rowSize, bottomOffset);
            AddLeafSideWall(triangles, row * rowSize + columns, (row + 1) * rowSize + columns, bottomOffset);
        }

        for (var column = 0; column < columns; column++)
        {
            AddLeafSideWall(triangles, column, column + 1, bottomOffset);
            AddLeafSideWall(triangles, rows * rowSize + column, rows * rowSize + column + 1, bottomOffset);
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);

        return mesh;
    }

    private static void CreateLeafOutline(Transform root, Material veinMaterial)
    {
        const int outlineSegments = 9;

        for (var i = 0; i < outlineSegments; i++)
        {
            var t0 = i / (float)outlineSegments;
            var t1 = (i + 1) / (float)outlineSegments;

            CreateTaperedCylinder(
                "Leaf Outline Left " + (i + 1),
                root,
                LeafEdgePoint(t0, -1f, -0.042f),
                LeafEdgePoint(t1, -1f, -0.042f),
                0.005f,
                0.003f,
                veinMaterial);

            CreateTaperedCylinder(
                "Leaf Outline Right " + (i + 1),
                root,
                LeafEdgePoint(t0, 1f, -0.042f),
                LeafEdgePoint(t1, 1f, -0.042f),
                0.005f,
                0.003f,
                veinMaterial);
        }
    }

    private static void AddLeafSideWall(List<int> triangles, int topA, int topB, int bottomOffset)
    {
        var bottomA = topA + bottomOffset;
        var bottomB = topB + bottomOffset;

        triangles.Add(topA);
        triangles.Add(topB);
        triangles.Add(bottomA);

        triangles.Add(topB);
        triangles.Add(bottomB);
        triangles.Add(bottomA);
    }

    private static Vector3 LeafCenterPoint(float t, float z)
    {
        t = Mathf.Clamp01(t);
        return new Vector3(LeafCenterX(t), t * 0.76f, z);
    }

    private static Vector3 LeafEdgePoint(float t, float side, float z)
    {
        var point = LeafSurfacePoint(t, side, z);
        return point;
    }

    private static Vector3 LeafSurfacePoint(float t, float side, float z)
    {
        t = Mathf.Clamp01(t);
        var wave = LeafWave(t);
        var halfWidth = Mathf.Pow(wave, 0.82f) * 0.19f;
        halfWidth = Mathf.Max(0.012f, halfWidth);
        var tipTaper = Mathf.Lerp(0.82f, 1.05f, wave);
        return new Vector3(LeafCenterX(t) + (side * halfWidth * tipTaper), t * 0.76f, z);
    }

    private static float LeafHalfThickness(float t, float side)
    {
        t = Mathf.Clamp01(t);
        var centerInfluence = Mathf.Pow(1f - Mathf.Clamp01(Mathf.Abs(side)), 0.55f);
        var lengthInfluence = 0.38f + (0.62f * LeafWave(t));
        return Mathf.Lerp(0.0025f, 0.020f, centerInfluence) * lengthInfluence;
    }

    private static float LeafCenterX(float t)
    {
        return LeafWave(t) * 0.025f;
    }

    private static float LeafWave(float t)
    {
        return Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
    }

    private static void CreateTaperedCylinder(
        string objectName,
        Transform parent,
        Vector3 start,
        Vector3 end,
        float startRadius,
        float endRadius,
        Material material)
    {
        var direction = end - start;
        var length = direction.magnitude;
        if (length <= 0.0001f || float.IsNaN(length) || float.IsInfinity(length))
        {
            return;
        }

        var cylinder = new GameObject(objectName);
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = start;
        cylinder.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);

        var meshFilter = cylinder.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = GetOrCreateTaperedCylinderMesh(startRadius, endRadius, length);

        var meshRenderer = cylinder.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
    }

    private static Mesh GetOrCreateTaperedCylinderMesh(float startRadius, float endRadius, float length)
    {
        var meshPath = ModelsFolder + "/" + TaperedMeshName(startRadius, endRadius, length) + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = TaperedMeshName(startRadius, endRadius, length)
            };
            AssetDatabase.CreateAsset(mesh, meshPath);
        }

        const int segments = 16;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        for (var i = 0; i < segments; i++)
        {
            var angle = (Mathf.PI * 2f * i) / segments;
            var x = Mathf.Cos(angle);
            var z = Mathf.Sin(angle);
            vertices.Add(new Vector3(x * startRadius, 0f, z * startRadius));
            vertices.Add(new Vector3(x * endRadius, length, z * endRadius));
        }

        var bottomCenter = vertices.Count;
        vertices.Add(Vector3.zero);
        var topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, length, 0f));

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            var bottomA = i * 2;
            var topA = bottomA + 1;
            var bottomB = next * 2;
            var topB = bottomB + 1;

            triangles.Add(bottomA);
            triangles.Add(topA);
            triangles.Add(bottomB);

            triangles.Add(bottomB);
            triangles.Add(topA);
            triangles.Add(topB);

            triangles.Add(bottomCenter);
            triangles.Add(bottomB);
            triangles.Add(bottomA);

            triangles.Add(topCenter);
            triangles.Add(topA);
            triangles.Add(topB);
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);

        return mesh;
    }

    private static string TaperedMeshName(float startRadius, float endRadius, float length)
    {
        return "MSH_TaperedCylinder_" +
            Mathf.RoundToInt(startRadius * 10000f) + "_" +
            Mathf.RoundToInt(endRadius * 10000f) + "_" +
            Mathf.RoundToInt(length * 10000f);
    }

    private static GameObject CreatePrimitive(string objectName, PrimitiveType primitiveType, Transform parent, Material material)
    {
        var go = GameObject.CreatePrimitive(primitiveType);
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = material;

        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        return go;
    }

    private static Material GetOrCreateMaterial(string materialName, Color color)
    {
        var path = MaterialsFolder + "/" + materialName + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(FindShader())
            {
                name = materialName
            };
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = FindShader();
        material.color = color;

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Shader FindShader()
    {
        var shader = Shader.Find("PlantAR/SolidColor");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            return shader;
        }

        return Shader.Find("Standard");
    }

    private static void SavePrefabAndDestroy(GameObject root, string prefabPath)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        EditorUtility.SetDirty(prefab);
    }

    private static void EnsureFolder(string parent, string child)
    {
        var path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
