using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlantARMaterialCreator
{
    private const string MaterialsFolder = "Assets/_Project/Materials";

    [MenuItem("Tools/Plant AR/Create Plant Materials")]
    public static void CreatePlantMaterials()
    {
        EnsureFolder("Assets", "_Project");
        EnsureFolder("Assets/_Project", "Materials");

        CreateOrUpdateMaterial("MAT_Stem_GreenBrown", new Color32(94, 107, 53, 255));
        CreateOrUpdateMaterial("MAT_Leaf_Green", new Color32(63, 163, 77, 255));
        CreateOrUpdateMaterial("MAT_Leaf_DarkGreen", new Color32(37, 111, 55, 255));
        CreateOrUpdateMaterial("MAT_Flower_Yellow", new Color32(244, 196, 48, 255));
        CreateOrUpdateMaterial("MAT_Flower_Pink", new Color32(232, 112, 156, 255));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Plant AR",
            "Plant materials were created in Assets/_Project/Materials.",
            "OK"
        );
    }

    private static void CreateOrUpdateMaterial(string materialName, Color color)
    {
        string path = $"{MaterialsFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        SetColorIfPropertyExists(material, "_BaseColor", color);
        SetColorIfPropertyExists(material, "_Color", color);

        EditorUtility.SetDirty(material);
    }

    private static void SetColorIfPropertyExists(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void EnsureFolder(string parentFolder, string childFolder)
    {
        string fullPath = $"{parentFolder}/{childFolder}";

        if (!Directory.Exists(fullPath))
        {
            AssetDatabase.CreateFolder(parentFolder, childFolder);
        }
    }
}
