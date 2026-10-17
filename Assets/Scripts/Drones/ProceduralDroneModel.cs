using UnityEngine;

namespace DroneTrainer.Drones
{
    /// <summary>
    /// Generates a realistic tactical multi-rotor UAV visual model featuring
    /// fuselage body, 4 quadcopter arms, motor pods, animated spinning rotor props,
    /// glowing LED navigation beacons, and an underslung sensor/payload pod.
    /// Eliminates placeholder cubes for high-impact visual demonstrations.
    /// </summary>
    public class ProceduralDroneModel : MonoBehaviour
    {
        [Header("Model Configuration")]
        [SerializeField] private Color bodyColor = new Color(0.18f, 0.20f, 0.22f);
        [SerializeField] private Color armColor = new Color(0.12f, 0.12f, 0.12f);
        [SerializeField] private Color propColor = new Color(0.8f, 0.8f, 0.8f, 0.45f);
        [SerializeField] private float armSpan = 0.85f;
        [SerializeField] private float propSpinRpm = 1800f;

        private Transform[] rotorProps;

        private void Start()
        {
            BuildVisualModel();
        }

        private void Update()
        {
            if (rotorProps != null)
            {
                float spinDelta = (propSpinRpm * 360f / 60f) * Time.deltaTime;
                for (int i = 0; i < rotorProps.Length; i++)
                {
                    if (rotorProps[i] != null)
                    {
                        rotorProps[i].Rotate(Vector3.up, spinDelta * (i % 2 == 0 ? 1f : -1f), Space.Self);
                    }
                }
            }
        }

        [ContextMenu("Regenerate Procedural Drone Model")]
        public void BuildVisualModel()
        {
            // Clear existing model if present
            Transform existing = transform.Find("Drone_Visual_Root");
            if (existing != null) DestroyImmediate(existing.gameObject);

            GameObject root = new GameObject("Drone_Visual_Root");
            root.transform.SetParent(transform);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            // 1. Central Fuselage
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Fuselage";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.35f, 0.12f, 0.45f);
            ApplyMaterial(body, bodyColor, 0.4f, 0.6f);

            // 2. Underslung Gimbal / Payload Pod
            GameObject pod = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pod.name = "SensorPod";
            pod.transform.SetParent(root.transform);
            pod.transform.localPosition = new Vector3(0f, -0.08f, 0.12f);
            pod.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
            ApplyMaterial(pod, Color.black, 0.8f, 0.9f);

            // 3. Four Quadcopter Motor Arms & Rotors
            rotorProps = new Transform[4];
            Vector3[] armOffsets = new Vector3[]
            {
                new Vector3(1f, 0f, 1f).normalized,
                new Vector3(-1f, 0f, 1f).normalized,
                new Vector3(1f, 0f, -1f).normalized,
                new Vector3(-1f, 0f, -1f).normalized
            };

            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = armOffsets[i];
                Vector3 armEnd = dir * (armSpan * 0.5f);

                // Arm strut
                GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arm.name = $"Arm_{i + 1}";
                arm.transform.SetParent(root.transform);
                arm.transform.localPosition = armEnd * 0.5f;
                arm.transform.localScale = new Vector3(0.04f, (armSpan * 0.5f) * 0.5f, 0.04f);
                arm.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                ApplyMaterial(arm, armColor, 0.2f, 0.3f);

                // Motor Cap
                GameObject motor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                motor.name = $"Motor_{i + 1}";
                motor.transform.SetParent(root.transform);
                motor.transform.localPosition = armEnd + Vector3.up * 0.02f;
                motor.transform.localScale = new Vector3(0.09f, 0.04f, 0.09f);
                ApplyMaterial(motor, Color.black, 0.5f, 0.5f);

                // Spinning Rotor Propeller
                GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prop.name = $"Prop_{i + 1}";
                prop.transform.SetParent(motor.transform);
                prop.transform.localPosition = Vector3.up * 0.6f;
                prop.transform.localScale = new Vector3(4.2f, 0.15f, 0.45f);
                ApplyMaterial(prop, propColor, 0.1f, 0.2f);
                rotorProps[i] = prop.transform;

                // LED Nav Lights (Front Green, Rear Red)
                GameObject led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                led.name = "Nav_LED";
                led.transform.SetParent(motor.transform);
                led.transform.localPosition = Vector3.down * 0.5f;
                led.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                Color ledCol = (dir.z > 0) ? Color.green : Color.red;
                ApplyMaterial(led, ledCol, 0.1f, 0.9f, isEmissive: true);
            }
        }

        private void ApplyMaterial(GameObject obj, Color color, float metallic, float smoothness, bool isEmissive = false)
        {
            var rend = obj.GetComponent<Renderer>();
            if (rend == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") 
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard") 
                ?? Shader.Find("Diffuse");
            var mat = rend.sharedMaterial != null ? new Material(rend.sharedMaterial) : new Material(shader);
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            if (isEmissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 2f);
            }

            rend.sharedMaterial = mat;
        }
    }
}
