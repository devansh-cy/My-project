using NUnit.Framework;
using UnityEngine;
using DroneTrainer.Scenarios;
using DroneTrainer.Scenarios.Procedural;
using DroneTrainer.Scoring;

namespace DroneTrainer.Tests
{
    /// <summary>
    /// Unit test suite verifying deterministic scenario generation from seeds,
    /// difficulty parameter scaling, and adaptive weakness biasing rules.
    /// </summary>
    [TestFixture]
    public class ProceduralGeneratorTests
    {
        [Test]
        public void Generate_SameSeedAndDifficulty_ProducesExactlyIdenticalScenarios()
        {
            int testSeed = 42891;
            float difficulty = 0.65f;

            ScenarioDefinition scenarioA = ScenarioGenerator.Generate(testSeed, difficulty);
            ScenarioDefinition scenarioB = ScenarioGenerator.Generate(testSeed, difficulty);

            // Verify core environmental properties
            Assert.AreEqual(scenarioA.id, scenarioB.id);
            Assert.AreEqual(scenarioA.terrain, scenarioB.terrain);
            Assert.AreEqual(scenarioA.timeOfDay, scenarioB.timeOfDay);
            Assert.AreEqual(scenarioA.weather, scenarioB.weather);

            // Verify sensor degradation values
            Assert.AreEqual(scenarioA.sensorProfile.radarNoise, scenarioB.sensorProfile.radarNoise, 0.0001f);
            Assert.AreEqual(scenarioA.sensorProfile.radarClutter, scenarioB.sensorProfile.radarClutter, 0.0001f);
            Assert.AreEqual(scenarioA.sensorProfile.rfJammingLevel, scenarioB.sensorProfile.rfJammingLevel, 0.0001f);
            Assert.AreEqual(scenarioA.sensorProfile.opticalVisibility, scenarioB.sensorProfile.opticalVisibility, 0.0001f);

            // Verify threat wave count and threat kinematics
            Assert.AreEqual(scenarioA.threatSpawns.Count, scenarioB.threatSpawns.Count);
            for (int i = 0; i < scenarioA.threatSpawns.Count; i++)
            {
                var waveA = scenarioA.threatSpawns[i];
                var waveB = scenarioB.threatSpawns[i];

                Assert.AreEqual(waveA.threatId, waveB.threatId);
                Assert.AreEqual(waveA.type, waveB.type);
                Assert.AreEqual(waveA.count, waveB.count);
                Assert.AreEqual(waveA.isHostile, waveB.isHostile);
                Assert.AreEqual(waveA.speed, waveB.speed, 0.001f);
                Assert.AreEqual(waveA.spawnTime, waveB.spawnTime, 0.001f);
                Assert.AreEqual(waveA.flightPattern, waveB.flightPattern);
                Assert.AreEqual(waveA.spawnPosition.x, waveB.spawnPosition.x, 0.001f);
                Assert.AreEqual(waveA.spawnPosition.y, waveB.spawnPosition.y, 0.001f);
                Assert.AreEqual(waveA.spawnPosition.z, waveB.spawnPosition.z, 0.001f);
            }
        }

        [Test]
        public void Generate_DifferentSeeds_ProduceDistinctScenarios()
        {
            ScenarioDefinition scenarioA = ScenarioGenerator.Generate(11111, 0.5f);
            ScenarioDefinition scenarioB = ScenarioGenerator.Generate(99999, 0.5f);

            Assert.AreNotEqual(scenarioA.id, scenarioB.id);
            // Positions of initial spawns should differ
            Assert.AreNotEqual(scenarioA.threatSpawns[0].spawnPosition.x, scenarioB.threatSpawns[0].spawnPosition.x);
        }

        [Test]
        public void Generate_DifficultyScaling_HigherDifficultyIncreasesThreatsAndJamming()
        {
            int seed = 12345;
            ScenarioDefinition lowDiff = ScenarioGenerator.Generate(seed, 0.10f);
            ScenarioDefinition highDiff = ScenarioGenerator.Generate(seed, 0.90f);

            Assert.GreaterOrEqual(highDiff.GetTotalDroneCount(), lowDiff.GetTotalDroneCount());
            Assert.GreaterOrEqual(highDiff.sensorProfile.rfJammingLevel, lowDiff.sensorProfile.rfJammingLevel);
            Assert.GreaterOrEqual(highDiff.sensorProfile.radarClutter, lowDiff.sensorProfile.radarClutter);
        }

        [Test]
        public void DifficultyController_PromotionRule_IncreasesDifficultyOnHighScore()
        {
            var profile = new PerformanceProfile("TEST-OP", initialDifficulty: 0.40f);
            var report = new SessionScoreReport
            {
                overallScorePercentage = 88.0f,
                averageDetectionScore = 90f,
                averageClassificationScore = 85f,
                averageEngagementScore = 90f
            };

            var controller = new GameObject("TestDiffCtrl").AddComponent<DifficultyController>();
            // Simulate processing completed session
            controller.ProcessCompletedSession(report);

            Assert.Greater(controller.CurrentDifficulty, 0.40f);
            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void DifficultyController_DemotionRule_DecreasesDifficultyOnLowScore()
        {
            var profile = new PerformanceProfile("TEST-OP", initialDifficulty: 0.50f);
            var report = new SessionScoreReport
            {
                overallScorePercentage = 42.0f,
                averageDetectionScore = 40f,
                averageClassificationScore = 40f,
                averageEngagementScore = 45f
            };

            var controller = new GameObject("TestDiffCtrl").AddComponent<DifficultyController>();
            controller.ProcessCompletedSession(report);

            Assert.Less(controller.CurrentDifficulty, 0.50f);
            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void ScenarioGenerator_WeaknessBias_ElevatesDecoyRatioWhenDecoyWeaknessHigh()
        {
            int seed = 777;
            var baselineProfile = new PerformanceProfile("OP-STRONG") { decoyDiscriminationWeakness = 0.1f };
            var weakProfile = new PerformanceProfile("OP-WEAK") { decoyDiscriminationWeakness = 0.9f };

            ScenarioDefinition normalScenario = ScenarioGenerator.Generate(seed, 0.5f, baselineProfile);
            ScenarioDefinition biasedScenario = ScenarioGenerator.Generate(seed, 0.5f, weakProfile);

            Assert.GreaterOrEqual(biasedScenario.GetDecoyCount(), normalScenario.GetDecoyCount());
        }
    }
}
