using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneTrainer.Scenarios
{
    /// <summary>
    /// Data-driven scenario definition model containing environmental conditions,
    /// sensor degradation parameters, and scheduled threat waves.
    /// Serializes cleanly via both Unity JsonUtility and Newtonsoft.Json.
    /// </summary>
    [Serializable]
    public class ScenarioDefinition
    {
        [Tooltip("Unique scenario identifier.")]
        public string id = "scenario_default";

        [Tooltip("Human-readable scenario title displayed to trainee.")]
        public string name = "Untitled Scenario";

        [Tooltip("Mission briefing and tactical context description.")]
        public string description = string.Empty;

        [Tooltip("Target operating terrain environment.")]
        public TerrainType terrain = TerrainType.Urban;

        [Tooltip("Simulated time of day affecting ambient lighting.")]
        public TimeOfDay timeOfDay = TimeOfDay.Day;

        [Tooltip("Atmospheric weather conditions affecting sensor line-of-sight.")]
        public WeatherCondition weather = WeatherCondition.Clear;

        [Tooltip("Sensor degradation, clutter, noise, and jamming profile.")]
        public SensorProfile sensorProfile = new SensorProfile();

        [Tooltip("List of scheduled threat waves and flight paths.")]
        public List<ThreatSpawn> threatSpawns = new List<ThreatSpawn>();

        [Tooltip("Maximum scenario duration in seconds (0 = unlimited / until all threats resolved).")]
        public float timeLimitSeconds = 300.0f;

        [Tooltip("Baseline difficulty level (1 = Novice, 5 = Elite).")]
        [Range(1, 5)]
        public int difficultyRating = 1;

        public ScenarioDefinition()
        {
            sensorProfile = new SensorProfile();
            threatSpawns = new List<ThreatSpawn>();
        }

        public ScenarioDefinition(string id, string name, string description, 
                                  TerrainType terrain, TimeOfDay timeOfDay, WeatherCondition weather, 
                                  SensorProfile sensorProfile, List<ThreatSpawn> threatSpawns, 
                                  float timeLimitSeconds = 300f, int difficultyRating = 1)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.terrain = terrain;
            this.timeOfDay = timeOfDay;
            this.weather = weather;
            this.sensorProfile = sensorProfile ?? new SensorProfile();
            this.threatSpawns = threatSpawns ?? new List<ThreatSpawn>();
            this.timeLimitSeconds = timeLimitSeconds;
            this.difficultyRating = Mathf.Clamp(difficultyRating, 1, 5);
        }

        /// <summary>
        /// Calculates the total count of all airborne targets across all spawn waves.
        /// </summary>
        public int GetTotalDroneCount()
        {
            if (threatSpawns == null) return 0;
            int total = 0;
            for (int i = 0; i < threatSpawns.Count; i++)
            {
                total += threatSpawns[i].count;
            }
            return total;
        }

        /// <summary>
        /// Calculates the number of confirmed hostile drones.
        /// </summary>
        public int GetHostileDroneCount()
        {
            if (threatSpawns == null) return 0;
            int total = 0;
            for (int i = 0; i < threatSpawns.Count; i++)
            {
                if (threatSpawns[i].isHostile)
                {
                    total += threatSpawns[i].count;
                }
            }
            return total;
        }

        /// <summary>
        /// Calculates the number of non-hostile decoys or friendly/civilian drones.
        /// </summary>
        public int GetDecoyCount()
        {
            return GetTotalDroneCount() - GetHostileDroneCount();
        }

        /// <summary>
        /// Validates scenario constraints and corrects null or out-of-bounds fields.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = $"scenario_{Guid.NewGuid().ToString().Substring(0, 6)}";
            }

            if (sensorProfile == null)
            {
                sensorProfile = new SensorProfile();
            }
            sensorProfile.Validate();

            if (threatSpawns == null)
            {
                threatSpawns = new List<ThreatSpawn>();
            }

            for (int i = 0; i < threatSpawns.Count; i++)
            {
                threatSpawns[i]?.Validate();
            }

            timeLimitSeconds = Mathf.Max(30f, timeLimitSeconds);
            difficultyRating = Mathf.Clamp(difficultyRating, 1, 5);
        }
    }
}
