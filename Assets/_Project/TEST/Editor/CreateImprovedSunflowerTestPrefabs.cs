using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class CreateImprovedSunflowerTestPrefabs
{
    private const string Root = "Assets/_Project/TEST";
    private const string MeshFolder = Root + "/Meshes";
    private const string MaterialFolder = Root + "/Materials";
    private const string TextureFolder = Root + "/Textures";
    private const string PrefabFolder = Root + "/Prefabs";
    private const string SceneFolder = Root + "/Scenes";

    private static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();
    private static Material stemMaterial;
    private static Material youngStemMaterial;
    private static Material leafMaterial;
    private static Material youngLeafMaterial;
    private static Material veinMaterial;
    private static Material petalMaterial;
    private static Material budPetalMaterial;
    private static Material discMaterial;
    private static Material seedMaterial;
    private static Material seedHighlightMaterial;
    private static Material sepalMaterial;
    private static Material receptacleMaterial;

    [MenuItem("Tools/Plant AR/TEST/Create Improved Sunflower Models")]
    public static void CreateAll()
    {
        ClearGeneratedOutput();
        EnsureFolders();
        MeshCache.Clear();
        CreateMaterials();

        CreateStemPrefab(new StemSpec("01_Incipient", 0.36f, 0.011f, 0.0055f, 0.010f, 0.018f, 0.018f, youngStemMaterial));
        CreateStemPrefab(new StemSpec("02_Young", 0.72f, 0.025f, 0.012f, 0.026f, 0.025f, 0.013f, youngStemMaterial));
        CreateStemPrefab(new StemSpec("03_Mature", 1.15f, 0.045f, 0.024f, 0.040f, 0.034f, 0.009f, stemMaterial));
        CreateStemPrefab(new StemSpec("04_Uniform90", 1.035f, 0.0345f, 0.0345f, 0.0f, 0.030f, 0.009f, stemMaterial));

        CreateLeafPrefab(new LeafSpec("01_Incipient", 0.16f, 0.16f, 0.060f, 0.010f, 0.0015f, 0.042f, 0.032f, 4, 2, youngLeafMaterial));
        CreateLeafPrefab(new LeafSpec("02_Young", 0.22f, 0.46f, 0.120f, 0.016f, 0.0020f, 0.020f, 0.013f, 8, 5, youngLeafMaterial));
        CreateLeafPrefab(new LeafSpec("03_Mature", 0.28f, 0.78f, 0.235f, 0.024f, 0.0026f, 0.009f, 0.005f, 12, 8, leafMaterial));

        CreateBudPrefab();
        CreateOpenFlowerPrefab("02_Young", 0.36f, 0.095f, 0.105f, 0.030f, 32, 70f, 0.055f, 0.028f, 26, 96);
        CreateOpenFlowerPrefab("03_Mature", 0.50f, 0.145f, 0.155f, 0.043f, 44, 30f, 0.078f, 0.040f, 30, 220);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CreatePreviewScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var report = ValidateGeneratedAssets();
        File.WriteAllText(Path.Combine(Application.dataPath, "_Project/TEST/VALIDATION.txt"), report);
        AssetDatabase.ImportAsset(Root + "/VALIDATION.txt", ImportAssetOptions.ForceUpdate);
        Debug.Log(report);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "_Project");
        EnsureFolder("Assets/_Project", "TEST");
        EnsureFolder(Root, "Meshes");
        EnsureFolder(Root, "Materials");
        EnsureFolder(Root, "Textures");
        EnsureFolder(Root, "Prefabs");
        EnsureFolder(Root, "Scenes");
        EnsureFolder(Root, "Editor");
    }

    private static void ClearGeneratedOutput()
    {
        var generatedPaths = new[]
        {
            MeshFolder, MaterialFolder, TextureFolder, PrefabFolder, SceneFolder,
            Root + "/VALIDATION.txt", Root + "/Sunflower_TEST_Preview.png", Root + "/Sunflower_TEST_RearPreview.png"
        };
        foreach (var path in generatedPaths)
        {
            if (AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
    }

    private static void CreateMaterials()
    {
        var stemTextures = CreateTextureSet("Stem", new Color(0.12f, 0.38f, 0.13f), 0.16f, 5.0f, 1.3f);
        var youngStemTextures = CreateTextureSet("YoungStem", new Color(0.26f, 0.58f, 0.20f), 0.13f, 4.0f, 1.0f);
        var leafTextures = CreateTextureSet("Leaf", new Color(0.075f, 0.39f, 0.12f), 0.18f, 8.0f, 1.6f);
        var youngLeafTextures = CreateTextureSet("YoungLeaf", new Color(0.23f, 0.60f, 0.20f), 0.14f, 7.0f, 1.2f);
        var veinTextures = CreateTextureSet("Vein", new Color(0.055f, 0.24f, 0.07f), 0.08f, 5.0f, 0.7f);
        var petalTextures = CreateTextureSet("Petal", new Color(1.0f, 0.62f, 0.015f), 0.14f, 10.0f, 0.8f);
        var budTextures = CreateTextureSet("BudPetal", new Color(1.0f, 0.76f, 0.06f), 0.10f, 9.0f, 0.6f);
        var discTextures = CreateTextureSet("SeedDisc", new Color(0.035f, 0.018f, 0.008f), 0.24f, 13.0f, 1.8f);
        var seedTextures = CreateTextureSet("Seed", new Color(0.19f, 0.085f, 0.022f), 0.55f, 18.0f, 3.1f);
        var seedHighlightTextures = CreateTextureSet("SeedHighlight", new Color(0.34f, 0.15f, 0.028f), 0.48f, 20.0f, 2.7f);
        var sepalTextures = CreateTextureSet("Sepal", new Color(0.10f, 0.32f, 0.095f), 0.17f, 7.0f, 1.4f);
        var receptacleTextures = CreateTextureSet("Receptacle", new Color(0.075f, 0.27f, 0.07f), 0.30f, 12.0f, 2.8f);

        stemMaterial = CreateMaterial("MAT_TEST_Stem_Mature", stemTextures, new Color(0.12f, 0.38f, 0.13f), 0.12f, 0.0f);
        youngStemMaterial = CreateMaterial("MAT_TEST_Stem_Young", youngStemTextures, new Color(0.26f, 0.58f, 0.20f), 0.16f, 0.0f);
        leafMaterial = CreateMaterial("MAT_TEST_Leaf_Mature", leafTextures, new Color(0.075f, 0.39f, 0.12f), 0.18f, 0.0f);
        youngLeafMaterial = CreateMaterial("MAT_TEST_Leaf_Young", youngLeafTextures, new Color(0.23f, 0.60f, 0.20f), 0.22f, 0.0f);
        veinMaterial = CreateMaterial("MAT_TEST_Leaf_Veins", veinTextures, new Color(0.055f, 0.24f, 0.07f), 0.20f, 0.0f);
        petalMaterial = CreateMaterial("MAT_TEST_Petals_Mature", petalTextures, new Color(1.0f, 0.62f, 0.015f), 0.28f, 0.0f);
        budPetalMaterial = CreateMaterial("MAT_TEST_Petals_Bud", budTextures, new Color(1.0f, 0.76f, 0.06f), 0.30f, 0.0f);
        discMaterial = CreateMaterial("MAT_TEST_SeedDisc", discTextures, new Color(0.035f, 0.018f, 0.008f), 0.11f, 0.0f);
        seedMaterial = CreateMaterial("MAT_TEST_Seeds_Detail", seedTextures, new Color(0.19f, 0.085f, 0.022f), 0.19f, 0.0f);
        seedHighlightMaterial = CreateMaterial("MAT_TEST_Seeds_Highlight", seedHighlightTextures, new Color(0.34f, 0.15f, 0.028f), 0.23f, 0.0f);
        sepalMaterial = CreateMaterial("MAT_TEST_Sepals", sepalTextures, new Color(0.10f, 0.32f, 0.095f), 0.16f, 0.0f);
        receptacleMaterial = CreateMaterial("MAT_TEST_Receptacle", receptacleTextures, new Color(0.075f, 0.27f, 0.07f), 0.13f, 0.0f);
    }

    private static TextureSet CreateTextureSet(string name, Color baseColor, float variation, float frequency, float normalStrength)
    {
        const int size = 128;
        var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var heights = new float[size * size];
        var seed = Mathf.Abs(name.GetHashCode() % 997) * 0.0137f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)size;
                var v = y / (float)size;
                var broad = Mathf.PerlinNoise(seed + u * frequency, seed * 0.37f + v * frequency);
                var fine = Mathf.PerlinNoise(seed * 1.7f + u * frequency * 3.7f, seed + v * frequency * 3.7f);
                var fibers = 0.5f + 0.5f * Mathf.Sin((u * 23f + broad * 2f) * Mathf.PI);
                var h = broad * 0.55f + fine * 0.30f + fibers * 0.15f;
                if (name.StartsWith("Seed") && name != "SeedDisc")
                {
                    var shellGrooves = 0.5f + 0.5f * Mathf.Sin((u * 34f + fine * 1.6f) * Mathf.PI);
                    var seedSpeckle = Mathf.PerlinNoise(seed * 2.3f + u * 52f, seed * 0.6f + v * 52f);
                    h = broad * 0.30f + fine * 0.22f + shellGrooves * 0.32f + seedSpeckle * 0.16f;
                }
                else if (name == "Receptacle")
                {
                    var stagger = Mathf.Floor(v * 14f) % 2f * 0.5f;
                    var scales = Mathf.Pow(Mathf.Abs(Mathf.Sin((u * 18f + stagger) * Mathf.PI)), 0.65f);
                    var ringGroove = 0.5f + 0.5f * Mathf.Sin(v * 28f * Mathf.PI);
                    h = broad * 0.28f + fine * 0.20f + scales * 0.34f + ringGroove * 0.18f;
                }
                heights[y * size + x] = h;
                var multiplier = 1f + (h - 0.5f) * variation;
                albedo.SetPixel(x, y, new Color(baseColor.r * multiplier, baseColor.g * multiplier, baseColor.b * multiplier, 1f));
            }
        }

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var left = heights[y * size + ((x - 1 + size) % size)];
                var right = heights[y * size + ((x + 1) % size)];
                var down = heights[((y - 1 + size) % size) * size + x];
                var up = heights[((y + 1) % size) * size + x];
                var n = new Vector3((left - right) * normalStrength, (down - up) * normalStrength, 1f).normalized;
                normal.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
            }
        }

        albedo.Apply();
        normal.Apply();
        var albedoPath = TextureFolder + "/TEX_TEST_" + name + "_Albedo.png";
        var normalPath = TextureFolder + "/TEX_TEST_" + name + "_Normal.png";
        File.WriteAllBytes(ToAbsolutePath(albedoPath), albedo.EncodeToPNG());
        File.WriteAllBytes(ToAbsolutePath(normalPath), normal.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(albedo);
        UnityEngine.Object.DestroyImmediate(normal);
        AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);

        ConfigureTexture(albedoPath, false);
        ConfigureTexture(normalPath, true);
        return new TextureSet(
            AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath),
            AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
    }

    private static void ConfigureTexture(string path, bool isNormal)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !isNormal;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }

    private static Material CreateMaterial(string name, TextureSet textures, Color color, float smoothness, float metallic)
    {
        var path = MaterialFolder + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);

        var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
        var material = new Material(shader) { name = name };
        SetColor(material, "_BaseColor", Color.white);
        SetColor(material, "_Color", Color.white);
        SetTexture(material, "_BaseColorMap", textures.Albedo);
        SetTexture(material, "_BaseMap", textures.Albedo);
        SetTexture(material, "_MainTex", textures.Albedo);
        SetTexture(material, "_NormalMap", textures.Normal);
        SetTexture(material, "_BumpMap", textures.Normal);
        SetFloat(material, "_NormalScale", 0.55f);
        SetFloat(material, "_BumpScale", 0.55f);
        SetFloat(material, "_Smoothness", smoothness);
        SetFloat(material, "_Metallic", metallic);
        SetFloat(material, "_DoubleSidedEnable", 1f);
        SetFloat(material, "_CullMode", 0f);
        SetFloat(material, "_CullModeForward", 0f);
        material.enableInstancing = true;
        ResetHdrpMaterialKeywords(material);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void ResetHdrpMaterialKeywords(Material material)
    {
        var utilityType = Type.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor");
        var method = utilityType == null ? null : utilityType.GetMethod("ResetMaterialKeywords", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (method != null) method.Invoke(null, new object[] { material });
    }

    private static void CreateStemPrefab(StemSpec spec)
    {
        var root = new GameObject("TEST_SunflowerStem_" + spec.Key);
        var anatomy = new GameObject("Anatomy");
        anatomy.transform.SetParent(root.transform, false);
        var mesh = CreateStemMesh("MSH_TEST_Stem_" + spec.Key, spec, 18, 22);
        CreateMeshObject("Ribbed Stem", anatomy.transform, mesh, spec.Material);

        var tipMesh = CreateEllipsoidMesh("MSH_TEST_StemTip_" + spec.Key, 12, 8);
        var tip = CreateMeshObject("Living Tip", anatomy.transform, tipMesh, spec.Material);
        tip.transform.localPosition = StemPoint(spec, 1f);
        tip.transform.localScale = new Vector3(spec.TipCap * 1.55f, spec.TipCap * 2.0f, spec.TipCap * 1.55f);

        if (spec.Height > 0.5f) CreateStemHairs(anatomy.transform, spec);
        SavePrefab(root, PrefabFolder + "/TEST_SunflowerStem_" + spec.Key + ".prefab");
    }

    private static Mesh CreateStemMesh(string name, StemSpec spec, int radialSegments, int lengthSegments)
    {
        return CreateTubeMesh(name, lengthSegments, radialSegments,
            t => StemPoint(spec, t),
            t => Mathf.Lerp(spec.BaseRadius, spec.TipRadius, t) * (1f + 0.025f * Mathf.Sin(t * Mathf.PI * 7f)),
            0.055f, 8);
    }

    private static Vector3 StemPoint(StemSpec spec, float t)
    {
        var x = spec.Bend * (t * t + 0.18f * Mathf.Sin(t * Mathf.PI * 2f));
        var z = spec.Bend * 0.22f * Mathf.Sin(t * Mathf.PI * 1.45f);
        return new Vector3(x, spec.Height * t, z);
    }

    private static void CreateStemHairs(Transform parent, StemSpec spec)
    {
        var hairRoot = new GameObject("Fine Surface Hairs");
        hairRoot.transform.SetParent(parent, false);
        var hairMesh = GetConeMesh("MSH_TEST_StemHair", 0.030f, 0.0012f, 6);
        for (var i = 0; i < 18; i++)
        {
            var t = 0.12f + (i % 9) * 0.085f;
            var angle = i * 137.5f;
            var radial = Quaternion.Euler(0f, angle, 0f) * Vector3.right;
            var radius = Mathf.Lerp(spec.BaseRadius, spec.TipRadius, t);
            var hair = CreateMeshObject("Hair " + (i + 1), hairRoot.transform, hairMesh, spec.Material);
            hair.transform.localPosition = StemPoint(spec, t) + radial * radius;
            hair.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (radial + Vector3.up * 0.22f).normalized);
            hair.transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.15f, (i % 4) / 3f);
        }
    }

    private static void CreateLeafPrefab(LeafSpec spec)
    {
        var root = new GameObject("TEST_SunflowerLeaf_" + spec.Key);
        var petioleSpec = new StemSpec(spec.Key + "_Petiole", spec.Petiole, spec.CenterThickness * 0.44f, spec.CenterThickness * 0.20f, spec.Curve * 0.32f, spec.CenterThickness * 0.2f, 0.004f, spec.SurfaceMaterial);
        var petiole = CreateMeshObject("Petiole", root.transform, CreateStemMesh("MSH_TEST_LeafPetiole_" + spec.Key, petioleSpec, 12, 12), spec.SurfaceMaterial);
        petiole.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);

        var blade = CreateMeshObject("Serrated Leaf Blade", root.transform, CreateLeafMesh(spec), spec.SurfaceMaterial);
        blade.transform.localPosition = Vector3.zero;

        var veins = new GameObject("Attached Vein System");
        veins.transform.SetParent(root.transform, false);
        CreateLeafVeins(veins.transform, spec);
        SavePrefab(root, PrefabFolder + "/TEST_SunflowerLeaf_" + spec.Key + ".prefab");
    }

    private static Mesh CreateLeafMesh(LeafSpec spec)
    {
        const int lengthSegments = 30;
        const int halfWidthSegments = 5;
        var widthSegments = halfWidthSegments * 2;
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        for (var sideLayer = 0; sideLayer < 2; sideLayer++)
        {
            var top = sideLayer == 0;
            for (var y = 0; y <= lengthSegments; y++)
            {
                var t = y / (float)lengthSegments;
                for (var x = 0; x <= widthSegments; x++)
                {
                    var side = x / (float)halfWidthSegments - 1f;
                    vertices.Add(LeafPoint(spec, t, side, top));
                    uvs.Add(new Vector2(x / (float)widthSegments, t));
                }
            }
        }

        var layerSize = (lengthSegments + 1) * (widthSegments + 1);
        for (var layer = 0; layer < 2; layer++)
        {
            var top = layer == 0;
            var offset = layer * layerSize;
            for (var y = 0; y < lengthSegments; y++)
            {
                for (var x = 0; x < widthSegments; x++)
                {
                    var a = offset + y * (widthSegments + 1) + x;
                    var b = a + 1;
                    var c = a + widthSegments + 1;
                    var d = c + 1;
                    if (top) AddQuad(triangles, a, c, d, b); else AddQuad(triangles, a, b, d, c);
                }
            }
        }

        for (var y = 0; y < lengthSegments; y++)
        {
            var next = y + 1;
            AddQuad(triangles,
                y * (widthSegments + 1),
                next * (widthSegments + 1),
                layerSize + next * (widthSegments + 1),
                layerSize + y * (widthSegments + 1));
            AddQuad(triangles,
                y * (widthSegments + 1) + widthSegments,
                layerSize + y * (widthSegments + 1) + widthSegments,
                layerSize + next * (widthSegments + 1) + widthSegments,
                next * (widthSegments + 1) + widthSegments);
        }
        for (var x = 0; x < widthSegments; x++)
        {
            AddQuad(triangles, x, layerSize + x, layerSize + x + 1, x + 1);
            var topRow = lengthSegments * (widthSegments + 1);
            AddQuad(triangles, topRow + x, topRow + x + 1, layerSize + topRow + x + 1, layerSize + topRow + x);
        }

        var mesh = new Mesh { name = "MSH_TEST_Leaf_" + spec.Key };
        mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh);
    }

    private static Vector3 LeafPoint(LeafSpec spec, float t, float side, bool top)
    {
        var sineProfile = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)));
        var widthProfile = Mathf.Pow(sineProfile, spec.Key.StartsWith("01") ? 0.42f : 0.70f);
        var baseWidth = spec.Key.StartsWith("01") ? 0.20f : 0.10f;
        var tip = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.88f, 1f, t));
        var serration = 1f + 0.065f * Mathf.Sin(t * spec.Serrations * Mathf.PI * 2f) * Mathf.Pow(Mathf.Abs(side), 1.7f);
        var halfWidth = spec.Width * 0.5f * Mathf.Max(baseWidth * (1f - t), widthProfile) * tip * serration;
        var x = side * halfWidth;
        var y = spec.Petiole + spec.Length * t;
        var centerCurve = spec.Curve * Mathf.Sin(t * Mathf.PI) + spec.Curl * t * t;
        var ripple = 0.006f * Mathf.Sin(t * Mathf.PI * spec.Serrations) * Mathf.Pow(Mathf.Abs(side), 2f);
        var fold = spec.Fold * Mathf.Pow(Mathf.Abs(side), 1.35f) * Mathf.Sin(t * Mathf.PI);
        var thicknessProfile = Mathf.Pow(sineProfile, 0.55f);
        var thickness = Mathf.Lerp(spec.EdgeThickness, spec.CenterThickness, Mathf.Pow(1f - Mathf.Abs(side), 0.70f)) * thicknessProfile + spec.EdgeThickness * 0.35f;
        var z = centerCurve + ripple + fold + (top ? thickness * 0.58f : -thickness * 0.42f);
        return new Vector3(x, y, z);
    }

    private static void CreateLeafVeins(Transform parent, LeafSpec spec)
    {
        var centerMesh = CreateTubeMesh("MSH_TEST_Midrib_" + spec.Key, 18, 8,
            t => LeafPoint(spec, 0.02f + t * 0.95f, 0f, true) + Vector3.forward * 0.0012f,
            t => Mathf.Lerp(spec.CenterThickness * 0.30f, spec.EdgeThickness * 0.75f, t), 0f, 0);
        CreateMeshObject("Central Midrib", parent, centerMesh, veinMaterial);

        for (var i = 0; i < spec.VeinPairs; i++)
        {
            var startT = Mathf.Lerp(0.16f, 0.78f, (i + 1f) / (spec.VeinPairs + 1f));
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var endT = Mathf.Min(0.93f, startT + 0.13f);
                var mesh = CreateTubeMesh("MSH_TEST_LeafVein_" + spec.Key + "_" + i + "_" + sign, 8, 6,
                    t =>
                    {
                        var along = Mathf.Lerp(startT, endT, t);
                        var side = sign * Mathf.SmoothStep(0f, 0.93f, t);
                        return LeafPoint(spec, along, side, true) + Vector3.forward * 0.0010f;
                    },
                    t => Mathf.Lerp(spec.CenterThickness * 0.14f, spec.EdgeThickness * 0.55f, t), 0f, 0);
                CreateMeshObject((sign < 0 ? "Left" : "Right") + " Vein " + (i + 1), parent, mesh, veinMaterial);
            }
        }
    }

    private static void CreateBudPrefab()
    {
        var root = new GameObject("TEST_SunflowerFlower_01_Incipient");
        var stemSpec = new StemSpec("FlowerBudStem", 0.24f, 0.015f, 0.010f, 0.010f, 0.012f, 0.005f, youngStemMaterial);
        CreateMeshObject("Peduncle", root.transform, CreateStemMesh("MSH_TEST_BudPeduncle", stemSpec, 12, 12), youngStemMaterial);

        var budBase = new Vector3(stemSpec.Bend, stemSpec.Height - 0.002f, 0f);
        var core = CreateMeshObject("Green Developing Receptacle", root.transform, CreateEllipsoidMesh("MSH_TEST_ClosedBudCore", 24, 16), receptacleMaterial);
        core.transform.localPosition = budBase + Vector3.up * 0.049f;
        core.transform.localScale = new Vector3(0.031f, 0.061f, 0.031f);

        var foldedPetalMesh = CreatePetalMesh("MSH_TEST_BudFoldedPetal", 0.082f, 0.010f, 0.0028f, -0.012f, 14, 3);
        var foldedPetals = new GameObject("Tightly Folded Yellow Petals");
        foldedPetals.transform.SetParent(root.transform, false);
        const int foldedPetalCount = 8;
        for (var i = 0; i < foldedPetalCount; i++)
        {
            var angle = i * 360f / foldedPetalCount;
            var radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var petal = CreateMeshObject("Folded Petal " + (i + 1), foldedPetals.transform, foldedPetalMesh, budPetalMaterial);
            petal.transform.localPosition = budBase + radial * 0.018f + Vector3.up * 0.018f;
            petal.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            petal.transform.localScale = Vector3.one * (0.96f + (i % 2) * 0.035f);
        }

        var bractMesh = CreatePetalMesh("MSH_TEST_BudBract", 0.088f, 0.017f, 0.0038f, -0.010f, 14, 3);
        var bracts = new GameObject("Separated Protective Bracts");
        bracts.transform.SetParent(root.transform, false);
        const int bractCount = 10;
        for (var i = 0; i < bractCount; i++)
        {
            var angle = i * 360f / bractCount + 18f;
            var radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var bract = CreateMeshObject("Bract " + (i + 1), bracts.transform, bractMesh, sepalMaterial);
            bract.transform.localPosition = budBase + radial * 0.034f + Vector3.up * 0.002f;
            bract.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            bract.transform.localScale = Vector3.one * (i % 2 == 0 ? 1f : 0.94f);
        }

        var collarMesh = CreatePetalMesh("MSH_TEST_BudCollarBract", 0.050f, 0.015f, 0.0032f, 0.006f, 10, 3);
        var collar = new GameObject("Lower Bract Collar");
        collar.transform.SetParent(root.transform, false);
        for (var i = 0; i < 5; i++)
        {
            var angle = i * 72f;
            var radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var bract = CreateMeshObject("Collar Bract " + (i + 1), collar.transform, collarMesh, receptacleMaterial);
            bract.transform.localPosition = budBase + radial * 0.031f - Vector3.up * 0.006f;
            bract.transform.localRotation = Quaternion.Euler(0f, angle, i % 2 == 0 ? 20f : -20f);
        }
        SavePrefab(root, PrefabFolder + "/TEST_SunflowerFlower_01_Incipient.prefab");
    }

    private static void CreateOpenFlowerPrefab(string key, float stemHeight, float discRadius, float petalLength, float petalWidth, int petalCount, float petalTilt, float bowlDepth, float rimForward, int bractCount, int seedCount)
    {
        var mature = key.StartsWith("03");
        var root = new GameObject("TEST_SunflowerFlower_" + key);
        var flowerCenter = new Vector3(0f, stemHeight, 0f);
        var stemMesh = CreateCurvedPeduncleMesh("MSH_TEST_FlowerPeduncle_" + key, stemHeight, mature ? 0.030f : 0.022f, mature ? 0.022f : 0.014f, flowerCenter, bowlDepth);
        CreateMeshObject("Centered Curved Peduncle", root.transform, stemMesh, mature ? stemMaterial : youngStemMaterial);

        var bowlRadius = discRadius * 1.17f;
        var bowl = CreateMeshObject("Continuous Green Receptacle Bowl", root.transform,
            CreateBowlMesh("MSH_TEST_ReceptacleBowl_" + key, bowlRadius, bowlDepth, rimForward, mature ? 0.012f : 0.008f, 48, 14), receptacleMaterial);
        bowl.transform.localPosition = flowerCenter;
        CreateReceptacleSurfaceScales(root.transform, key, flowerCenter, bowlRadius, bowlDepth, rimForward, mature);

        var disc = CreateMeshObject("Domed Seed Disc", root.transform,
            CreateDiscMesh("MSH_TEST_SeedDisc_" + key, discRadius, mature ? 0.038f : 0.026f, 48, 12), discMaterial);
        disc.transform.localPosition = flowerCenter + Vector3.forward * (rimForward * 0.66f);

        var seedRoot = new GameObject("Spiral Seed Detail");
        seedRoot.transform.SetParent(root.transform, false);
        seedRoot.transform.localPosition = flowerCenter + Vector3.forward * (rimForward * 0.66f + (mature ? 0.040f : 0.028f));
        CreateMeshObject("Dark Spiral Seeds", seedRoot.transform,
            CreateSeedPatternMesh("MSH_TEST_Seeds_Dark_" + key, discRadius * 0.92f, seedCount, mature ? 0.0072f : 0.0062f, mature ? 0.0032f : 0.0028f, 3, 0), seedMaterial);
        CreateMeshObject("Warm Spiral Seeds", seedRoot.transform,
            CreateSeedPatternMesh("MSH_TEST_Seeds_Warm_" + key, discRadius * 0.92f, seedCount, mature ? 0.0072f : 0.0062f, mature ? 0.0032f : 0.0028f, 3, 1), seedHighlightMaterial);
        CreateMeshObject("Midtone Spiral Seeds", seedRoot.transform,
            CreateSeedPatternMesh("MSH_TEST_Seeds_Midtone_" + key, discRadius * 0.92f, seedCount, mature ? 0.0072f : 0.0062f, mature ? 0.0032f : 0.0028f, 3, 2), seedMaterial);

        var petalMesh = CreatePetalMesh("MSH_TEST_Petal_" + key, petalLength, petalWidth, mature ? 0.0060f : 0.0045f, mature ? 0.010f : 0.020f, 14, 4);
        var petals = new GameObject("Layered Yellow Ray Petals");
        petals.transform.SetParent(root.transform, false);
        var rings = mature ? 2 : 2;
        for (var ring = 0; ring < rings; ring++)
        {
            var count = ring == 0 ? petalCount : Mathf.RoundToInt(petalCount * 0.78f);
            var startRadius = discRadius * (ring == 0 ? 0.86f : 0.78f);
            for (var i = 0; i < count; i++)
            {
                var angle = i * 360f / count + (ring == 1 ? 180f / count : 0f);
                var radial = Quaternion.Euler(0f, 0f, -angle) * Vector3.up;
                var petal = CreateMeshObject("Petal R" + (ring + 1) + " " + (i + 1), petals.transform, petalMesh, petalMaterial);
                var petalLayerOffset = (i % 3) * 0.0009f;
                petal.transform.localPosition = flowerCenter + radial * startRadius + Vector3.forward * (rimForward * 0.48f - ring * 0.006f + petalLayerOffset);
                petal.transform.localRotation = Quaternion.FromToRotation(Vector3.up, radial) * Quaternion.Euler(petalTilt + ring * 4f, 0f, 0f);
                petal.transform.localScale = Vector3.one * (ring == 0 ? 0.96f + (i % 2) * 0.025f : 0.84f + (i % 2) * 0.020f);
            }
        }

        var bractMesh = CreatePetalMesh("MSH_TEST_ReceptacleBract_" + key, discRadius * 0.78f, petalWidth * 0.72f, 0.0055f, 0.014f, 12, 3);
        var bracts = new GameObject("Tangential Green Involucral Bracts");
        bracts.transform.SetParent(root.transform, false);
        for (var i = 0; i < bractCount; i++)
        {
            var angle = i * 360f / bractCount;
            var radial = Quaternion.Euler(0f, 0f, -angle) * Vector3.up;
            var surfaceT = 0.46f + (i % 3) * 0.025f;
            var normal = BowlSurfaceNormal(radial, bowlRadius, bowlDepth, rimForward, surfaceT);
            var basePoint = flowerCenter + BowlSurfacePoint(radial, bowlRadius, bowlDepth, rimForward, surfaceT) + normal * 0.0045f;
            var direction = (BowlSurfaceTangent(radial, bowlRadius, bowlDepth, rimForward, surfaceT) + normal * 0.10f).normalized;
            var bract = CreateMeshObject("Bract " + (i + 1), bracts.transform, bractMesh, sepalMaterial);
            bract.transform.localPosition = basePoint;
            bract.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            bract.transform.localScale = Vector3.one * (i % 3 == 0 ? 1.08f : 0.94f);
        }

        SavePrefab(root, PrefabFolder + "/TEST_SunflowerFlower_" + key + ".prefab");
    }

    private static void CreateReceptacleSurfaceScales(Transform root, string key, Vector3 center, float radius, float depth, float rimForward, bool mature)
    {
        var scaleMesh = CreatePetalMesh("MSH_TEST_ReceptacleScale_" + key, radius * 0.27f, radius * 0.12f, 0.0032f, 0.004f, 10, 3);
        var scaleRoot = new GameObject("Textured Receptacle Scales");
        scaleRoot.transform.SetParent(root, false);
        var ringValues = new[] { 0.24f, 0.43f, 0.62f };
        for (var ring = 0; ring < ringValues.Length; ring++)
        {
            var count = (mature ? 9 : 8) + ring * (mature ? 4 : 3);
            for (var i = 0; i < count; i++)
            {
                var angle = i * 360f / count + (ring % 2) * 180f / count;
                var radial = Quaternion.Euler(0f, 0f, -angle) * Vector3.up;
                var t = ringValues[ring];
                var normal = BowlSurfaceNormal(radial, radius, depth, rimForward, t);
                var tangent = BowlSurfaceTangent(radial, radius, depth, rimForward, t);
                var scale = CreateMeshObject("Scale R" + (ring + 1) + " " + (i + 1), scaleRoot.transform, scaleMesh, receptacleMaterial);
                scale.transform.localPosition = center + BowlSurfacePoint(radial, radius, depth, rimForward, t) + normal * 0.0038f;
                scale.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (tangent + normal * 0.08f).normalized);
                scale.transform.localScale = Vector3.one * (0.90f + (i % 3) * 0.045f);
            }
        }
    }

    private static Vector3 BowlSurfacePoint(Vector3 radial, float radius, float depth, float rimForward, float t)
    {
        var rr = radius * Mathf.Sin(t * Mathf.PI * 0.5f);
        var z = Mathf.Lerp(-depth, rimForward, Mathf.Pow(t, 1.45f));
        return radial * rr + Vector3.forward * z;
    }

    private static Vector3 BowlSurfaceTangent(Vector3 radial, float radius, float depth, float rimForward, float t)
    {
        var dt = 0.002f;
        return (BowlSurfacePoint(radial, radius, depth, rimForward, Mathf.Min(1f, t + dt)) -
                BowlSurfacePoint(radial, radius, depth, rimForward, Mathf.Max(0f, t - dt))).normalized;
    }

    private static Vector3 BowlSurfaceNormal(Vector3 radial, float radius, float depth, float rimForward, float t)
    {
        var dt = 0.002f;
        var before = BowlSurfacePoint(radial, radius, depth, rimForward, Mathf.Max(0f, t - dt));
        var after = BowlSurfacePoint(radial, radius, depth, rimForward, Mathf.Min(1f, t + dt));
        var tangent = (after - before).normalized;
        var circumferential = Vector3.Cross(Vector3.forward, radial).normalized;
        var normal = Vector3.Cross(circumferential, tangent).normalized;
        if (Vector3.Dot(normal, radial - Vector3.forward) < 0f) normal = -normal;
        return normal;
    }

    private static Mesh CreateCurvedPeduncleMesh(string name, float height, float baseRadius, float tipRadius, Vector3 flowerCenter, float bowlDepth)
    {
        var p0 = Vector3.zero;
        var p1 = new Vector3(0f, height * 0.58f, -bowlDepth * 0.25f);
        var p2 = new Vector3(0f, height * 0.88f, -bowlDepth * 1.15f);
        var p3 = flowerCenter + Vector3.back * bowlDepth;
        return CreateTubeMesh(name, 24, 16,
            t => CubicBezier(p0, p1, p2, p3, t),
            t => Mathf.Lerp(baseRadius, tipRadius, t), 0.045f, 7);
    }

    private static Mesh CreateTubeMesh(string name, int lengthSegments, int radialSegments, Func<float, Vector3> point, Func<float, float> radius, float ribStrength, int ribs)
    {
        var cached = LoadMesh(name);
        if (cached != null) return cached;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        for (var y = 0; y <= lengthSegments; y++)
        {
            var t = y / (float)lengthSegments;
            var prev = point(Mathf.Max(0f, t - 0.001f));
            var next = point(Mathf.Min(1f, t + 0.001f));
            var tangent = (next - prev).normalized;
            var right = Vector3.Cross(tangent, Vector3.forward);
            if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(tangent, Vector3.right);
            right.Normalize();
            var forward = Vector3.Cross(right, tangent).normalized;
            for (var x = 0; x <= radialSegments; x++)
            {
                var u = x / (float)radialSegments;
                var angle = u * Mathf.PI * 2f;
                var radial = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                var rib = ribs > 0 ? 1f + ribStrength * Mathf.Cos(angle * ribs) : 1f;
                vertices.Add(point(t) + radial * radius(t) * rib);
                normals.Add(radial);
                uvs.Add(new Vector2(u, t * 4f));
            }
        }
        for (var y = 0; y < lengthSegments; y++)
        {
            for (var x = 0; x < radialSegments; x++)
            {
                var a = y * (radialSegments + 1) + x;
                var b = a + 1;
                var c = a + radialSegments + 1;
                var d = c + 1;
                AddQuad(triangles, a, b, d, c);
            }
        }
        AddTubeCap(vertices, normals, uvs, triangles, point(0f), (point(0.001f) - point(0f)).normalized, radius(0f), radialSegments, true);
        AddTubeCap(vertices, normals, uvs, triangles, point(1f), (point(1f) - point(0.999f)).normalized, radius(1f), radialSegments, false);

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh);
    }

    private static void AddTubeCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector3 center, Vector3 tangent, float radius, int segments, bool bottom)
    {
        var normal = bottom ? -tangent : tangent;
        var right = Vector3.Cross(tangent, Vector3.forward);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(tangent, Vector3.right);
        right.Normalize();
        var forward = Vector3.Cross(right, tangent).normalized;
        var centerIndex = vertices.Count;
        vertices.Add(center);
        normals.Add(normal);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (var i = 0; i <= segments; i++)
        {
            var angle = i / (float)segments * Mathf.PI * 2f;
            vertices.Add(center + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius);
            normals.Add(normal);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
        }
        for (var i = 0; i < segments; i++)
        {
            if (bottom) AddTriangle(triangles, centerIndex, centerIndex + i + 2, centerIndex + i + 1);
            else AddTriangle(triangles, centerIndex, centerIndex + i + 1, centerIndex + i + 2);
        }
    }

    private static Mesh CreatePetalMesh(string name, float length, float width, float thickness, float curl, int lengthSegments, int widthSegments)
    {
        var cached = LoadMesh(name);
        if (cached != null) return cached;
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        var layerSize = (lengthSegments + 1) * (widthSegments + 1);
        for (var layer = 0; layer < 2; layer++)
        {
            var top = layer == 0;
            for (var y = 0; y <= lengthSegments; y++)
            {
                var t = y / (float)lengthSegments;
                var profile = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 0.92f + 0.04f)), 0.65f) * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.82f, 1f, t));
                for (var x = 0; x <= widthSegments; x++)
                {
                    var side = x / (float)widthSegments * 2f - 1f;
                    var px = side * width * 0.5f * profile;
                    var py = length * t;
                    var ridge = thickness * (1f - Mathf.Abs(side)) * Mathf.Sin(t * Mathf.PI);
                    var pz = curl * t * t + ridge + (top ? thickness * 0.34f : -thickness * 0.34f);
                    vertices.Add(new Vector3(px, py, pz));
                    uvs.Add(new Vector2(x / (float)widthSegments, t));
                }
            }
        }
        for (var layer = 0; layer < 2; layer++)
        {
            var top = layer == 0;
            var offset = layer * layerSize;
            for (var y = 0; y < lengthSegments; y++)
            {
                for (var x = 0; x < widthSegments; x++)
                {
                    var a = offset + y * (widthSegments + 1) + x;
                    var b = a + 1;
                    var c = a + widthSegments + 1;
                    var d = c + 1;
                    if (top) AddQuad(triangles, a, c, d, b); else AddQuad(triangles, a, b, d, c);
                }
            }
        }
        for (var y = 0; y < lengthSegments; y++)
        {
            var n = y + 1;
            AddQuad(triangles, y * (widthSegments + 1), n * (widthSegments + 1), layerSize + n * (widthSegments + 1), layerSize + y * (widthSegments + 1));
            AddQuad(triangles, y * (widthSegments + 1) + widthSegments, layerSize + y * (widthSegments + 1) + widthSegments, layerSize + n * (widthSegments + 1) + widthSegments, n * (widthSegments + 1) + widthSegments);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh);
    }

    private static Mesh CreateBowlMesh(string name, float radius, float depth, float rimForward, float thickness, int radialSegments, int rings)
    {
        var cached = LoadMesh(name);
        if (cached != null) return cached;
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        var layerSize = (rings + 1) * (radialSegments + 1);
        for (var layer = 0; layer < 2; layer++)
        {
            var outer = layer == 0;
            for (var r = 0; r <= rings; r++)
            {
                var t = r / (float)rings;
                var rr = radius * Mathf.Sin(t * Mathf.PI * 0.5f) - (outer ? 0f : thickness);
                rr = Mathf.Max(0f, rr);
                var z = Mathf.Lerp(-depth, rimForward, Mathf.Pow(t, 1.45f)) + (outer ? 0f : thickness * 0.55f);
                for (var s = 0; s <= radialSegments; s++)
                {
                    var angle = s / (float)radialSegments * Mathf.PI * 2f;
                    var ribFade = Mathf.SmoothStep(0f, 1f, t) * Mathf.SmoothStep(1f, 0.35f, t);
                    var radialRibs = outer ? 1f + 0.032f * Mathf.Sin(angle * 18f + t * 5f) * ribFade : 1f;
                    var ringRelief = outer ? depth * 0.024f * Mathf.Sin(t * Mathf.PI * 10f) * Mathf.Sin(t * Mathf.PI) : 0f;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * rr * radialRibs, Mathf.Sin(angle) * rr * radialRibs, z + ringRelief));
                    uvs.Add(new Vector2(s / (float)radialSegments, t));
                }
            }
        }
        for (var layer = 0; layer < 2; layer++)
        {
            var outer = layer == 0;
            var offset = layer * layerSize;
            for (var r = 0; r < rings; r++)
            {
                for (var s = 0; s < radialSegments; s++)
                {
                    var a = offset + r * (radialSegments + 1) + s;
                    var b = a + 1;
                    var c = a + radialSegments + 1;
                    var d = c + 1;
                    if (outer) AddQuad(triangles, a, b, d, c); else AddQuad(triangles, a, c, d, b);
                }
            }
        }
        var outerRim = rings * (radialSegments + 1);
        var innerRim = layerSize + outerRim;
        for (var s = 0; s < radialSegments; s++) AddQuad(triangles, outerRim + s, outerRim + s + 1, innerRim + s + 1, innerRim + s);
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh);
    }

    private static Mesh CreateDiscMesh(string name, float radius, float depth, int radialSegments, int rings)
    {
        var cached = LoadMesh(name);
        if (cached != null) return cached;
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        for (var r = 0; r <= rings; r++)
        {
            var t = r / (float)rings;
            var rr = radius * t;
            var z = depth * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
            for (var s = 0; s <= radialSegments; s++)
            {
                var a = s / (float)radialSegments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, z));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * t * 0.5f, 0.5f + Mathf.Sin(a) * t * 0.5f));
            }
        }
        for (var r = 0; r < rings; r++)
        {
            for (var s = 0; s < radialSegments; s++)
            {
                var a = r * (radialSegments + 1) + s;
                AddQuad(triangles, a, a + 1, a + radialSegments + 2, a + radialSegments + 1);
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh);
    }

    private static Mesh CreateSeedPatternMesh(string name, float radius, int count, float seedLength, float seedWidth, int subsetModulo, int subsetRemainder)
    {
        var cached = LoadMesh(name);
        if (cached != null) return cached;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();
        const float goldenAngle = 2.39996323f;
        for (var i = 0; i < count; i++)
        {
            if (i % subsetModulo != subsetRemainder) continue;
            var t = Mathf.Sqrt((i + 0.5f) / count);
            var angle = i * goldenAngle;
            var center = new Vector3(Mathf.Cos(angle) * radius * t, Mathf.Sin(angle) * radius * t, 0f);
            var tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var baseIndex = vertices.Count;
            var uvCenter = new Vector2(Mathf.Repeat(i * 0.618034f, 0.78f) + 0.11f, Mathf.Repeat(i * 0.414214f, 0.78f) + 0.11f);
            const int ringVertices = 8;
            for (var v = 0; v < ringVertices; v++)
            {
                var localAngle = v / (float)ringVertices * Mathf.PI * 2f;
                var across = Mathf.Cos(localAngle) * seedWidth;
                var along = Mathf.Sin(localAngle) * seedLength;
                vertices.Add(center + new Vector3(tangent.x * across + radial.x * along, tangent.y * across + radial.y * along, 0f));
                uvs.Add(uvCenter + new Vector2(Mathf.Cos(localAngle), Mathf.Sin(localAngle)) * 0.085f);
            }
            var frontIndex = vertices.Count;
            vertices.Add(center + new Vector3(radial.x, radial.y, 0f) * seedLength * 0.10f + Vector3.forward * seedWidth * 0.95f);
            uvs.Add(uvCenter + new Vector2(0.02f, 0.02f));
            var backIndex = vertices.Count;
            vertices.Add(center - Vector3.forward * seedWidth * 0.22f);
            uvs.Add(uvCenter - new Vector2(0.02f, 0.02f));
            for (var v = 0; v < ringVertices; v++)
            {
                var next = (v + 1) % ringVertices;
                AddTriangle(triangles, frontIndex, baseIndex + v, baseIndex + next);
                AddTriangle(triangles, backIndex, baseIndex + next, baseIndex + v);
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh);
    }

    private static Mesh CreateEllipsoidMesh(string name, int longitude, int latitude)
    {
        var cached = LoadMesh(name);
        if (cached != null) return cached;
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        for (var y = 0; y <= latitude; y++)
        {
            var v = y / (float)latitude;
            var phi = v * Mathf.PI;
            for (var x = 0; x <= longitude; x++)
            {
                var u = x / (float)longitude;
                var theta = u * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)));
                uvs.Add(new Vector2(u, v));
            }
        }
        for (var y = 0; y < latitude; y++)
        {
            for (var x = 0; x < longitude; x++)
            {
                var a = y * (longitude + 1) + x;
                AddQuad(triangles, a, a + longitude + 1, a + longitude + 2, a + 1);
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveMesh(mesh);
    }

    private static Mesh GetConeMesh(string name, float height, float radius, int segments)
    {
        var cached = LoadMesh(name);
        if (cached != null) return cached;
        return CreateTubeMesh(name, 3, segments, t => Vector3.up * height * t, t => radius * (1f - t), 0f, 0);
    }

    private static void CreatePreviewScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Sunflower TEST Preview";
        var positions = new[]
        {
            new Vector3(-2.4f, 0f, 0f), new Vector3(-1.5f, 0f, 0f), new Vector3(-0.5f, 0f, 0f), new Vector3(0.6f, 0f, 0f),
            new Vector3(-2.3f, 0f, 1.2f), new Vector3(-1.1f, 0f, 1.2f), new Vector3(0.2f, 0f, 1.2f),
            new Vector3(-2.1f, 0f, 2.5f), new Vector3(-0.7f, 0f, 2.5f), new Vector3(0.9f, 0f, 2.5f)
        };
        var names = new[]
        {
            "TEST_SunflowerStem_01_Incipient", "TEST_SunflowerStem_02_Young", "TEST_SunflowerStem_03_Mature", "TEST_SunflowerStem_04_Uniform90",
            "TEST_SunflowerLeaf_01_Incipient", "TEST_SunflowerLeaf_02_Young", "TEST_SunflowerLeaf_03_Mature",
            "TEST_SunflowerFlower_01_Incipient", "TEST_SunflowerFlower_02_Young", "TEST_SunflowerFlower_03_Mature"
        };
        var previewInstances = new List<GameObject>();
        for (var i = 0; i < names.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + names[i] + ".prefab");
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                instance.transform.position = positions[i];
                previewInstances.Add(instance);
            }
        }
        var lightObject = new GameObject("Preview Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 55000f;
        lightObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);

        var volumeObject = new GameObject("Preview Exposure");
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Sunflower TEST Preview Volume";
        AssetDatabase.CreateAsset(profile, SceneFolder + "/Sunflower_TEST_PreviewVolume.asset");
        volume.sharedProfile = profile;
        var exposure = profile.Add<Exposure>(true);
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(11.5f);
        var tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        var cameraObject = new GameObject("Preview Camera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(-0.45f, 1.45f, 6.2f);
        cameraObject.transform.LookAt(new Vector3(-0.55f, 0.55f, 1.25f));
        camera.fieldOfView = 33f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.13f, 0.14f, 1f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 30f;
        EditorSceneManager.SaveScene(scene, SceneFolder + "/Sunflower_TEST_Preview.unity");
        RenderPreview(camera, Root + "/Sunflower_TEST_Preview.png");

        foreach (var instance in previewInstances)
            if (!instance.name.Contains("Flower")) instance.SetActive(false);
        cameraObject.transform.position = new Vector3(-0.45f, 1.10f, -2.8f);
        cameraObject.transform.LookAt(new Vector3(-0.55f, 0.38f, 2.5f));
        camera.fieldOfView = 29f;
        RenderPreview(camera, Root + "/Sunflower_TEST_RearPreview.png");
    }

    private static void RenderPreview(Camera camera, string assetPath)
    {
        const int width = 1600;
        const int height = 1000;
        var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        camera.targetTexture = renderTexture;
        camera.Render();
        RenderTexture.active = renderTexture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(ToAbsolutePath(assetPath), image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        camera.targetTexture = null;
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    private static string ValidateGeneratedAssets()
    {
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
        var meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder });
        var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder });
        var errors = new List<string>();
        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) errors.Add(path + ": missing mesh on " + filter.name);
                else if (!IsFinite(filter.sharedMesh.bounds.center) || !IsFinite(filter.sharedMesh.bounds.size)) errors.Add(path + ": invalid mesh bounds on " + filter.name);
            }
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null) errors.Add(path + ": missing material or shader on " + renderer.name);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        return "Sunflower TEST validation\n" +
               "Prefabs: " + prefabGuids.Length + " (expected 10)\n" +
               "Meshes: " + meshGuids.Length + "\n" +
               "Materials: " + materialGuids.Length + " (expected 12)\n" +
               "Errors: " + errors.Count + (errors.Count == 0 ? "\nPASS" : "\n" + string.Join("\n", errors.ToArray()));
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
               !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
    }

    private static Mesh SaveMesh(Mesh mesh)
    {
        var path = MeshFolder + "/" + mesh.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        MeshCache[mesh.name] = mesh;
        return mesh;
    }

    private static Mesh LoadMesh(string name)
    {
        Mesh mesh;
        if (MeshCache.TryGetValue(name, out mesh) && mesh != null) return mesh;
        mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + "/" + name + ".asset");
        if (mesh != null) MeshCache[name] = mesh;
        return mesh;
    }

    private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static Vector3 CubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        var q = 1f - t;
        return q * q * q * a + 3f * q * q * t * b + 3f * q * t * t * c + t * t * t * d;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        AddTriangle(triangles, a, b, c);
        AddTriangle(triangles, a, c, d);
    }

    private static void AddTriangle(List<int> triangles, int a, int b, int c)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    private static string ToAbsolutePath(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
    }

    private static void EnsureFolder(string parent, string child)
    {
        var path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static void SetColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property)) material.SetColor(property, value);
    }

    private static void SetTexture(Material material, string property, Texture value)
    {
        if (material.HasProperty(property)) material.SetTexture(property, value);
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private struct TextureSet
    {
        public readonly Texture2D Albedo;
        public readonly Texture2D Normal;
        public TextureSet(Texture2D albedo, Texture2D normal) { Albedo = albedo; Normal = normal; }
    }

    private struct StemSpec
    {
        public readonly string Key;
        public readonly float Height;
        public readonly float BaseRadius;
        public readonly float TipRadius;
        public readonly float Bend;
        public readonly float TipCap;
        public readonly float HairLength;
        public readonly Material Material;
        public StemSpec(string key, float height, float baseRadius, float tipRadius, float bend, float tipCap, float hairLength, Material material)
        {
            Key = key; Height = height; BaseRadius = baseRadius; TipRadius = tipRadius; Bend = bend; TipCap = tipCap; HairLength = hairLength; Material = material;
        }
    }

    private struct LeafSpec
    {
        public readonly string Key;
        public readonly float Petiole;
        public readonly float Length;
        public readonly float Width;
        public readonly float CenterThickness;
        public readonly float EdgeThickness;
        public readonly float Curl;
        public readonly float Fold;
        public readonly int Serrations;
        public readonly int VeinPairs;
        public readonly float Curve;
        public readonly Material SurfaceMaterial;
        public LeafSpec(string key, float petiole, float length, float width, float centerThickness, float edgeThickness, float curl, float fold, int serrations, int veinPairs, Material surfaceMaterial)
        {
            Key = key; Petiole = petiole; Length = length; Width = width; CenterThickness = centerThickness; EdgeThickness = edgeThickness; Curl = curl; Fold = fold; Serrations = serrations; VeinPairs = veinPairs; Curve = length * 0.035f; SurfaceMaterial = surfaceMaterial;
        }
    }
}
