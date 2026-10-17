using UnityEngine;
using DroneTrainer.Core.Events;
using DroneTrainer.Drones;
using DroneTrainer.Scenarios;

namespace DroneTrainer.Sensors
{
    /// <summary>
    /// Optical camera modes.
    /// </summary>
    public enum CameraSpectralBand
    {
        VisibleDaylight,
        InfraredThermalFLIR
    }

    /// <summary>
    /// Simulates an Electro-Optical / Infrared (EO/IR) gimbaled camera turret.
    /// Computes target optical visibility based on distance (angular resolution), line-of-sight occlusion,
    /// time-of-day illumination, fog/atmospheric extinction (Beer-Lambert model), and thermal contrast.
    /// Outputs high-fidelity Contact records via EventBus.
    /// </summary>
    public class EOCameraSim : MonoBehaviour
    {
        [Header("Gimbal & Optical Specs")]
        [SerializeField] private string cameraId = "CAM-GIMBAL-01";
        [SerializeField] private CameraSpectralBand spectralBand = CameraSpectralBand.VisibleDaylight;
        [SerializeField] [Range(5f, 90f)] private float fieldOfViewDeg = 45f;
        [SerializeField] [Range(100f, 3000f)] private float maxDetectionRange = 1600f;
        [SerializeField] [Range(1f, 30f)] private float updateRateHz = 10f; // 10 updates per second

        [Header("Environmental & Illumination Settings")]
        [SerializeField] private TimeOfDay timeOfDay = TimeOfDay.Day;
        [SerializeField] private SensorProfile sensorProfile = new SensorProfile();
        [SerializeField] private LayerMask occlusionMask = ~0;

        [Header("Debug & Visuals")]
        [SerializeField] private bool logDetections = false;
        [SerializeField] private bool drawGizmoFov = true;

        // Runtime state
        private float updateInterval;
        private float timer = 0f;
        private int trackCounter = 1;
        private readonly System.Collections.Generic.Dictionary<string, string> droneToTrackId = new System.Collections.Generic.Dictionary<string, string>();

        public string CameraId => cameraId;

        public CameraSpectralBand SpectralBand
        {
            get => spectralBand;
            set => spectralBand = value;
        }

        public TimeOfDay ActiveTimeOfDay
        {
            get => timeOfDay;
            set => timeOfDay = value;
        }

        public SensorProfile ActiveProfile
        {
            get => sensorProfile;
            set => sensorProfile = value ?? new SensorProfile();
        }

        private void Awake()
        {
            updateInterval = 1f / Mathf.Max(1f, updateRateHz);
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer = 0f;
                ScanFieldOfView();
            }
        }

        /// <summary>
        /// Evaluates all active drone agents within the camera frustum and line of sight.
        /// </summary>
        public void ScanFieldOfView()
        {
            float simTime = Time.time;
            Vector3 camPos = transform.position;
            Vector3 camForward = transform.forward;

            DroneAgent[] agents = FindObjectsByType<DroneAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < agents.Length; i++)
            {
                DroneAgent agent = agents[i];
                if (agent == null || !agent.gameObject.activeInHierarchy || agent.IsNeutralized) continue;

                Vector3 targetPos = agent.transform.position;
                Vector3 toTarget = targetPos - camPos;
                float distance = toTarget.magnitude;

                if (distance > maxDetectionRange || distance < 1f) continue;

                // 1. Angular FOV check
                float angle = Vector3.Angle(camForward, toTarget);
                if (angle > fieldOfViewDeg * 0.5f) continue;

                // 2. Line of sight occlusion check (buildings/terrain)
                if (Physics.Raycast(camPos, toTarget.normalized, out RaycastHit hit, distance - 0.5f, occlusionMask))
                {
                    // Occluded by intervening geometry
                    continue;
                }

                // 3. Compute optical visibility probability
                float pd = CalculateOpticalVisibility(distance, agent);

                // 4. Probabilistic detection check
                if (Random.value <= pd)
                {
                    // Optical sensors have razor-sharp angular accuracy, but slight range/depth estimation uncertainty
                    Vector3 estimatedPos = targetPos + (toTarget.normalized * Random.Range(-2f, 2f)) + (Random.insideUnitSphere * 0.4f);

                    SensorType type = (spectralBand == CameraSpectralBand.InfraredThermalFLIR)
                        ? SensorType.IRThermalCamera
                        : SensorType.EOCamera;

                    // Maintain consistent persistent track ID for the same drone
                    if (!droneToTrackId.TryGetValue(agent.DroneId, out string trackId))
                    {
                        trackId = $"EO-TRK-{trackCounter++:D4}";
                        droneToTrackId[agent.DroneId] = trackId;
                    }

                    Contact contact = new Contact(
                        id: trackId,
                        estimatedPosition: estimatedPos,
                        confidence: Mathf.Clamp01(pd),
                        sensorType: type,
                        timestamp: simTime,
                        estimatedVelocity: agent.Velocity,
                        snrDb: 15f * pd,
                        isFalseAlarm: false,
                        trueDroneId: agent.DroneId
                    );

                    EventBus.Publish(new ContactDetectedEvent(contact, simTime));

                    if (logDetections)
                    {
                        Debug.Log($"[EOCameraSim] Visual Acquisition: {contact}");
                    }
                }
            }
        }

        #region Optical Extinction and Visibility Model

        /// <summary>
        /// Computes detection probability accounting for distance falloff, time-of-day lighting,
        /// atmospheric extinction (fog/rain), and thermal contrast in FLIR mode.
        /// </summary>
        public float CalculateOpticalVisibility(float distance, DroneAgent agent)
        {
            // 1. Angular resolution factor: target visual footprint shrinks with distance
            float distanceRatio = distance / maxDetectionRange;
            float resolutionFactor = Mathf.Clamp01(1.0f - Mathf.Pow(distanceRatio, 1.3f));

            if (spectralBand == CameraSpectralBand.InfraredThermalFLIR)
            {
                // Thermal FLIR mode: penetrates darkness; depends on thermal contrast delta & motor heat
                float baseThermalVisibility = sensorProfile.thermalContrast;
                // Atmospheric moisture/fog still absorbs thermal IR slightly
                float thermalFogAttenuation = Mathf.Clamp01(sensorProfile.opticalVisibility * 0.5f + 0.5f);
                return Mathf.Clamp01(resolutionFactor * baseThermalVisibility * thermalFogAttenuation);
            }
            else
            {
                // Visible Daylight mode: heavily influenced by TimeOfDay and optical fog/rain
                float daylightFactor = GetTimeOfDayIllumination();

                // Beer-Lambert atmospheric optical transmission: T = exp(-extinction * distance)
                float extinctionCoeff = (1.0f - sensorProfile.opticalVisibility) * 0.003f;
                float atmosphericTransmission = Mathf.Exp(-extinctionCoeff * distance);

                float finalPd = resolutionFactor * daylightFactor * atmosphericTransmission;
                return Mathf.Clamp01(finalPd);
            }
        }

        private float GetTimeOfDayIllumination()
        {
            switch (timeOfDay)
            {
                case TimeOfDay.Day:
                    return 1.0f;
                case TimeOfDay.Dawn:
                case TimeOfDay.Dusk:
                    return 0.55f;
                case TimeOfDay.Night:
                    return 0.08f; // Extremely low probability of spotting small dark drones at night without IR
                default:
                    return 1.0f;
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmoFov) return;

            Gizmos.color = (spectralBand == CameraSpectralBand.InfraredThermalFLIR) 
                ? new Color(1f, 0.4f, 0f, 0.3f) 
                : new Color(1f, 1f, 0f, 0.3f);

            Vector3 camPos = transform.position;
            Vector3 forward = transform.forward * maxDetectionRange;
            float halfFov = fieldOfViewDeg * 0.5f;

            Quaternion leftRay = Quaternion.AngleAxis(-halfFov, transform.up);
            Quaternion rightRay = Quaternion.AngleAxis(halfFov, transform.up);
            Quaternion upRay = Quaternion.AngleAxis(-halfFov * 0.6f, transform.right);
            Quaternion downRay = Quaternion.AngleAxis(halfFov * 0.6f, transform.right);

            Gizmos.DrawRay(camPos, leftRay * forward);
            Gizmos.DrawRay(camPos, rightRay * forward);
            Gizmos.DrawRay(camPos, upRay * forward);
            Gizmos.DrawRay(camPos, downRay * forward);
        }

        #endregion
    }
}
