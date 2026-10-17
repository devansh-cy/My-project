using UnityEngine;

namespace DroneTrainer.Environment
{
    /// <summary>
    /// Procedurally constructs a rural forward operating base environment featuring
    /// rolling ground terrain mounds, dispersed tree clusters, perimeter security fences,
    /// and a central sensor watchtower.
    /// </summary>
    public class RuralEnvironmentBuilder : MonoBehaviour
    {
        [Header("Rural Terrain & Foliage")]
        [SerializeField] private int treeCount = 65;
        [SerializeField] private float treeScatterRadius = 350f;
        [SerializeField] private int terrainMoundCount = 12;
        [SerializeField] private int seed = 54321;

        [Header("Perimeter Defense")]
        [SerializeField] private float basePerimeterRadius = 60f;
        [SerializeField] private int fencePosts = 24;

        [ContextMenu("Build Rural Environment")]
        public void BuildEnvironment()
        {
            ClearEnvironment();

            System.Random rng = new System.Random(seed);
            GameObject ruralParent = new GameObject("Rural_Environment_Container");
            ruralParent.transform.SetParent(transform);
            ruralParent.transform.localPosition = Vector3.zero;

            // 1. Spawn Rolling Hills / Terrain Mounds
            GameObject moundsContainer = new GameObject("Terrain_Mounds");
            moundsContainer.transform.SetParent(ruralParent.transform);
            for (int m = 0; m < terrainMoundCount; m++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                float dist = Mathf.Lerp(basePerimeterRadius + 40f, treeScatterRadius, (float)rng.NextDouble());
                Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mound.name = $"TerrainMound_{m + 1}";
                mound.transform.SetParent(moundsContainer.transform);
                mound.transform.position = pos;
                mound.transform.localScale = new Vector3(
                    (float)(40.0 + rng.NextDouble() * 50.0),
                    (float)(12.0 + rng.NextDouble() * 18.0),
                    (float)(40.0 + rng.NextDouble() * 50.0)
                );

                var rend = mound.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                    mat.color = new Color(0.28f, 0.38f, 0.22f); // Dark military olive grass
                    rend.sharedMaterial = mat;
                }
            }

            // 2. Spawn Trees (Trunk + Foliage Canopy)
            GameObject treesContainer = new GameObject("Tree_Clusters");
            treesContainer.transform.SetParent(ruralParent.transform);
            for (int t = 0; t < treeCount; t++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                float dist = Mathf.Lerp(basePerimeterRadius + 20f, treeScatterRadius, (float)rng.NextDouble());
                Vector3 treePos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                GameObject treeRoot = new GameObject($"Tree_{t + 1}");
                treeRoot.transform.SetParent(treesContainer.transform);
                treeRoot.transform.position = treePos;

                // Trunk
                float trunkH = (float)(4.0 + rng.NextDouble() * 3.0);
                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Trunk";
                trunk.transform.SetParent(treeRoot.transform);
                trunk.transform.localPosition = new Vector3(0f, trunkH * 0.5f, 0f);
                trunk.transform.localScale = new Vector3(0.6f, trunkH * 0.5f, 0.6f);
                var trunkRend = trunk.GetComponent<Renderer>();
                if (trunkRend != null)
                {
                    var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                    mat.color = new Color(0.35f, 0.22f, 0.12f);
                    trunkRend.sharedMaterial = mat;
                }

                // Foliage Canopy
                GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                foliage.name = "Canopy";
                foliage.transform.SetParent(treeRoot.transform);
                float canopyScale = (float)(4.5 + rng.NextDouble() * 3.0);
                foliage.transform.localPosition = new Vector3(0f, trunkH + (canopyScale * 0.4f), 0f);
                foliage.transform.localScale = new Vector3(canopyScale, canopyScale * 1.2f, canopyScale);
                var folRend = foliage.GetComponent<Renderer>();
                if (folRend != null)
                {
                    var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                    mat.color = new Color(0.18f, (float)(0.40 + rng.NextDouble() * 0.15), 0.15f);
                    folRend.sharedMaterial = mat;
                }
            }

            // 3. Perimeter Fence Ring
            GameObject fenceContainer = new GameObject("Perimeter_Fence");
            fenceContainer.transform.SetParent(ruralParent.transform);
            for (int f = 0; f < fencePosts; f++)
            {
                float fenceAngle = (f / (float)fencePosts) * Mathf.PI * 2f;
                Vector3 postPos = new Vector3(
                    Mathf.Cos(fenceAngle) * basePerimeterRadius,
                    1.25f,
                    Mathf.Sin(fenceAngle) * basePerimeterRadius
                );

                // Fence post (vertical cylinder)
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = $"FencePost_{f + 1}";
                post.transform.SetParent(fenceContainer.transform);
                post.transform.position = postPos;
                post.transform.localScale = new Vector3(0.2f, 1.25f, 0.2f);
                var postRend = post.GetComponent<Renderer>();
                if (postRend != null)
                {
                    var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                    mat.color = new Color(0.45f, 0.45f, 0.45f); // Gunmetal grey
                    postRend.sharedMaterial = mat;
                }

                // Horizontal rail connecting to next post
                float nextAngle = ((f + 1) / (float)fencePosts) * Mathf.PI * 2f;
                Vector3 nextPostPos = new Vector3(
                    Mathf.Cos(nextAngle) * basePerimeterRadius,
                    1.25f,
                    Mathf.Sin(nextAngle) * basePerimeterRadius
                );

                Vector3 midpoint = (postPos + nextPostPos) * 0.5f;
                float railLength = Vector3.Distance(postPos, nextPostPos);

                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = $"FenceRail_{f + 1}";
                rail.transform.SetParent(fenceContainer.transform);
                rail.transform.position = midpoint;
                rail.transform.localScale = new Vector3(railLength, 0.12f, 0.08f);
                rail.transform.LookAt(nextPostPos);
                var railRend = rail.GetComponent<Renderer>();
                if (railRend != null)
                {
                    var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                    mat.color = new Color(0.35f, 0.35f, 0.35f);
                    railRend.sharedMaterial = mat;
                }
            }

            // 4. Central Watchtower
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "FOB_Watchtower";
            tower.transform.SetParent(ruralParent.transform);
            tower.transform.position = new Vector3(0f, 6f, 0f);
            tower.transform.localScale = new Vector3(4f, 12f, 4f);

            Debug.Log($"[RuralEnvironmentBuilder] Generated rural environment with {treeCount} trees, {terrainMoundCount} mounds, and {fencePosts} fence posts.");
        }

        [ContextMenu("Clear Rural Environment")]
        public void ClearEnvironment()
        {
            Transform existing = transform.Find("Rural_Environment_Container");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }
        }
    }
}
