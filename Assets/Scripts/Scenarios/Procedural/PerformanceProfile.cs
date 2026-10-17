using System;
using UnityEngine;
using DroneTrainer.Scoring;

namespace DroneTrainer.Scenarios.Procedural
{
    /// <summary>
    /// Tracks granular performance weaknesses and adaptive difficulty ratings for an operator.
    /// Used by DifficultyController to tailor procedural scenario generation to training deficits.
    /// Weakness values are normalized between 0.0 (mastered) and 1.0 (severe weakness).
    /// </summary>
    [Serializable]
    public class PerformanceProfile
    {
        public string traineeId = "OPERATOR-ALPHA";

        [Tooltip("Current adaptive difficulty level for this trainee (0.0 = Novice, 1.0 = Elite).")]
        [Range(0f, 1f)]
        public float currentDifficulty = 0.30f;

        [Header("Granular Category Weaknesses (0.0 = Strong, 1.0 = Weak)")]
        [Range(0f, 1f)] public float detectionLatencyWeakness = 0.50f;
        [Range(0f, 1f)] public float decoyDiscriminationWeakness = 0.50f;
        [Range(0f, 1f)] public float nightOperationsWeakness = 0.50f;
        [Range(0f, 1f)] public float degradedSensorWeakness = 0.50f;
        [Range(0f, 1f)] public float swarmSaturationWeakness = 0.50f;

        [Header("Career Statistics")]
        public int sessionsEvaluated = 0;
        public float careerAverageScore = 0f;
        public int consecutiveHighPasses = 0;
        public int consecutiveFailures = 0;

        public PerformanceProfile() { }

        public PerformanceProfile(string traineeId, float initialDifficulty = 0.30f)
        {
            this.traineeId = traineeId;
            this.currentDifficulty = Mathf.Clamp01(initialDifficulty);
            this.detectionLatencyWeakness = 0.5f;
            this.decoyDiscriminationWeakness = 0.5f;
            this.nightOperationsWeakness = 0.5f;
            this.degradedSensorWeakness = 0.5f;
            this.swarmSaturationWeakness = 0.5f;
        }

        /// <summary>
        /// Analyzes a completed session report to update granular weakness metrics.
        /// </summary>
        public void UpdateWeaknesses(SessionScoreReport report)
        {
            if (report == null) return;

            sessionsEvaluated++;
            float n = sessionsEvaluated;
            careerAverageScore = ((careerAverageScore * (n - 1)) + report.overallScorePercentage) / n;

            // 1. Detection latency weakness: low detection score indicates slow reaction
            float detectDeficit = 1.0f - Mathf.Clamp01(report.averageDetectionScore / 100f);
            detectionLatencyWeakness = Mathf.Lerp(detectionLatencyWeakness, detectDeficit, 0.35f);

            // 2. Decoy / ROE discrimination weakness: ROE violations or misclassifying decoys
            float roeDeficit = (report.roeViolations > 0) ? 0.9f : (1.0f - Mathf.Clamp01(report.averageEngagementScore / 100f));
            decoyDiscriminationWeakness = Mathf.Lerp(decoyDiscriminationWeakness, roeDeficit, 0.40f);

            // 3. Classification fidelity
            float classDeficit = 1.0f - Mathf.Clamp01(report.averageClassificationScore / 100f);

            // 4. Swarm saturation weakness: if multi-target count was high and score was low
            if (report.totalContactsEvaluated >= 5)
            {
                float saturationDeficit = 1.0f - Mathf.Clamp01(report.overallScorePercentage / 100f);
                swarmSaturationWeakness = Mathf.Lerp(swarmSaturationWeakness, saturationDeficit, 0.30f);
            }

            ClampValues();
        }

        public void ClampValues()
        {
            currentDifficulty = Mathf.Clamp01(currentDifficulty);
            detectionLatencyWeakness = Mathf.Clamp01(detectionLatencyWeakness);
            decoyDiscriminationWeakness = Mathf.Clamp01(decoyDiscriminationWeakness);
            nightOperationsWeakness = Mathf.Clamp01(nightOperationsWeakness);
            degradedSensorWeakness = Mathf.Clamp01(degradedSensorWeakness);
            swarmSaturationWeakness = Mathf.Clamp01(swarmSaturationWeakness);
        }
    }
}
