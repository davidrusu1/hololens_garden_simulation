using System.Collections.Generic;
using UnityEngine;

namespace ICI.PlantGrowth.Phenology
{
    /// <summary>
    /// Adds an orange pot below every generated plant. The presentation contains
    /// no floor and no backdrop; the legacy backdrop is restored only when the
    /// old runtime menu is selected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SunflowerPresentationSetup : MonoBehaviour
    {
        [Header("Orange pots")]
        [SerializeField]
        private bool createOrangePots = true;

        [SerializeField, Min(0.1f)]
        private float potHeight = 0.42f;

        [SerializeField, Min(0.05f)]
        private float potTopRadius = 0.34f;

        [SerializeField, Min(0.05f)]
        private float potBottomRadius = 0.25f;

        [SerializeField, Range(12, 64)]
        private int potSegments = 32;

        private readonly Dictionary<Transform, GameObject> pots =
            new Dictionary<Transform, GameObject>();
        private readonly List<Transform> stalePlants = new List<Transform>();

        private Material orangePotMaterial;
        private Material soilMaterial;
        private Mesh potBodyMesh;
        private bool newSetupActive = true;
        private float nextRefreshTime;

        private void Start()
        {
            if (createOrangePots)
            {
                CreateSharedPotResources();
                RefreshPlantPots();
            }

            ApplyBackdropVisibility();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.4f;
            if (createOrangePots)
            {
                RefreshPlantPots();
            }
            ApplyBackdropVisibility();
        }

        private void OnDestroy()
        {
            foreach (GameObject pot in pots.Values)
            {
                if (pot != null)
                {
                    Destroy(pot);
                }
            }
            pots.Clear();

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

        public void SetNewSetupActive(bool active)
        {
            newSetupActive = active;
            SetPotsVisible(active);
            ApplyBackdropVisibility();
        }

        private void RefreshPlantPots()
        {
            RemoveDestroyedPlantEntries();

            Transform generatedField = FindGeneratedField();
            if (generatedField != null)
            {
                for (int index = 0; index < generatedField.childCount; index++)
                {
                    Transform plant = generatedField.GetChild(index);
                    EnsurePotForPlant(plant);
                }
                return;
            }

            // Scenes without the mixed-crop generator still receive one pot.
            EnsurePotForPlant(transform);
        }

        private Transform FindGeneratedField()
        {
            Transform[] descendants =
                transform.root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (descendants[index].name == "Generated Mixed Crop Field")
                {
                    return descendants[index];
                }
            }

            return null;
        }

        private void EnsurePotForPlant(Transform plant)
        {
            if (plant == null || pots.ContainsKey(plant))
            {
                return;
            }

            GameObject potRoot = new GameObject("Orange Plant Pot");
            potRoot.transform.SetParent(plant, false);
            potRoot.transform.localPosition = Vector3.zero;
            potRoot.transform.localRotation = Quaternion.identity;

            Vector3 parentScale = plant.lossyScale;
            potRoot.transform.localScale = new Vector3(
                SafeInverse(parentScale.x),
                SafeInverse(parentScale.y),
                SafeInverse(parentScale.z));

            CreatePotBody(potRoot.transform);
            CreatePotRimAndSoil(potRoot.transform);
            potRoot.SetActive(newSetupActive && plant.gameObject.activeInHierarchy);
            pots.Add(plant, potRoot);
        }

        private void RemoveDestroyedPlantEntries()
        {
            stalePlants.Clear();
            foreach (KeyValuePair<Transform, GameObject> entry in pots)
            {
                if (entry.Key == null || entry.Value == null)
                {
                    stalePlants.Add(entry.Key);
                }
            }

            for (int index = 0; index < stalePlants.Count; index++)
            {
                pots.Remove(stalePlants[index]);
            }
        }

        private void SetPotsVisible(bool visible)
        {
            foreach (KeyValuePair<Transform, GameObject> entry in pots)
            {
                if (entry.Value != null)
                {
                    entry.Value.SetActive(
                        visible
                        && entry.Key != null
                        && entry.Key.gameObject.activeInHierarchy);
                }
            }
        }

        private void ApplyBackdropVisibility()
        {
            StemGrowthController[] controllers =
                transform.root.GetComponentsInChildren<StemGrowthController>(true);
            for (int index = 0; index < controllers.Length; index++)
            {
                controllers[index].SetRuntimeBackdropVisible(!newSetupActive);
            }
        }

        private void CreateSharedPotResources()
        {
            if (potBodyMesh != null)
            {
                return;
            }

            orangePotMaterial = CreateRuntimeMaterial(
                "Runtime Orange Pot",
                new Color(0.92f, 0.31f, 0.055f, 1f),
                0.16f);
            soilMaterial = CreateRuntimeMaterial(
                "Runtime Pot Soil",
                new Color(0.16f, 0.075f, 0.035f, 1f),
                0.02f);
            potBodyMesh = BuildFrustumMesh(
                Mathf.Max(12, potSegments),
                Mathf.Max(0.05f, potBottomRadius),
                Mathf.Max(potBottomRadius, potTopRadius),
                Mathf.Max(0.1f, potHeight));
        }

        private void CreatePotBody(Transform parent)
        {
            GameObject bodyObject = new GameObject(
                "Tapered Orange Pot",
                typeof(MeshFilter),
                typeof(MeshRenderer));
            bodyObject.transform.SetParent(parent, false);
            bodyObject.GetComponent<MeshFilter>().sharedMesh = potBodyMesh;

            MeshRenderer renderer = bodyObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = orangePotMaterial;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private void CreatePotRimAndSoil(Transform parent)
        {
            float rimHeight = Mathf.Clamp(potHeight * 0.12f, 0.035f, 0.065f);

            CreateCylinder(
                "Orange Pot Rim",
                parent,
                new Vector3(0f, potHeight - rimHeight * 0.5f, 0f),
                potTopRadius * 1.08f,
                rimHeight,
                orangePotMaterial);
            CreateCylinder(
                "Soil",
                parent,
                new Vector3(0f, potHeight + 0.006f, 0f),
                potTopRadius * 0.82f,
                0.018f,
                soilMaterial);
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
            float bottomY = 0f;
            float topY = height;

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
                name = "Runtime Tapered Orange Pot"
            };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateCylinder(
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
        }

        private static Material CreateRuntimeMaterial(
            string materialName,
            Color color,
            float smoothness)
        {
            Shader shader = Shader.Find("HDRP/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
            Material material = new Material(shader)
            {
                name = materialName,
                color = color
            };

            SetMaterialColor(material, "_BaseColor", color);
            SetMaterialColor(material, "_Color", color);
            SetMaterialColor(material, "_UnlitColor", color);
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            return material;
        }

        private static void SetMaterialColor(
            Material material,
            string propertyName,
            Color color)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        private static float SafeInverse(float value)
        {
            return 1f / Mathf.Max(0.0001f, Mathf.Abs(value));
        }
    }
}
