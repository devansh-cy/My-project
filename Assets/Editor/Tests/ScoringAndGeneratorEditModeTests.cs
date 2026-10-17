using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DroneTrainer.Logging;
using DroneTrainer.Scenarios;
using DroneTrainer.Scenarios.Procedural;
using DroneTrainer.Scoring;
using DroneTrainer.Trainee;

namespace DroneTrainer.Tests
{
    /// <summary>
    /// EditMode unit tests executing inside the Unity Test Runner without entering Play Mode.
    /// Validates DecisionTree scoring models, Rules of Engagement (ROE) logic,
    /// and deterministic seed-based scenario generation.
    /// </summary>
    [TestFixture]
    public class ScoringAndGeneratorEditModeTests
    {
        private ScoringConfig scoringConfig;
        private DecisionTreeScorer scorer;

        [SetUp]
        public void Setup()
        {
            scoringConfig = new ScoringConfig
            {
                detectionTimeWeight = 0.30f,
                classificationWeight = 0.35f,
                engagementWeight = 0.35f,
                optimalDetectionTimeSeconds = 3.0f,
                maxDetectionTimeoutSeconds = 20.0f,
                correctClassificationPoints = 100f,
                partialClassificationPoints = 50f,
                wrongClassificationPoints = 0f,
                falseAlarmPenaltyPoints = -30f,
                successfulHostileNeutralizationPoints = 100f,
                correctNonThreatMonitorPoints = 100f,
                birdDecoyEngagementPenalty = -80f,
                civilianEngagementPenalty = -100f,
                unengagedLethalThreatPenalty = -60f
            };

            scorer = new DecisionTreeScorer(scoringConfig);
        }

        #region Scoring Decision-Tree EditMode Tests

        [Test]
        public void EditMode_DetectionLatency_FullScoreWithinThreshold()
        {
            float scoreFast = scorer.ComputeDetectionTimeScore(1.0f);
            float scoreLimit = scorer.ComputeDetectionTimeScore(3.0f);

            Assert.AreEqual(100f, scoreFast, 0.001f);
            Assert.AreEqual(100f, scoreLimit, 0.001f);
        }

        [Test]
        public void EditMode_DetectionLatency_ZeroScoreAfterTimeout()
        {
            float scoreLate = scorer.ComputeDetectionTimeScore(20.0f);
            float scoreVeryLate = scorer.ComputeDetectionTimeScore(45.0f);

            Assert.AreEqual(0f, scoreLate, 0.001f);
            Assert.AreEqual(0f, scoreVeryLate, 0.001f);
        }

        [Test]
        public void EditMode_Classification_EvaluatesExactAndPartialMatches()
        {
            var hostileQuad = new DroneSpawnedEvent(1f, "DRONE-01", ThreatType.TacticalQuadcopter, Vector3.zero, true);

            // Exact match
            var exactResult = scorer.EvaluateClassificationMatch(hostileQuad, false, TraineeClassification.Quadcopter);
            Assert.AreEqual(ClassificationMatchResult.Correct, exactResult);
            Assert.AreEqual(100f, scorer.ComputeClassificationScore(exactResult));

            // Partial match (Tactical Quad identified as Loitering Munition)
            var partialResult = scorer.EvaluateClassificationMatch(hostileQuad, false, TraineeClassification.LoiteringMunition);
            Assert.AreEqual(ClassificationMatchResult.Partial, partialResult);
            Assert.AreEqual(50f, scorer.ComputeClassificationScore(partialResult));

            // Wrong match (Tactical Quad identified as Bird)
            var wrongResult = scorer.EvaluateClassificationMatch(hostileQuad, false, TraineeClassification.Bird);
            Assert.AreEqual(ClassificationMatchResult.Wrong, wrongResult);
            Assert.AreEqual(0f, scorer.ComputeClassificationScore(wrongResult));
        }

        [Test]
        public void EditMode_ROE_EngagingBirdDecoy_InflictsPenalty()
        {
            var decoy = new DroneSpawnedEvent(2f, "BIRD-01", ThreatType.BirdDecoy, Vector3.zero, false);
            float score = scorer.EvaluateEngagementRoe(decoy, false, ResponseOption.Engage, out string notes);

            Assert.AreEqual(-80f, score);
            StringAssert.Contains("Severe ROE Breach", notes);
        }

        [Test]
        public void EditMode_ROE_EngagingCivilianAircraft_InflictsCriticalViolation()
        {
            var civilian = new DroneSpawnedEvent(2f, "CIV-01", ThreatType.CivilianUltralight, Vector3.zero, false);
            float score = scorer.EvaluateEngagementRoe(civilian, false, ResponseOption.Engage, out string notes);

            Assert.AreEqual(-100f, score);
            StringAssert.Contains("Collateral damage", notes);
        }

        [Test]
        public void EditMode_ROE_AuthorizingJammerAgainstHostile_AwardsMaxPoints()
        {
            var hostile = new DroneSpawnedEvent(2f, "THREAT-01", ThreatType.FPVKamikaze, Vector3.zero, true);
            float score = scorer.EvaluateEngagementRoe(hostile, false, ResponseOption.Jam, out string notes);

            Assert.AreEqual(100f, score);
            StringAssert.Contains("Adherence", notes);
        }

        [Test]
        public void EditMode_SessionLog_SerializationRoundtrip_MaintainsTelemetryIntegrity()
        {
            var session = new SessionLog("EDIT-SESSION-01", "scenario_test", "OP-TESTER");
            session.AddEntry(1.0f, "DroneSpawnedEvent", "TARGET-01", "Target spawned", "{}");
            session.AddEntry(4.5f, "TraineeActionEvent", "TARGET-01", "Engaged target", "{}");
            session.FinalizeSession(20.0f);

            string json = session.ToJson(false);
            Assert.IsNotEmpty(json);

            SessionLog restored = SessionLog.FromJson(json);
            Assert.IsNotNull(restored);
            Assert.AreEqual(session.sessionId, restored.sessionId);
            Assert.AreEqual(2, restored.entries.Count);
            Assert.AreEqual(4.5f, restored.entries[1].timestamp, 0.01f);
        }

        #endregion

        #region Scenario Generator Determinism EditMode Tests

        [Test]
        public void EditMode_Generator_IdenticalSeed_ProducesExactDuplicateScenario()
        {
            int seed = 88412;
            float difficulty = 0.72f;

            ScenarioDefinition firstRun = ScenarioGenerator.Generate(seed, difficulty);
            ScenarioDefinition secondRun = ScenarioGenerator.Generate(seed, difficulty);

            // Assert identity across environment
            Assert.AreEqual(firstRun.id, secondRun.id);
            Assert.AreEqual(firstRun.terrain, secondRun.terrain);
            Assert.AreEqual(firstRun.timeOfDay, secondRun.timeOfDay);
            Assert.AreEqual(firstRun.weather, secondRun.weather);

            // Assert identity across sensor profile
            Assert.AreEqual(firstRun.sensorProfile.radarNoise, secondRun.sensorProfile.radarNoise, 0.00001f);
            Assert.AreEqual(firstRun.sensorProfile.radarClutter, secondRun.sensorProfile.radarClutter, 0.00001f);
            Assert.AreEqual(firstRun.sensorProfile.rfJammingLevel, secondRun.sensorProfile.rfJammingLevel, 0.00001f);
            Assert.AreEqual(firstRun.sensorProfile.opticalVisibility, secondRun.sensorProfile.opticalVisibility, 0.00001f);
            Assert.AreEqual(firstRun.sensorProfile.thermalContrast, secondRun.sensorProfile.thermalContrast, 0.00001f);

            // Assert identity across all threat spawns
            Assert.AreEqual(firstRun.threatSpawns.Count, secondRun.threatSpawns.Count);
            for (int i = 0; i < firstRun.threatSpawns.Count; i++)
            {
                var waveA = firstRun.threatSpawns[i];
                var waveB = secondRun.threatSpawns[i];

                Assert.AreEqual(waveA.threatId, waveB.threatId);
                Assert.AreEqual(waveA.type, waveB.type);
                Assert.AreEqual(waveA.count, waveB.count);
                Assert.AreEqual(waveA.isHostile, waveB.isHostile);
                Assert.AreEqual(waveA.flightPattern, waveB.flightPattern);
                Assert.AreEqual(waveA.speed, waveB.speed, 0.0001f);
                Assert.AreEqual(waveA.spawnTime, waveB.spawnTime, 0.0001f);
                Assert.AreEqual(waveA.spawnPosition.x, waveB.spawnPosition.x, 0.0001f);
                Assert.AreEqual(waveA.spawnPosition.y, waveB.spawnPosition.y, 0.0001f);
                Assert.AreEqual(waveA.spawnPosition.z, waveB.spawnPosition.z, 0.0001f);
            }
        }

        [Test]
        public void EditMode_Generator_DifferentSeeds_ProduceDivergentScenarios()
        {
            ScenarioDefinition a = ScenarioGenerator.Generate(12345, 0.5f);
            ScenarioDefinition b = ScenarioGenerator.Generate(67890, 0.5f);

            Assert.AreNotEqual(a.id, b.id);
            // Spawn positions must differ
            Assert.AreNotEqual(a.threatSpawns[0].spawnPosition.x, b.threatSpawns[0].spawnPosition.x);
        }

        [Test]
        public void EditMode_Generator_DifficultyScaling_ControlsJammingAndCount()
        {
            int seed = 44556;
            ScenarioDefinition novice = ScenarioGenerator.Generate(seed, 0.15f);
            ScenarioDefinition elite = ScenarioGenerator.Generate(seed, 0.85f);

            Assert.GreaterOrEqual(elite.GetTotalDroneCount(), novice.GetTotalDroneCount());
            Assert.GreaterOrEqual(elite.sensorProfile.rfJammingLevel, novice.sensorProfile.rfJammingLevel);
            Assert.GreaterOrEqual(elite.sensorProfile.radarClutter, novice.sensorProfile.radarClutter);
        }

        #endregion
    }
}
