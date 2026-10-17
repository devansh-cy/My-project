using System;
using UnityEngine;

namespace DroneTrainer.Scenarios
{
    /// <summary>
    /// Configures environmental sensor degradation, noise floors, clutter, and active jamming.
    /// Values are normalized between 0.0 (clean/nominal) and 1.0 (extreme degradation).
    /// </summary>
    [Serializable]
    public class SensorProfile
    {
        [Tooltip("Radar background thermal noise floor (0.0 = clear, 1.0 = heavy noise).")]
        [Range(0f, 1f)]
        public float radarNoise = 0.05f;

        [Tooltip("Radar ground and urban building reflection clutter density (0.0 = flat open, 1.0 = heavy reflections).")]
        [Range(0f, 1f)]
        public float radarClutter = 0.10f;

        [Tooltip("Active RF jamming/interference intensity disrupting detection and telemetry (0.0 = none, 1.0 = full blackout).")]
        [Range(0f, 1f)]
        public float rfJammingLevel = 0.0f;

        [Tooltip("Optical sensor atmospheric visibility multiplier (1.0 = clear daylight, 0.0 = zero visibility in dense fog/night).")]
        [Range(0f, 1f)]
        public float opticalVisibility = 1.0f;

        [Tooltip("Thermal IR contrast delta between drone engines/batteries and environment (1.0 = sharp contrast, 0.0 = thermal washout).")]
        [Range(0f, 1f)]
        public float thermalContrast = 0.85f;

        [Tooltip("Background acoustic noise level from wind, traffic, or combat noise (0.0 = silent, 1.0 = deafening).")]
        [Range(0f, 1f)]
        public float acousticAmbientNoise = 0.15f;

        public SensorProfile() { }

        public SensorProfile(float radarNoise, float radarClutter, float rfJammingLevel, 
                             float opticalVisibility = 1.0f, float thermalContrast = 0.85f, float acousticAmbientNoise = 0.15f)
        {
            this.radarNoise = Mathf.Clamp01(radarNoise);
            this.radarClutter = Mathf.Clamp01(radarClutter);
            this.rfJammingLevel = Mathf.Clamp01(rfJammingLevel);
            this.opticalVisibility = Mathf.Clamp01(opticalVisibility);
            this.thermalContrast = Mathf.Clamp01(thermalContrast);
            this.acousticAmbientNoise = Mathf.Clamp01(acousticAmbientNoise);
        }

        /// <summary>
        /// Creates a pristine, un-degraded sensor baseline.
        /// </summary>
        public static SensorProfile Nominal => new SensorProfile(0.02f, 0.05f, 0.0f, 1.0f, 0.9f, 0.05f);

        /// <summary>
        /// Validates and clamps all normalized parameters within [0, 1].
        /// </summary>
        public void Validate()
        {
            radarNoise = Mathf.Clamp01(radarNoise);
            radarClutter = Mathf.Clamp01(radarClutter);
            rfJammingLevel = Mathf.Clamp01(rfJammingLevel);
            opticalVisibility = Mathf.Clamp01(opticalVisibility);
            thermalContrast = Mathf.Clamp01(thermalContrast);
            acousticAmbientNoise = Mathf.Clamp01(acousticAmbientNoise);
        }
    }
}
