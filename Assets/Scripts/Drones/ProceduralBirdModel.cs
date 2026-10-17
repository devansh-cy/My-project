using UnityEngine;

namespace DroneTrainer.Drones
{
    /// <summary>
    /// Generates a biological avian / bird decoy visual model with animated flapping wings.
    /// Distinguishes biological decoys visually from tactical mechanical UAVs.
    /// </summary>
    public class ProceduralBirdModel : MonoBehaviour
    {
        [Header("Bird Anatomy")]
        [SerializeField] private Color featherColor = new Color(0.35f, 0.32f, 0.28f);
        [SerializeField] private float flapFrequencyHz = 4.5f;
        [SerializeField] private float maxFlapAngleDeg = 35f;

        private Transform leftWing;
        private Transform rightWing;

        private void Start()
        {
            BuildBirdModel();
        }

        private void Update()
        {
            if (leftWing != null && rightWing != null)
            {
                float flapAngle = Mathf.Sin(Time.time * flapFrequencyHz * Mathf.PI * 2f) * maxFlapAngleDeg;
                leftWing.localRotation = Quaternion.Euler(0f, 0f, flapAngle);
                rightWing.localRotation = Quaternion.Euler(0f, 0f, -flapAngle);
            }
        }

        [ContextMenu("Regenerate Bird Model")]
        public void BuildBirdModel()
        {
            Transform existing = transform.Find("Bird_Visual_Root");
            if (existing != null) DestroyImmediate(existing.gameObject);

            GameObject root = new GameObject("Bird_Visual_Root");
            root.transform.SetParent(transform);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            // 1. Torso
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.25f, 0.20f, 0.55f);
            ApplyColor(body, featherColor);

            // 2. Beak
            GameObject beak = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beak.name = "Beak";
            beak.transform.SetParent(root.transform);
            beak.transform.localPosition = new Vector3(0f, 0.02f, 0.35f);
            beak.transform.localScale = new Vector3(0.04f, 0.08f, 0.04f);
            beak.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ApplyColor(beak, new Color(0.85f, 0.65f, 0.15f));

            // 3. Left Wing
            GameObject leftPivot = new GameObject("LeftWing_Pivot");
            leftPivot.transform.SetParent(root.transform);
            leftPivot.transform.localPosition = new Vector3(0.12f, 0.05f, 0f);

            GameObject lWingMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lWingMesh.name = "Wing_Mesh";
            lWingMesh.transform.SetParent(leftPivot.transform);
            lWingMesh.transform.localPosition = new Vector3(0.35f, 0f, 0f);
            lWingMesh.transform.localScale = new Vector3(0.70f, 0.02f, 0.25f);
            ApplyColor(lWingMesh, featherColor);
            leftWing = leftPivot.transform;

            // 4. Right Wing
            GameObject rightPivot = new GameObject("RightWing_Pivot");
            rightPivot.transform.SetParent(root.transform);
            rightPivot.transform.localPosition = new Vector3(-0.12f, 0.05f, 0f);

            GameObject rWingMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rWingMesh.name = "Wing_Mesh";
            rWingMesh.transform.SetParent(rightPivot.transform);
            rWingMesh.transform.localPosition = new Vector3(-0.35f, 0f, 0f);
            rWingMesh.transform.localScale = new Vector3(0.70f, 0.02f, 0.25f);
            ApplyColor(rWingMesh, featherColor);
            rightWing = rightPivot.transform;
        }

        private void ApplyColor(GameObject obj, Color color)
        {
            var rend = obj.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") 
                    ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                    ?? Shader.Find("Standard") 
                    ?? Shader.Find("Diffuse");
                var mat = rend.sharedMaterial != null ? new Material(rend.sharedMaterial) : new Material(shader);
                mat.color = color;
                rend.sharedMaterial = mat;
            }
        }
    }
}
