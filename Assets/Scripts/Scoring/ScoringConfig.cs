using System;
using UnityEngine;

namespace DroneTrainer.Scoring
{
    /// <summary>
    /// Classification match quality rating.
    /// </summary>
    public enum ClassificationMatchResult
    {
        Correct,
        Partial,
        Wrong,
        FalseAlarmMisidentified
    }

    /// <summary>
    /// Configurable weights and evaluation thresholds for the JSON-driven decision tree scoring engine.
    /// </summary>
    [Serializable]
    public class ScoringConfig
    {
        [Header("Composite Weights (Sum = 1.0)")]
        [Range(0f, 1f)] public float detectionTimeWeight = 0.30f;
        [Range(0f, 1f)] public float classificationWeight = 0.35f;
        [Range(0f, 1f)] public float engagementWeight = 0.35f;

        [Header("Detection Time Latency Curve (Seconds)")]
        [Tooltip("Response time in seconds or less achieving 100% score.")]
        public float optimalDetectionTimeSeconds = 3.5f;

        [Tooltip("Response time at which detection score drops to zero.")]
        public float maxDetectionTimeoutSeconds = 25.0f;

        [Header("Classification Points")]
        public float correctClassificationPoints = 100f;
        public float partialClassificationPoints = 50f;
        public float wrongClassificationPoints = 0f;
        public float falseAlarmPenaltyPoints = -30f;

        [Header("Rules of Engagement (ROE) Evaluation")]
        [Tooltip("Points awarded for neutralizing a confirmed hostile drone.")]
        public float successfulHostileNeutralizationPoints = 100f;

        [Tooltip("Points awarded for correctly monitoring a civilian or bird non-threat.")]
        public float correctNonThreatMonitorPoints = 100f;

        [Tooltip("Points for executing timely alert / warnings.")]
        public float timelyAlertPoints = 75f;

        [Tooltip("Severe ROE penalty for firing upon or destroying bird decoys.")]
        public float birdDecoyEngagementPenalty = -80f;

        [Tooltip("Severe ROE penalty for engaging civilian or friendly aircraft.")]
        public float civilianEngagementPenalty = -100f;

        [Tooltip("Penalty for failing to engage an incoming hostile drone before it impacts.")]
        public float unengagedLethalThreatPenalty = -60f;

        public static ScoringConfig Default => new ScoringConfig();

        public void Validate()
        {
            float totalWeight = detectionTimeWeight + classificationWeight + engagementWeight;
            if (totalWeight > 0.001f && !Mathf.Approximately(totalWeight, 1f))
            {
                detectionTimeWeight /= totalWeight;
                classificationWeight /= totalWeight;
                engagementWeight /= totalWeight;
            }

            optimalDetectionTimeSeconds = Mathf.Max(0.5f, optimalDetectionTimeSeconds);
            maxDetectionTimeoutSeconds = Mathf.Max(optimalDetectionTimeSeconds + 1f, maxDetectionTimeoutSeconds);
        }

        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static ScoringConfig FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Default;
            var config = JsonUtility.FromJson<ScoringConfig>(json);
            config.Validate();
            return config;
        }
    }
}
