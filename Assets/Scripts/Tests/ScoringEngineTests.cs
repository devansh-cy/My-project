using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DroneTrainer.Logging;
using DroneTrainer.Scenarios;
using DroneTrainer.Scoring;
using DroneTrainer.Trainee;

namespace DroneTrainer.Tests
{
    /// <summary>
    /// Comprehensive unit test suite validating the EventLogger, DecisionTree scoring rules,
    /// detection latency falloff, classification accuracy, and ROE compliance.
    /// Compatible with Unity Test Runner (EditMode/PlayMode) and standalone test runners.
    /// </summary>
    [TestFixture]
    public class ScoringEngineTests
    {
        private ScoringConfig testConfig;
        private DecisionTreeScorer scorer;

        [SetUp]
        public void Setup()
        {
            testConfig = new ScoringConfig
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

            scorer = new DecisionTreeScorer(testConfig);
        }

        #region Detection Time Score Tests

        [Test]
        public void ComputeDetectionTimeScore_WithinOptimalThreshold_ReturnsMaxScore()
        {
            float scoreFast = scorer.ComputeDetectionTimeScore(1.5f);
            float scoreBoundary = scorer.ComputeDetectionTimeScore(3.0f);

            Assert.AreEqual(100f, scoreFast, 0.01f);
            Assert.AreEqual(100f, scoreBoundary, 0.01f);
        }

        [Test]
        public void ComputeDetectionTimeScore_ExceedingTimeout_ReturnsZero()
        {
            float scoreTimeout = scorer.ComputeDetectionTimeScore(20.0f);
            float scoreVeryLate = scorer.ComputeDetectionTimeScore(35.0f);

            Assert.AreEqual(0f, scoreTimeout, 0.01f);
            Assert.AreEqual(0f, scoreVeryLate, 0.01f);
        }

        [Test]
        public void ComputeDetectionTimeScore_MidwayLatency_InterpolatesLinearly()
        {
            // Midway between 3.0s and 20.0s is 11.5s -> should yield exactly 50%
            float scoreMidway = scorer.ComputeDetectionTimeScore(11.5f);
            Assert.AreEqual(50f, scoreMidway, 0.5f);
        }

        #endregion

        #region Classification Match Tests

        [Test]
        public void EvaluateClassificationMatch_Quadcopter_YieldsCorrect()
        {
            var truth = new DroneSpawnedEvent(1f, "DRONE-01", ThreatType.TacticalQuadcopter, Vector3.zero, true);
            var result = scorer.EvaluateClassificationMatch(truth, false, TraineeClassification.Quadcopter);

            Assert.AreEqual(ClassificationMatchResult.Correct, result);
            Assert.AreEqual(100f, scorer.ComputeClassificationScore(result));
        }

        [Test]
        public void EvaluateClassificationMatch_TacticalQuadClassifiedAsLoiteringMunition_YieldsPartial()
        {
            var truth = new DroneSpawnedEvent(1f, "DRONE-01", ThreatType.TacticalQuadcopter, Vector3.zero, true);
            var result = scorer.EvaluateClassificationMatch(truth, false, TraineeClassification.LoiteringMunition);

            Assert.AreEqual(ClassificationMatchResult.Partial, result);
            Assert.AreEqual(50f, scorer.ComputeClassificationScore(result));
        }

        [Test]
        public void EvaluateClassificationMatch_BirdDecoyMisclassifiedAsQuadcopter_YieldsWrong()
        {
            var truth = new DroneSpawnedEvent(1f, "DECOY-01", ThreatType.BirdDecoy, Vector3.zero, false);
            var result = scorer.EvaluateClassificationMatch(truth, false, TraineeClassification.Quadcopter);

            Assert.AreEqual(ClassificationMatchResult.Wrong, result);
            Assert.AreEqual(0f, scorer.ComputeClassificationScore(result));
        }

        [Test]
        public void EvaluateClassificationMatch_FalseAlarmClassifiedAsDrone_YieldsPenalty()
        {
            var result = scorer.EvaluateClassificationMatch(null, true, TraineeClassification.Quadcopter);

            Assert.AreEqual(ClassificationMatchResult.FalseAlarmMisidentified, result);
            Assert.AreEqual(-30f, scorer.ComputeClassificationScore(result));
        }

        #endregion

        #region Rules of Engagement (ROE) Tests

        [Test]
        public void EvaluateEngagementRoe_HostileEngaged_AwardsFullPoints()
        {
            var truth = new DroneSpawnedEvent(2f, "THREAT-01", ThreatType.FPVKamikaze, Vector3.zero, true);
            float score = scorer.EvaluateEngagementRoe(truth, false, ResponseOption.Engage, out string notes);

            Assert.AreEqual(100f, score);
            StringAssert.Contains("Adherence", notes);
        }

        [Test]
        public void EvaluateEngagementRoe_HostileIgnored_PenalizesUnengagedThreat()
        {
            var truth = new DroneSpawnedEvent(2f, "THREAT-01", ThreatType.FPVKamikaze, Vector3.zero, true);
            float score = scorer.EvaluateEngagementRoe(truth, false, ResponseOption.Monitor, out string notes);

            Assert.AreEqual(-60f, score);
            StringAssert.Contains("Failure", notes);
        }

        [Test]
        public void EvaluateEngagementRoe_BirdDecoyDestroyed_InflictsSeverePenalty()
        {
            var truth = new DroneSpawnedEvent(3f, "BIRD-01", ThreatType.BirdDecoy, Vector3.zero, false);
            float score = scorer.EvaluateEngagementRoe(truth, false, ResponseOption.Engage, out string notes);

            Assert.AreEqual(-80f, score);
            StringAssert.Contains("Severe ROE Breach", notes);
        }

        [Test]
        public void EvaluateEngagementRoe_BirdDecoyMonitored_AwardsFullPoints()
        {
            var truth = new DroneSpawnedEvent(3f, "BIRD-01", ThreatType.BirdDecoy, Vector3.zero, false);
            float score = scorer.EvaluateEngagementRoe(truth, false, ResponseOption.Monitor, out string notes);

            Assert.AreEqual(100f, score);
            StringAssert.Contains("Withheld fire", notes);
        }

        [Test]
        public void EvaluateEngagementRoe_CivilianAircraftEngaged_InflictsCriticalViolation()
        {
            var truth = new DroneSpawnedEvent(4f, "CIV-01", ThreatType.CivilianUltralight, Vector3.zero, false);
            float score = scorer.EvaluateEngagementRoe(truth, false, ResponseOption.Engage, out string notes);

            Assert.AreEqual(-100f, score);
            StringAssert.Contains("Collateral damage", notes);
        }

        [Test]
        public void EvaluateEngagementRoe_HostileJammed_AwardsFullPoints()
        {
            var truth = new DroneSpawnedEvent(2.5f, "THREAT-02", ThreatType.TacticalQuadcopter, Vector3.zero, true);
            float score = scorer.EvaluateEngagementRoe(truth, false, ResponseOption.Jam, out string notes);

            Assert.AreEqual(100f, score);
            StringAssert.Contains("jamming", notes.ToLower());
        }

        [Test]
        public void EvaluateEngagementRoe_HostileNetted_AwardsFullPoints()
        {
            var truth = new DroneSpawnedEvent(3.0f, "THREAT-03", ThreatType.CommercialQuadcopter, Vector3.zero, true);
            float score = scorer.EvaluateEngagementRoe(truth, false, ResponseOption.Net, out string notes);

            Assert.AreEqual(100f, score);
            StringAssert.Contains("net", notes.ToLower());
        }

        #endregion

        #region EventLogger & Serialization Roundtrip Tests

        [Test]
        public void SessionLog_JsonSerializationRoundtrip_PreservesData()
        {
            var session = new SessionLog("TEST-SESSION-99", "scenario_test", "TEST-OPERATOR");
            session.AddEntry(1.5f, "DroneSpawnedEvent", "DRONE-01", "Spawned target", "{}");
            session.AddEntry(3.2f, "TraineeActionEvent", "DRONE-01", "Classified as Quadcopter", "{}");
            session.FinalizeSession(15.0f);

            string json = session.ToJson(true);
            Assert.IsNotNull(json);

            SessionLog deserialized = SessionLog.FromJson(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(session.sessionId, deserialized.sessionId);
            Assert.AreEqual(session.scenarioId, deserialized.scenarioId);
            Assert.AreEqual(session.entries.Count, deserialized.entries.Count);
            Assert.AreEqual(1.5f, deserialized.entries[0].timestamp, 0.01f);
        }

        #endregion
    }
}
