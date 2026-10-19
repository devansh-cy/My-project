using UnityEngine;
using DroneTrainer.Trainee;

namespace DroneTrainer.Sensors
{
    /// <summary>
    /// Phase 2: Electro-Optical / Infrared (EO/IR) FLIR Gimbal Camera.
    /// Simulates a military turret-mounted thermal FLIR camera with motorized pan/tilt slewing,
    /// target auto-tracking lock, and dynamic picture-in-picture (PiP) RenderTexture streaming.
    /// Provides azimuth, elevation, slant range, and optical zoom telemetry to the C2 HUD.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FLIRGimbalCamera : MonoBehaviour
    {
        public static FLIRGimbalCamera Instance { get; private set; }

        [Header("Gimbal Kinematics")]
        [SerializeField] private float slewSpeedDeg = 65f;
        [SerializeField] private float defaultFov = 20f;
        [SerializeField] private float lockedZoomFov = 9f;
        [SerializeField] private Vector3 homeDirection = Vector3.forward;

        [Header("Render Texture")]
        [SerializeField] private int textureWidth = 256;
        [SerializeField] private int textureHeight = 256;

        private Camera gimbalCamera;
        private RenderTexture flirTexture;
        private Transform targetTransform;
        private Vector3 targetEstimatedPos;
        private bool hasTarget = false;
        private bool isLocked = false;

        private float currentAzimuth = 0f;
        private float currentElevation = 0f;
        private float currentRange = 0f;

        public RenderTexture FlirTexture => flirTexture;
        public float Azimuth => currentAzimuth;
        public float Elevation => currentElevation;
        public float SlantRange => currentRange;
        public bool HasLock => isLocked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupCamera();
        }

        private void SetupCamera()
        {
            gimbalCamera = GetComponent<Camera>();
            if (gimbalCamera == null)
            {
                gimbalCamera = gameObject.AddComponent<Camera>();
            }

            flirTexture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "FLIR_PiP_RenderTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            flirTexture.Create();

            gimbalCamera.targetTexture = flirTexture;
            gimbalCamera.fieldOfView = defaultFov;
            gimbalCamera.nearClipPlane = 0.5f;
            gimbalCamera.farClipPlane = 2500f;
            gimbalCamera.clearFlags = CameraClearFlags.SolidColor;
            gimbalCamera.backgroundColor = new Color(0.04f, 0.07f, 0.05f, 1f); // Dark thermal FLIR base tint
            gimbalCamera.depth = -10; // Render before main UI
        }

        private void OnDestroy()
        {
            if (flirTexture != null)
            {
                if (gimbalCamera != null) gimbalCamera.targetTexture = null;
                flirTexture.Release();
                Destroy(flirTexture);
            }
        }

        /// <summary>
        /// Slews gimbal towards the specified world coordinate.
        /// </summary>
        public void TrackWorldPosition(Vector3 position, Transform optionalObj = null)
        {
            hasTarget = true;
            targetEstimatedPos = position;
            targetTransform = optionalObj;
        }

        public void ClearTarget()
        {
            hasTarget = false;
            targetTransform = null;
        }

        private void Update()
        {
            Vector3 aimPoint = targetEstimatedPos;
            if (targetTransform != null)
            {
                aimPoint = targetTransform.position;
            }

            if (hasTarget)
            {
                Vector3 toTarget = aimPoint - transform.position;
                currentRange = toTarget.magnitude;

                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion desiredRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRot, slewSpeedDeg * Time.deltaTime);

                    float angleToTarget = Quaternion.Angle(transform.rotation, desiredRot);
                    isLocked = angleToTarget < 3.5f;

                    // Compute Azimuth and Elevation in degrees
                    Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
                    currentAzimuth = Vector3.SignedAngle(Vector3.forward, flatForward, Vector3.up);
                    currentElevation = -Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

                    // Smooth zoom in when target is centered
                    float targetFov = isLocked ? lockedZoomFov : defaultFov;
                    gimbalCamera.fieldOfView = Mathf.MoveTowards(gimbalCamera.fieldOfView, targetFov, 20f * Time.deltaTime);
                }
            }
            else
            {
                // Slew back to home surveillance pose
                Quaternion homeRot = Quaternion.LookRotation(homeDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRot, (slewSpeedDeg * 0.5f) * Time.deltaTime);
                gimbalCamera.fieldOfView = Mathf.MoveTowards(gimbalCamera.fieldOfView, defaultFov, 15f * Time.deltaTime);
                isLocked = false;
                currentRange = 0f;
            }
        }
    }
}
