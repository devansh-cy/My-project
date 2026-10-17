using System.Collections.Generic;
using UnityEngine;

namespace DroneTrainer.Scenarios
{
    /// <summary>
    /// Test harness component to verify scenario loading from StreamingAssets in the Unity Editor or at runtime.
    /// Attach to an empty GameObject in a test scene and observe Unity Console logs.
    /// </summary>
    public class ScenarioLoaderTest : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string specificScenarioFileName = "single_quadcopter_day.json";
        [SerializeField] private JsonBackend jsonBackend = JsonBackend.Auto;

        [ContextMenu("Test Load Specific Scenario")]
        public void TestLoadSpecific()
        {
            Debug.Log($"--- [ScenarioLoaderTest] Testing loading: {specificScenarioFileName} ---");
            ScenarioDefinition scenario = ScenarioLoader.LoadScenarioFromFile(specificScenarioFileName, jsonBackend);
            LogScenarioDetails(scenario);
        }

        [ContextMenu("Test Load All Scenarios")]
        public void TestLoadAll()
        {
            Debug.Log("--- [ScenarioLoaderTest] Testing loading all scenarios from StreamingAssets ---");
            List<ScenarioDefinition> scenarios = ScenarioLoader.LoadAllScenarios(jsonBackend);

            foreach (var scenario in scenarios)
            {
                LogScenarioDetails(scenario);
            }
        }

        private void Start()
        {
            TestLoadAll();
        }

        private void LogScenarioDetails(ScenarioDefinition scenario)
        {
            if (scenario == null)
            {
                Debug.LogError("[ScenarioLoaderTest] Scenario loaded as null!");
                return;
            }

            Debug.Log($"[Scenario Verified] ID: '{scenario.id}' | Title: '{scenario.name}'\n" +
                      $"Terrain: {scenario.terrain} | TimeOfDay: {scenario.timeOfDay} | Weather: {scenario.weather}\n" +
                      $"Noise: {scenario.sensorProfile.radarNoise:F2} | Clutter: {scenario.sensorProfile.radarClutter:F2} | Jamming: {scenario.sensorProfile.rfJammingLevel:F2}\n" +
                      $"Total Drones: {scenario.GetTotalDroneCount()} (Hostile: {scenario.GetHostileDroneCount()}, Decoys: {scenario.GetDecoyCount()}) | Waves: {scenario.threatSpawns.Count}");
        }
    }
}
