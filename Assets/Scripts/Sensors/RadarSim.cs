using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Core.Events;
using DroneTrainer.Drones;
using DroneTrainer.Scenarios;

namespace DroneTrainer.Sensors
{
    /// <summary>
    /// Simulates a 3D pulse-Doppler radar system with periodic sweeping, range-dependent
    /// detection probability based on Radar Cross Section (RCS), clutter noise, jamming attenuation,
    /// measurement jitter, and false alarm generation. Outputs Contact objects via EventBus.
    /// </summary>
    public class RadarSim : MonoBehaviour
    {
        [Header("Radar Specifications")]
        [SerializeField] private string radarId = "RADAR-PULSE-01";
        [SerializeField] [Range(10f, 120f)] private float sweepRpm = 60f; // 60 RPM = 1.0s full 360 sweep
        [SerializeField] [Range(200f, 5000f)] private float maxInstrumentedRange = 2500f;
        [SerializeField] [Range(10f, 90f)] private float verticalBeamWidthDeg = 45f;
        [SerializeField] private float referenceDetectionRangeMeters = 1000f; // Range at which 0.01 m2 RCS has 50% Pd under clear conditions

        [Header("Active Environmental Profile")]
        [SerializeField] private SensorProfile sensorProfile = new SensorProfile();

        [Header("Measurement Noise & Jitter")]
        [SerializeField] [Range(0.5f, 20f)] private float rangeNoiseMeters = 3.5f;
        [SerializeField] [Range(0.1f, 5f)] private float bearingNoiseDeg = 0.8f;

        [Header("Clutter & False Alarms")]
        [SerializeField] [Range(0f, 10f)] private float maxFalseAlarmsPerSweep = 3f;
        [SerializeField] private float falseAlarmMaxRange = 1200f;

        [Header("Debug & Visuals")]
        [SerializeField] private bool logDetections = false;
        [SerializeField] private bool showGizmos = true;

        // Runtime state
        private float currentAzimuthDeg = 0f;
        private float sweepTimer = 0f;
        private float sweepInterval;
        private int trackCounter = 1;

        public SensorProfile ActiveProfile
        {
            get => sensorProfile;
            set => sensorProfile = value ?? new SensorProfile();
        }

        public float BearingNoiseDeg => bearingNoiseDeg;

        private void Awake()
        {
            CalculateSweepInterval();
        }

        private void OnValidate()
        {
            CalculateSweepInterval();
        }

        private void CalculateSweepInterval()
        {
            sweepInterval = 60f / Mathf.Max(1f, sweepRpm);
        }

        private void Update()
        {
            float sweepDelta = (360f / sweepInterval) * Time.deltaTime;
            currentAzimuthDeg = (currentAzimuthDeg + sweepDelta) % 360f;

            sweepTimer += Time.deltaTime;
            if (sweepTimer >= sweepInterval)
            {
                sweepTimer = 0f;
                ExecuteRadarSweep();
            }
        }

        /// <summary>
        /// Executes a full 360-degree radar scan, evaluating real targets and generating false blips.
        /// </summary>
        public void ExecuteRadarSweep()
        {
            float simTime = Time.time;
            Vector3 radarPos = transform.position;

            // 1. Scan real drone agents in the world
            DroneAgent[] agents = FindObjectsByType<DroneAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < agents.Length; i++)
            {
                DroneAgent agent = agents[i];
                if (agent == null || !agent.gameObject.activeInHierarchy || agent.IsNeutralized) continue;

                Vector3 targetPos = agent.transform.position;
                Vector3 toTarget = targetPos - radarPos;
                float range = toTarget.magnitude;

                if (range > maxInstrumentedRange) continue;

                // Check vertical elevation beam limits
                float elevationAngle = Mathf.Asin(Mathf.Clamp(toTarget.y / range, -1f, 1f)) * Mathf.Rad2Deg;
                if (Mathf.Abs(elevationAngle) > verticalBeamWidthDeg * 0.5f) continue;

                // Determine RCS for threat type
                float rcs = GetRcsForDrone(agent);

                // Calculate detection probability (Pd)
                float pd = CalculateDetectionProbability(range, rcs);

                // Probabilistic roll
                if (Random.value <= pd)
                {
                    // Compute Signal-to-Noise Ratio (dB)
                    float snr = CalculateSnrDb(range, rcs);

                    // Add realistic measurement noise
                    Vector3 estimatedPos = ApplyMeasurementNoise(targetPos, range);

                    Contact contact = new Contact(
                        id: $"{radarId}-TRK-{trackCounter++:D4}",
                        estimatedPosition: estimatedPos,
                        confidence: Mathf.Clamp01(pd * (1f - (sensorProfile.radarNoise * 0.4f))),
                        sensorType: SensorType.Radar,
                        timestamp: simTime,
                        estimatedVelocity: agent.Velocity + Random.insideUnitSphere * (sensorProfile.radarNoise * 2f),
                        snrDb: snr,
                        isFalseAlarm: false,
                        trueDroneId: agent.DroneId
                    );

                    EventBus.Publish(new ContactDetectedEvent(contact, simTime));

                    if (logDetections)
                    {
                        Debug.Log($"[{radarId}] Detected: {contact}");
                    }
                }
            }

            // 2. Generate clutter-induced false alarms
            GenerateClutterFalseAlarms(simTime);
        }

        #region Radar Equation & Probability Model

        /// <summary>
        /// Calculates detection probability Pd based on target RCS, range R^4 fall-off,
        /// radar noise, clutter density, and RF jamming attenuation.
        /// </summary>
        public float CalculateDetectionProbability(float rangeMeters, float rcsMetersSquared)
        {
            if (rangeMeters <= 10f) return 0.99f;

            // Base radar range equation: received power drops with R^4
            // Scaled relative to reference range where reference RCS produces 50% Pd
            float r4Normalized = Mathf.Pow(referenceDetectionRangeMeters / rangeMeters, 4f);
            float baseSignalStrength = (rcsMetersSquared / 0.01f) * r4Normalized;

            // Environmental degradation: noise, clutter, and jamming attenuate effective SNR
            float effectiveNoiseFloor = 1.0f + (sensorProfile.radarNoise * 3.0f) + (sensorProfile.rfJammingLevel * 4.0f);
            float clutterPenalty = 1.0f - (sensorProfile.radarClutter * 0.45f);

            float snrLinear = (baseSignalStrength / effectiveNoiseFloor) * clutterPenalty;

            // Sigmoidal detection curve mapping linear SNR to Pd in [0, 1]
            float pd = 1.0f / (1.0f + Mathf.Exp(-1.5f * (Mathf.Log(Mathf.Max(0.001f, snrLinear)))));
            return Mathf.Clamp01(pd);
        }

        private float CalculateSnrDb(float rangeMeters, float rcs)
        {
            float r4 = Mathf.Pow(referenceDetectionRangeMeters / Mathf.Max(10f, rangeMeters), 4f);
            float linearSnr = (rcs / 0.01f) * r4 / (1f + sensorProfile.radarNoise * 2f);
            return 10f * Mathf.Log10(Mathf.Max(0.1f, linearSnr));
        }

        private float GetRcsForDrone(DroneAgent agent)
        {
            // Default RCS based on threat naming or size; tactical drones have lower radar signature
            string name = agent.name.ToLower();
            if (name.Contains("bird") || name.Contains("decoy")) return 0.003f;
            if (name.Contains("fpv")) return 0.008f;
            if (name.Contains("fixedwing")) return 0.20f;
            if (name.Contains("tactical")) return 0.025f;
            if (name.Contains("ultralight")) return 1.80f;

            return 0.015f; // Standard commercial quadcopter
        }

        private Vector3 ApplyMeasurementNoise(Vector3 realPos, float range)
        {
            // Measurement error scales slightly with range and environmental noise
            float errorScale = (range / 1000f) * (1f + sensorProfile.radarNoise * 1.5f);
            Vector3 jitter = Random.insideUnitSphere * (rangeNoiseMeters * errorScale);
            return realPos + jitter;
        }

        #endregion

        #region False Alarm Generation

        private void GenerateClutterFalseAlarms(float simTime)
        {
            // Clutter and noise dictate expected false alarm rate
            float falseAlarmRate = maxFalseAlarmsPerSweep * (sensorProfile.radarClutter * 0.7f + sensorProfile.radarNoise * 0.3f);
            int falseAlarmsToSpawn = Mathf.FloorToInt(falseAlarmRate) + (Random.value < (falseAlarmRate % 1f) ? 1 : 0);

            for (int i = 0; i < falseAlarmsToSpawn; i++)
            {
                float randomRange = Random.Range(100f, falseAlarmMaxRange);
                float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float randomHeight = Random.Range(10f, 80f);

                Vector3 falsePos = transform.position + new Vector3(
                    Mathf.Cos(randomAngle) * randomRange,
                    randomHeight,
                    Mathf.Sin(randomAngle) * randomRange
                );

                Contact falseContact = new Contact(
                    id: $"{radarId}-CLUTTER-{Random.Range(1000, 9999)}",
                    estimatedPosition: falsePos,
                    confidence: Random.Range(0.20f, 0.45f), // lower confidence for clutter
                    sensorType: SensorType.Radar,
                    timestamp: simTime,
                    estimatedVelocity: Random.insideUnitSphere * 4f,
                    snrDb: Random.Range(2f, 7f),
                    isFalseAlarm: true,
                    trueDroneId: null
                );

                EventBus.Publish(new ContactDetectedEvent(falseContact, simTime));
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, maxInstrumentedRange);

            // Draw current sweep azimuth line
            Vector3 sweepDir = Quaternion.Euler(0f, currentAzimuthDeg, 0f) * Vector3.forward;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + sweepDir * (maxInstrumentedRange * 0.5f));
        }

        #endregion
    }
}
