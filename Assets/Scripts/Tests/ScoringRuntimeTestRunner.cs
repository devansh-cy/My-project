using UnityEngine;
using DroneTrainer.Scoring;

namespace DroneTrainer.Tests
{
    /// <summary>
    /// Runtime test runner that executes the scoring and serialization test suite
    /// in the Unity Editor or standalone builds, printing validation logs.
    /// </summary>
    public class ScoringRuntimeTestRunner : MonoBehaviour
    {
        [SerializeField] private bool runTestsOnStart = true;

        private void Start()
        {
            if (runTestsOnStart)
            {
                RunAllTests();
            }
        }

        [ContextMenu("Run Scoring Unit Tests")]
        public void RunAllTests()
        {
            Debug.Log("<color=#00e5ff><b>=== STARTING SCORING & TELEMETRY UNIT TESTS ===</b></color>");
            var suite = new ScoringEngineTests();
            int passed = 0;
            int total = 0;

            void Run(string testName, System.Action testAction)
            {
                total++;
                try
                {
                    suite.Setup();
                    testAction.Invoke();
                    passed++;
                    Debug.Log($"<color=#55ff55>[PASS]</color> {testName}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"<color=#ff3333>[FAIL]</color> {testName}: {ex.Message}");
                }
            }

            Run("DetectionTimeScore_WithinOptimalThreshold", suite.ComputeDetectionTimeScore_WithinOptimalThreshold_ReturnsMaxScore);
            Run("DetectionTimeScore_ExceedingTimeout", suite.ComputeDetectionTimeScore_ExceedingTimeout_ReturnsZero);
            Run("DetectionTimeScore_MidwayLatency", suite.ComputeDetectionTimeScore_MidwayLatency_InterpolatesLinearly);

            Run("ClassificationMatch_Quadcopter_Correct", suite.EvaluateClassificationMatch_Quadcopter_YieldsCorrect);
            Run("ClassificationMatch_TacticalQuad_Partial", suite.EvaluateClassificationMatch_TacticalQuadClassifiedAsLoiteringMunition_YieldsPartial);
            Run("ClassificationMatch_BirdDecoy_Wrong", suite.EvaluateClassificationMatch_BirdDecoyMisclassifiedAsQuadcopter_YieldsWrong);
            Run("ClassificationMatch_FalseAlarm_Penalty", suite.EvaluateClassificationMatch_FalseAlarmClassifiedAsDrone_YieldsPenalty);

            Run("EngagementRoe_HostileEngaged_FullPoints", suite.EvaluateEngagementRoe_HostileEngaged_AwardsFullPoints);
            Run("EngagementRoe_HostileIgnored_Penalized", suite.EvaluateEngagementRoe_HostileIgnored_PenalizesUnengagedThreat);
            Run("EngagementRoe_BirdDecoyDestroyed_SeverePenalty", suite.EvaluateEngagementRoe_BirdDecoyDestroyed_InflictsSeverePenalty);
            Run("EngagementRoe_BirdDecoyMonitored_FullPoints", suite.EvaluateEngagementRoe_BirdDecoyMonitored_AwardsFullPoints);
            Run("EngagementRoe_CivilianAircraftEngaged_CriticalViolation", suite.EvaluateEngagementRoe_CivilianAircraftEngaged_InflictsCriticalViolation);

            Run("SessionLog_JsonSerializationRoundtrip", suite.SessionLog_JsonSerializationRoundtrip_PreservesData);

            // Procedural Generator & Difficulty Controller Tests
            var procSuite = new ProceduralGeneratorTests();
            Run("Procedural_SameSeedProducesIdenticalScenarios", procSuite.Generate_SameSeedAndDifficulty_ProducesExactlyIdenticalScenarios);
            Run("Procedural_DifferentSeedsProduceDistinctScenarios", procSuite.Generate_DifferentSeeds_ProduceDistinctScenarios);
            Run("Procedural_DifficultyScalingIncreasesThreatsAndJamming", procSuite.Generate_DifficultyScaling_HigherDifficultyIncreasesThreatsAndJamming);
            Run("DifficultyController_PromotionRuleIncreasesDifficulty", procSuite.DifficultyController_PromotionRule_IncreasesDifficultyOnHighScore);
            Run("DifficultyController_DemotionRuleDecreasesDifficulty", procSuite.DifficultyController_DemotionRule_DecreasesDifficultyOnLowScore);
            Run("Procedural_WeaknessBiasElevatesDecoyRatio", procSuite.ScenarioGenerator_WeaknessBias_ElevatesDecoyRatioWhenDecoyWeaknessHigh);

            Debug.Log($"<color=#00e5ff><b>=== ALL UNIT TESTS FINISHED: {passed}/{total} PASSED ===</b></color>");
        }
    }
}
