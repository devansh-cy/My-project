using System;
using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Scenarios;

namespace DroneTrainer.Scenarios.Procedural
{
    /// <summary>
    /// Procedural scenario generator that synthesizes data-driven ScenarioDefinition assets
    /// from an integer seed and a continuous difficulty rating (0.0 to 1.0).
    /// Guarantee: The exact same seed and parameters will deterministically produce the exact same scenario.
    /// Supports weakness-biasing via PerformanceProfile.
    /// </summary>
    public static class ScenarioGenerator
    {
        /// <summary>
        /// Generates a complete ScenarioDefinition using a deterministic pseudo-random seed and difficulty rating.
        /// </summary>
        /// <param name="seed">Integer seed ensuring 100% reproducible scenario generation.</param>
        /// <param name="difficulty">Difficulty scaling factor between 0.0 (Introductory) and 1.0 (Elite Swarm).</param>
        /// <param name="profile">Optional performance profile used to bias generation toward operator training weaknesses.</param>
        public static ScenarioDefinition Generate(int seed, float difficulty, PerformanceProfile profile = null)
        {
            difficulty = Mathf.Clamp01(difficulty);

            // Seed a local System.Random instance to guarantee deterministic isolation
            System.Random rng = new System.Random(seed);

            string scenarioId = $"proc_seed_{seed}_diff_{Mathf.RoundToInt(difficulty * 100)}";
            string scenarioName = $"Procedural Ops {seed:X4} (Diff {difficulty:P0})";

            // 1. Determine Environmental Factors (Terrain, TimeOfDay, Weather)
            TerrainType terrain = SelectTerrain(rng, difficulty);
            TimeOfDay timeOfDay = SelectTimeOfDay(rng, difficulty, profile);
            WeatherCondition weather = SelectWeather(rng, difficulty, profile);

            // 2. Synthesize Sensor Profile with Degradation
            SensorProfile sensorProfile = GenerateSensorProfile(rng, difficulty, weather, timeOfDay, profile);

            // 3. Compute Threat Wave Parameters
            int totalThreatWaves = ComputeWaveCount(rng, difficulty, profile);
            float decoyRatio = ComputeDecoyRatio(rng, difficulty, profile);
            int approachVectorCount = ComputeApproachVectorCount(difficulty, profile);

            // 4. Generate Threat Spawns along Approach Angles
            List<ThreatSpawn> spawns = GenerateThreatWaves(rng, totalThreatWaves, decoyRatio, approachVectorCount, difficulty, profile);

            // 5. Compute Time Limit & Difficulty Rating
            float timeLimitSeconds = Mathf.Clamp(120f + (difficulty * 240f), 120f, 480f);
            int discreteDifficulty = Mathf.Clamp(Mathf.RoundToInt(difficulty * 4f) + 1, 1, 5);

            string description = $"Procedurally generated tactical engagement [Seed: {seed}]. " +
                                 $"Environment: {terrain}, {timeOfDay}, {weather}. " +
                                 $"Hostiles: {CountHostiles(spawns)}, Decoys: {CountDecoys(spawns)} across {approachVectorCount} approach vectors. " +
                                 $"RF Jamming: {sensorProfile.rfJammingLevel:P0}, Radar Clutter: {sensorProfile.radarClutter:P0}.";

            var scenario = new ScenarioDefinition(
                id: scenarioId,
                name: scenarioName,
                description: description,
                terrain: terrain,
                timeOfDay: timeOfDay,
                weather: weather,
                sensorProfile: sensorProfile,
                threatSpawns: spawns,
                timeLimitSeconds: timeLimitSeconds,
                difficultyRating: discreteDifficulty
            );

            scenario.Validate();
            return scenario;
        }

        #region Environment Deterministic Selection

        private static TerrainType SelectTerrain(System.Random rng, float difficulty)
        {
            TerrainType[] terrains = (TerrainType[])Enum.GetValues(typeof(TerrainType));
            // High difficulty favors cluttered urban and industrial environments
            if (difficulty > 0.65f && rng.NextDouble() < 0.70)
            {
                return (rng.NextDouble() < 0.5) ? TerrainType.Urban : TerrainType.Industrial;
            }
            return terrains[rng.Next(terrains.Length)];
        }

        private static TimeOfDay SelectTimeOfDay(System.Random rng, float difficulty, PerformanceProfile profile)
        {
            float nightBias = profile != null ? profile.nightOperationsWeakness : 0.5f;

            if (difficulty < 0.30f)
            {
                // Novice: mostly clear daylight
                return (rng.NextDouble() < 0.85) ? TimeOfDay.Day : TimeOfDay.Dusk;
            }

            if (difficulty > 0.60f || (nightBias > 0.65f && rng.NextDouble() < 0.60))
            {
                // Advanced: high probability of night operations
                double roll = rng.NextDouble();
                if (roll < 0.55) return TimeOfDay.Night;
                if (roll < 0.80) return TimeOfDay.Dusk;
                return TimeOfDay.Dawn;
            }

            // Intermediate
            TimeOfDay[] options = new[] { TimeOfDay.Day, TimeOfDay.Dawn, TimeOfDay.Dusk, TimeOfDay.Night };
            return options[rng.Next(options.Length)];
        }

        private static WeatherCondition SelectWeather(System.Random rng, float difficulty, PerformanceProfile profile)
        {
            float sensorWeakness = profile != null ? profile.degradedSensorWeakness : 0.5f;

            if (difficulty < 0.35f && sensorWeakness < 0.6f)
            {
                return WeatherCondition.Clear;
            }

            if (difficulty > 0.65f || sensorWeakness > 0.65f)
            {
                double roll = rng.NextDouble();
                if (roll < 0.40) return WeatherCondition.DenseFog;
                if (roll < 0.70) return WeatherCondition.HeavyRain;
                return WeatherCondition.Overcast;
            }

            WeatherCondition[] general = new[] { WeatherCondition.Clear, WeatherCondition.Overcast, WeatherCondition.LightRain };
            return general[rng.Next(general.Length)];
        }

        #endregion

        #region Sensor Profile Synthesis

        private static SensorProfile GenerateSensorProfile(System.Random rng, float difficulty, 
                                                            WeatherCondition weather, TimeOfDay timeOfDay, 
                                                            PerformanceProfile profile)
        {
            float sensorWeakness = profile != null ? profile.degradedSensorWeakness : 0.5f;

            // Radar noise and clutter scale with difficulty and weakness bias
            float baseRadarNoise = Mathf.Lerp(0.02f, 0.25f, difficulty) * Mathf.Lerp(0.8f, 1.4f, sensorWeakness);
            float baseRadarClutter = Mathf.Lerp(0.05f, 0.45f, difficulty);

            // RF Jamming scales aggressively at higher difficulty
            float rfJamming = 0f;
            if (difficulty > 0.45f)
            {
                float jamFactor = (difficulty - 0.45f) / 0.55f;
                rfJamming = Mathf.Lerp(0.05f, 0.55f, jamFactor) * (float)(0.75 + rng.NextDouble() * 0.5);
            }

            // Optical visibility based on weather & time of day
            float opticalVis = 1.0f;
            if (weather == WeatherCondition.DenseFog) opticalVis = (float)(0.25 + rng.NextDouble() * 0.20);
            else if (weather == WeatherCondition.HeavyRain) opticalVis = (float)(0.45 + rng.NextDouble() * 0.25);
            else if (weather == WeatherCondition.LightRain) opticalVis = 0.75f;

            if (timeOfDay == TimeOfDay.Night) opticalVis *= 0.30f;
            else if (timeOfDay == TimeOfDay.Dawn || timeOfDay == TimeOfDay.Dusk) opticalVis *= 0.70f;

            // Thermal contrast
            float thermal = (timeOfDay == TimeOfDay.Night) 
                ? (float)(0.85 + rng.NextDouble() * 0.12) // night cold air increases thermal signature of hot drone motors
                : (float)(0.60 + rng.NextDouble() * 0.25);

            float acoustic = Mathf.Lerp(0.05f, 0.35f, difficulty);

            return new SensorProfile(
                radarNoise: (float)(baseRadarNoise + rng.NextDouble() * 0.04),
                radarClutter: (float)(baseRadarClutter + rng.NextDouble() * 0.05),
                rfJammingLevel: rfJamming,
                opticalVisibility: opticalVis,
                thermalContrast: thermal,
                acousticAmbientNoise: acoustic
            );
        }

        #endregion

        #region Threat Wave Spawning & Vectors

        private static int ComputeWaveCount(System.Random rng, float difficulty, PerformanceProfile profile)
        {
            float swarmWeakness = profile != null ? profile.swarmSaturationWeakness : 0.5f;

            int minWaves = Mathf.RoundToInt(Mathf.Lerp(1, 4, difficulty));
            int maxWaves = Mathf.RoundToInt(Mathf.Lerp(2, 7, difficulty * (0.8f + swarmWeakness * 0.4f)));

            return rng.Next(minWaves, maxWaves + 1);
        }

        private static float ComputeDecoyRatio(System.Random rng, float difficulty, PerformanceProfile profile)
        {
            float decoyWeakness = profile != null ? profile.decoyDiscriminationWeakness : 0.5f;

            // Low difficulty = 0% to 15% decoys; high difficulty = 30% to 50% decoys
            float baseRatio = Mathf.Lerp(0.05f, 0.42f, difficulty);
            float weaknessBoost = decoyWeakness * 0.15f;
            float jitter = (float)((rng.NextDouble() - 0.5) * 0.10);

            return Mathf.Clamp(baseRatio + weaknessBoost + jitter, 0.0f, 0.50f);
        }

        private static int ComputeApproachVectorCount(float difficulty, PerformanceProfile profile)
        {
            float swarmWeakness = profile != null ? profile.swarmSaturationWeakness : 0.5f;

            if (difficulty < 0.30f) return 1; // Single directional approach axis
            if (difficulty < 0.65f && swarmWeakness < 0.6f) return 2; // Dual flank
            return 3; // Radial omnidirectional converge
        }

        private static List<ThreatSpawn> GenerateThreatWaves(System.Random rng, int waveCount, float decoyRatio, 
                                                            int approachVectorCount, float difficulty, 
                                                            PerformanceProfile profile)
        {
            var spawns = new List<ThreatSpawn>();
            float currentTime = 3.0f;

            // Generate base approach angles (in radians)
            float baseAngle = (float)(rng.NextDouble() * Math.PI * 2);
            float[] approachAngles = new float[approachVectorCount];
            for (int a = 0; a < approachVectorCount; a++)
            {
                approachAngles[a] = baseAngle + (float)(a * (Math.PI * 2 / approachVectorCount)) + (float)((rng.NextDouble() - 0.5) * 0.4);
            }

            for (int w = 0; w < waveCount; w++)
            {
                bool isDecoy = (rng.NextDouble() < decoyRatio);
                float angle = approachAngles[w % approachVectorCount];

                float spawnRange = (float)(650.0 + rng.NextDouble() * 550.0);
                float spawnAltitude = (float)(35.0 + rng.NextDouble() * 75.0);

                Vector3Data spawnPos = new Vector3Data(
                    x: Mathf.Cos(angle) * spawnRange,
                    y: spawnAltitude,
                    z: Mathf.Sin(angle) * spawnRange
                );

                ThreatType type;
                bool isHostile;
                FlightPattern pattern;
                int count = 1;
                float speed;
                string payload;

                if (isDecoy)
                {
                    isHostile = false;
                    bool isBird = rng.NextDouble() < 0.70;
                    if (isBird)
                    {
                        type = ThreatType.BirdDecoy;
                        count = rng.Next(2, 6); // flock
                        pattern = FlightPattern.PerimeterLoiter;
                        speed = (float)(7.0 + rng.NextDouble() * 4.0);
                        payload = "Biological / Bird flock";
                    }
                    else
                    {
                        type = ThreatType.CivilianUltralight;
                        count = 1;
                        pattern = FlightPattern.DirectInbound;
                        speed = (float)(16.0 + rng.NextDouble() * 6.0);
                        payload = "Civilian transponder active";
                    }
                }
                else
                {
                    isHostile = true;
                    // Hostile selection scales with difficulty
                    if (difficulty > 0.65f && rng.NextDouble() < 0.45)
                    {
                        // FPV kamikaze or Swarm strike
                        if (rng.NextDouble() < 0.5)
                        {
                            type = ThreatType.FPVKamikaze;
                            count = rng.Next(1, 3);
                            pattern = (rng.NextDouble() < 0.5) ? FlightPattern.NapOfEarthLowAltitude : FlightPattern.ZigZagEvasive;
                            speed = (float)(26.0 + rng.NextDouble() * 10.0);
                            payload = "High-explosive shaped impact charge";
                        }
                        else
                        {
                            type = ThreatType.TacticalQuadcopter;
                            count = Mathf.Clamp(rng.Next(4, Mathf.RoundToInt(6 + difficulty * 10)), 4, 16);
                            pattern = FlightPattern.SwarmCohesion;
                            speed = (float)(15.0 + rng.NextDouble() * 4.0);
                            payload = "Coordinated swarm mesh";
                        }
                    }
                    else if (difficulty > 0.40f && rng.NextDouble() < 0.40)
                    {
                        type = ThreatType.FixedWingRecon;
                        count = 1;
                        pattern = FlightPattern.WaypointRoute;
                        speed = (float)(20.0 + rng.NextDouble() * 6.0);
                        payload = "Long-range optical & SIGINT sensor";
                    }
                    else
                    {
                        type = ThreatType.CommercialQuadcopter;
                        count = 1;
                        pattern = (rng.NextDouble() < 0.5) ? FlightPattern.DirectInbound : FlightPattern.HoverSurveillance;
                        speed = (float)(12.0 + rng.NextDouble() * 5.0);
                        payload = "Suspicious underslung canister";
                    }
                }

                // Intermediate waypoints guiding inward toward asset origin (0, 0, 0)
                var waypoints = new List<Vector3Data>
                {
                    new Vector3Data(spawnPos.x * 0.6f, spawnPos.y * 0.85f, spawnPos.z * 0.6f),
                    new Vector3Data(spawnPos.x * 0.2f, spawnPos.y * 0.70f, spawnPos.z * 0.2f),
                    new Vector3Data(0f, 20f, 0f)
                };

                var wave = new ThreatSpawn(
                    threatId: $"WAVE-{(w + 1):D2}-{(isHostile ? "HOSTILE" : "DECOY")}",
                    type: type,
                    count: count,
                    spawnPosition: spawnPos,
                    flightPattern: pattern,
                    speed: speed,
                    isHostile: isHostile,
                    spawnTime: currentTime,
                    swarmSpreadRadius: count > 1 ? (float)(25.0 + count * 3.0) : 0f,
                    payloadDescription: payload
                )
                {
                    waypoints = waypoints
                };

                spawns.Add(wave);

                // Advance spawn time for sequential wave intervals
                float interval = (difficulty > 0.60f) 
                    ? (float)(8.0 + rng.NextDouble() * 12.0) // Compressed time between waves under high stress
                    : (float)(18.0 + rng.NextDouble() * 20.0);

                currentTime += interval;
            }

            return spawns;
        }

        private static int CountHostiles(List<ThreatSpawn> spawns)
        {
            int c = 0;
            for (int i = 0; i < spawns.Count; i++) if (spawns[i].isHostile) c += spawns[i].count;
            return c;
        }

        private static int CountDecoys(List<ThreatSpawn> spawns)
        {
            int c = 0;
            for (int i = 0; i < spawns.Count; i++) if (!spawns[i].isHostile) c += spawns[i].count;
            return c;
        }

        #endregion
    }
}
