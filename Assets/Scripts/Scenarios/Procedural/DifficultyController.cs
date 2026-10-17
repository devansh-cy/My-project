using System;
using System.IO;
using UnityEngine;
using DroneTrainer.Scoring;
using DroneTrainer.Scenarios;

namespace DroneTrainer.Scenarios.Procedural
{
    /// <summary>
    /// Controls adaptive training difficulty and procedural scenario progression.
    /// Evaluates trainee session scores against target performance bands, automatically
    /// adjusts difficulty ratings, and biases scenario generation toward diagnosed weaknesses.
    /// </summary>
    public class DifficultyController : MonoBehaviour
    {
        [Header("Trainee Identity")]
        [SerializeField] private string traineeId = "OPERATOR-ALPHA";

        [Header("Adaptive Progression Tuning")]
        [Tooltip("Minimum passing score threshold. Achieving above this increases difficulty.")]
        [SerializeField] [Range(50f, 95f)] private float promotionScoreThreshold = 80f;

        [Tooltip("Demotion score threshold. Scoring below this triggers remedial difficulty reduction.")]
        [SerializeField] [Range(30f, 75f)] private float demotionScoreThreshold = 55f;

        [Tooltip("Standard difficulty step delta per session.")]
        [SerializeField] [Range(0.01f, 0.20f)] private float difficultyStepDelta = 0.05f;

        [Tooltip("Consecutive passes required to trigger accelerated difficulty jumps.")]
        [SerializeField] private int consecutivePassesForAcceleration = 2;

        [Header("Current Runtime Profile")]
        [SerializeField] private PerformanceProfile activeProfile;

        private const string ProfileSubfolder = "TraineeProfiles";

        public PerformanceProfile ActiveProfile => activeProfile;
        public float CurrentDifficulty => activeProfile != null ? activeProfile.currentDifficulty : 0.3f;

        private void Awake()
        {
            LoadOrCreateProfile();
        }

        #region Profile Persistence

        public void LoadOrCreateProfile()
        {
            string folder = Path.Combine(Application.persistentDataPath, ProfileSubfolder);
            string filePath = Path.Combine(folder, $"{traineeId}_profile.json");

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    activeProfile = JsonUtility.FromJson<PerformanceProfile>(json);
                    activeProfile.ClampValues();
                    Debug.Log($"[DifficultyController] Loaded profile for '{traineeId}' (Difficulty: {activeProfile.currentDifficulty:P0})");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DifficultyController] Failed to parse profile file: {ex.Message}. Creating default.");
                }
            }

            activeProfile = new PerformanceProfile(traineeId, initialDifficulty: 0.30f);
            SaveProfile();
        }

        public void SaveProfile()
        {
            if (activeProfile == null) return;

            try
            {
                string folder = Path.Combine(Application.persistentDataPath, ProfileSubfolder);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string filePath = Path.Combine(folder, $"{traineeId}_profile.json");
                string json = JsonUtility.ToJson(activeProfile, true);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DifficultyController] Failed to save profile for '{traineeId}': {ex.Message}");
            }
        }

        #endregion

        #region Session Processing & Adaptation Rules

        /// <summary>
        /// Analyzes a completed training session report, adjusts difficulty, and updates weakness weights.
        /// </summary>
        public void ProcessCompletedSession(SessionScoreReport report)
        {
            if (report == null) return;
            if (activeProfile == null) LoadOrCreateProfile();

            float score = report.overallScorePercentage;
            float oldDiff = activeProfile.currentDifficulty;

            // 1. Evaluate difficulty progression rules
            if (score >= promotionScoreThreshold)
            {
                activeProfile.consecutiveHighPasses++;
                activeProfile.consecutiveFailures = 0;

                // Accelerated boost if on a consistent win streak
                float multiplier = (activeProfile.consecutiveHighPasses >= consecutivePassesForAcceleration) ? 1.5f : 1.0f;
                activeProfile.currentDifficulty += (difficultyStepDelta * multiplier);
                Debug.Log($"<color=#55ff55>[Difficulty Increased]</color> Score: {score:F1}% >= {promotionScoreThreshold:F0}%. New Diff: {activeProfile.currentDifficulty:P0} (+{activeProfile.currentDifficulty - oldDiff:P1})");
            }
            else if (score < demotionScoreThreshold)
            {
                activeProfile.consecutiveFailures++;
                activeProfile.consecutiveHighPasses = 0;

                float multiplier = (activeProfile.consecutiveFailures >= 2) ? 1.4f : 1.0f;
                activeProfile.currentDifficulty -= (difficultyStepDelta * multiplier);
                Debug.Log($"<color=#ff5555>[Difficulty Decreased]</color> Score: {score:F1}% < {demotionScoreThreshold:F0}%. New Diff: {activeProfile.currentDifficulty:P0} ({activeProfile.currentDifficulty - oldDiff:P1})");
            }
            else
            {
                // In acceptable middle band (e.g. 55% - 79%) -> maintain difficulty
                activeProfile.consecutiveHighPasses = 0;
                activeProfile.consecutiveFailures = 0;
                Debug.Log($"[Difficulty Maintained] Score: {score:F1}% in maintenance band. Diff: {activeProfile.currentDifficulty:P0}");
            }

            // 2. Update category weakness vector
            activeProfile.UpdateWeaknesses(report);
            activeProfile.ClampValues();

            SaveProfile();
        }

        #endregion

        #region Scenario Generation Interface

        /// <summary>
        /// Generates the next adaptive scenario biased toward the trainee's diagnosed weaknesses.
        /// If no seed is provided, generates a random seed.
        /// </summary>
        public ScenarioDefinition GenerateNextScenario(int? specificSeed = null)
        {
            if (activeProfile == null) LoadOrCreateProfile();

            int seed = specificSeed ?? UnityEngine.Random.Range(1000, 999999);
            float difficulty = activeProfile.currentDifficulty;

            ScenarioDefinition scenario = ScenarioGenerator.Generate(seed, difficulty, activeProfile);
            Debug.Log($"[DifficultyController] Generated adaptive scenario: '{scenario.name}' [Seed: {seed}, Diff: {difficulty:P0}]");
            return scenario;
        }

        #endregion
    }
}
