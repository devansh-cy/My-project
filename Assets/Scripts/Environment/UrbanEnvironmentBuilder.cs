using UnityEngine;

namespace DroneTrainer.Environment
{
    /// <summary>
    /// Procedurally constructs an urban environment with building blocks, city street corridors,
    /// and structural obstacles that realistically occlude sensor line-of-sight and generate radar clutter.
    /// </summary>
    public class UrbanEnvironmentBuilder : MonoBehaviour
    {
        [Header("City Grid Settings")]
        [SerializeField] private int gridRows = 6;
        [SerializeField] private int gridCols = 6;
        [SerializeField] private float blockSize = 40f;
        [SerializeField] private float streetWidth = 25f;
        [SerializeField] private float minBuildingHeight = 15f;
        [SerializeField] private float maxBuildingHeight = 75f;
        [SerializeField] private int seed = 12345;

        [Header("Materials / Colors")]
        [SerializeField] private Color buildingBaseColor = new Color(0.25f, 0.28f, 0.32f);
        [SerializeField] private Color windowGlowColor = new Color(0.8f, 0.85f, 0.9f);

        [ContextMenu("Build Urban Environment")]
        public void BuildEnvironment()
        {
            ClearEnvironment();

            System.Random rng = new System.Random(seed);
            GameObject urbanParent = new GameObject("Urban_Buildings_Container");
            urbanParent.transform.SetParent(transform);
            urbanParent.transform.localPosition = Vector3.zero;

            float totalSpanX = gridCols * (blockSize + streetWidth);
            float totalSpanZ = gridRows * (blockSize + streetWidth);
            Vector3 origin = new Vector3(-totalSpanX * 0.5f, 0f, -totalSpanZ * 0.5f);

            for (int r = 0; r < gridRows; r++)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    // Leave the center block open for the defense base / sensor tower
                    if (r >= gridRows / 2 - 1 && r <= gridRows / 2 && c >= gridCols / 2 - 1 && c <= gridCols / 2)
                    {
                        continue;
                    }

                    float height = Mathf.Lerp(minBuildingHeight, maxBuildingHeight, (float)rng.NextDouble());
                    Vector3 pos = origin + new Vector3(
                        c * (blockSize + streetWidth) + (blockSize * 0.5f),
                        height * 0.5f,
                        r * (blockSize + streetWidth) + (blockSize * 0.5f)
                    );

                    GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    building.name = $"Building_{r}_{c}";
                    building.transform.SetParent(urbanParent.transform);
                    building.transform.position = pos;
                    building.transform.localScale = new Vector3(blockSize, height, blockSize);

                    // Apply building material
                    var renderer = building.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                        mat.color = buildingBaseColor;
                        mat.SetFloat("_Metallic", 0.3f);
                        mat.SetFloat("_Glossiness", 0.4f);
                        renderer.sharedMaterial = mat;
                    }

                    // Optional rooftop antenna / clutter
                    if (rng.NextDouble() < 0.35)
                    {
                        GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        antenna.name = "Rooftop_Antenna";
                        antenna.transform.SetParent(building.transform);
                        antenna.transform.localPosition = new Vector3(0f, 0.5f + 0.1f, 0f);
                        antenna.transform.localScale = new Vector3(0.05f, 0.2f, 0.05f);
                    }
                }
            }

            Debug.Log($"[UrbanEnvironmentBuilder] Generated urban environment grid with {gridRows * gridCols} blocks.");
        }

        [ContextMenu("Clear Urban Environment")]
        public void ClearEnvironment()
        {
            Transform existing = transform.Find("Urban_Buildings_Container");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }
        }
    }
}
