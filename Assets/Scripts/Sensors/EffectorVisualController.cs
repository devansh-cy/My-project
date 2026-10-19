using System.Collections;
using UnityEngine;
using DroneTrainer.Trainee;

namespace DroneTrainer.Sensors
{
    /// <summary>
    /// Coordinates procedural tactical countermeasure visual effects (VFX):
    /// - High-intensity Directed Energy Laser beam with thermal spark emitter
    /// - Expanding Electronic Warfare (EW) radiofrequency jamming shockwaves
    /// - High-speed Net Catcher projectile with unfurling containment mesh
    /// - Kinetic Interceptor tracer missile with terminal blast
    /// </summary>
    public class EffectorVisualController : MonoBehaviour
    {
        private static EffectorVisualController instance;
        public static EffectorVisualController Instance => instance;

        [Header("Laser Visuals")]
        [SerializeField] private Color laserColor = new Color(0f, 1f, 0.85f, 1f);
        [SerializeField] private float laserDuration = 0.65f;

        [Header("EW Jamming Visuals")]
        [SerializeField] private Color jammingWaveColor = new Color(1f, 0.35f, 0f, 0.75f);

        private LineRenderer laserLine;
        private Transform towerMuzzle;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            EnsureLaserRenderer();
            LocateTowerMuzzle();
        }

        private void EnsureLaserRenderer()
        {
            laserLine = GetComponent<LineRenderer>();
            if (laserLine == null)
            {
                laserLine = gameObject.AddComponent<LineRenderer>();
            }

            laserLine.startWidth = 0.35f;
            laserLine.endWidth = 0.15f;
            laserLine.positionCount = 2;
            laserLine.enabled = false;

            Shader litShader = Shader.Find("Universal Render Pipeline/Unlit") 
                ?? Shader.Find("Universal Render Pipeline/Lit") 
                ?? Shader.Find("Sprites/Default");
            var mat = new Material(litShader);
            mat.color = laserColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", laserColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", laserColor * 3f);
            }
            laserLine.sharedMaterial = mat;
        }

        private void LocateTowerMuzzle()
        {
            var tower = GameObject.Find("SensorTower") ?? GameObject.Find("FOB_Watchtower");
            if (tower != null)
            {
                towerMuzzle = tower.transform;
            }
            else
            {
                towerMuzzle = transform;
            }
        }

        /// <summary>
        /// Fires visual countermeasure effect from sensor station towards the specified target coordinates.
        /// </summary>
        public void TriggerEffector(ResponseOption option, Vector3 targetPosition, GameObject targetObject = null)
        {
            Vector3 origin = towerMuzzle != null ? towerMuzzle.position + Vector3.up * 7f : Vector3.up * 8f;

            switch (option)
            {
                case ResponseOption.Laser:
                    StartCoroutine(FireLaserRoutine(origin, targetPosition));
                    break;

                case ResponseOption.Jam:
                    StartCoroutine(FireJammingPulseRoutine(origin, targetPosition));
                    break;

                case ResponseOption.Net:
                    StartCoroutine(FireNetProjectileRoutine(origin, targetPosition, targetObject));
                    break;

                case ResponseOption.Kinetic:
                    StartCoroutine(FireKineticInterceptorRoutine(origin, targetPosition));
                    break;

                default:
                    break;
            }
        }

        private IEnumerator FireLaserRoutine(Vector3 origin, Vector3 target)
        {
            if (laserLine == null) EnsureLaserRenderer();
            laserLine.enabled = true;

            float elapsed = 0f;
            while (elapsed < laserDuration)
            {
                elapsed += Time.deltaTime;
                Vector3 jitter = Random.insideUnitSphere * 0.08f;
                laserLine.SetPosition(0, origin);
                laserLine.SetPosition(1, target + jitter);

                // Laser pulse width oscillation
                float width = Mathf.PingPong(elapsed * 12f, 0.45f) + 0.15f;
                laserLine.startWidth = width;
                laserLine.endWidth = width * 0.5f;

                yield return null;
            }

            laserLine.enabled = false;
        }

        private IEnumerator FireJammingPulseRoutine(Vector3 origin, Vector3 target)
        {
            // Spawn procedural expanding spherical shockwave ring towards target
            GameObject wave = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wave.name = "EW_Jamming_Shockwave";
            Destroy(wave.GetComponent<Collider>());
            wave.transform.position = origin;
            wave.transform.localScale = Vector3.one * 2f;

            var rend = wave.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                var mat = new Material(s);
                mat.color = jammingWaveColor;
                rend.sharedMaterial = mat;
            }

            float duration = 1.2f;
            float elapsed = 0f;
            Vector3 startPos = origin;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Move from origin to target while expanding in size
                wave.transform.position = Vector3.Lerp(startPos, target, t);
                float scale = Mathf.Lerp(2f, 35f, t);
                wave.transform.localScale = new Vector3(scale, scale * 0.4f, scale);

                if (rend != null && rend.sharedMaterial != null)
                {
                    Color c = jammingWaveColor;
                    c.a = Mathf.Lerp(0.8f, 0f, t);
                    rend.sharedMaterial.color = c;
                }

                yield return null;
            }

            Destroy(wave);
        }

        private IEnumerator FireNetProjectileRoutine(Vector3 origin, Vector3 target, GameObject targetObj)
        {
            // Spawn compact net projectile
            GameObject canister = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            canister.name = "Net_Canister";
            Destroy(canister.GetComponent<Collider>());
            canister.transform.position = origin;
            canister.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);

            float speed = 85f; // Fast deployment
            float dist = Vector3.Distance(origin, target);
            float duration = Mathf.Max(0.1f, dist / speed);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                canister.transform.position = Vector3.Lerp(origin, target, t);
                canister.transform.Rotate(new Vector3(180f, 360f, 90f) * Time.deltaTime);
                yield return null;
            }

            // Unfurl net mesh at target
            canister.transform.position = target;
            canister.transform.localScale = new Vector3(5f, 0.2f, 5f);
            var rend = canister.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial.color = new Color(0.9f, 0.9f, 0.1f, 0.8f);
            }

            if (targetObj != null)
            {
                canister.transform.SetParent(targetObj.transform);
            }

            Destroy(canister, 4f);
        }

        private IEnumerator FireKineticInterceptorRoutine(Vector3 origin, Vector3 target)
        {
            GameObject missile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            missile.name = "Kinetic_Interceptor";
            Destroy(missile.GetComponent<Collider>());
            missile.transform.position = origin;
            missile.transform.localScale = new Vector3(0.25f, 1.2f, 0.25f);
            missile.transform.LookAt(target);

            var rend = missile.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
                var mat = new Material(s);
                mat.color = Color.white;
                rend.sharedMaterial = mat;
            }

            float speed = 140f;
            float dist = Vector3.Distance(origin, target);
            float duration = Mathf.Max(0.1f, dist / speed);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                missile.transform.position = Vector3.Lerp(origin, target, t);
                yield return null;
            }

            // Terminal flash
            missile.transform.localScale = Vector3.one * 6f;
            if (rend != null && rend.sharedMaterial != null)
            {
                rend.sharedMaterial.color = new Color(1f, 0.5f, 0f, 0.9f);
            }

            Destroy(missile, 0.15f);
        }
    }
}
