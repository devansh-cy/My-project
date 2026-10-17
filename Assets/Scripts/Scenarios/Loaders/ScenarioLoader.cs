using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace DroneTrainer.Scenarios
{
    /// <summary>
    /// Serialization engine options for parsing scenario JSON files.
    /// </summary>
    public enum JsonBackend
    {
        Auto,
        JsonUtility,
        Newtonsoft
    }

    /// <summary>
    /// Handles discovery, reading, parsing, and caching of data-driven scenario JSON files
    /// from Unity's StreamingAssets directory.
    /// Supports both Unity's native JsonUtility and Newtonsoft.Json.
    /// </summary>
    public class ScenarioLoader
    {
        private const string ScenariosFolderName = "Scenarios";

        /// <summary>
        /// Base directory path for scenarios inside StreamingAssets.
        /// </summary>
        public static string ScenariosDirectoryPath => 
            Path.Combine(Application.streamingAssetsPath, ScenariosFolderName);

        /// <summary>
        /// Loads and parses a scenario definition file by relative filename or full path.
        /// </summary>
        /// <param name="fileNameOrPath">Relative filename (e.g. 'single_quadcopter_day.json') or full path.</param>
        /// <param name="backend">Preferred JSON serialization engine.</param>
        /// <returns>Deserialized ScenarioDefinition instance, or null if loading failed.</returns>
        public static ScenarioDefinition LoadScenarioFromFile(string fileNameOrPath, JsonBackend backend = JsonBackend.Auto)
        {
            string fullPath = ResolveScenarioPath(fileNameOrPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[ScenarioLoader] Scenario file not found at path: {fullPath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                ScenarioDefinition scenario = ParseScenarioJson(json, backend);

                if (scenario != null)
                {
                    scenario.Validate();
                    Debug.Log($"[ScenarioLoader] Successfully loaded scenario '{scenario.name}' ({scenario.id}) with {scenario.threatSpawns.Count} spawn waves from {Path.GetFileName(fullPath)}");
                }

                return scenario;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScenarioLoader] Failed to read or parse scenario file '{fullPath}'. Exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses a raw JSON string into a ScenarioDefinition using the specified backend.
        /// </summary>
        public static ScenarioDefinition ParseScenarioJson(string json, JsonBackend backend = JsonBackend.Auto)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError("[ScenarioLoader] Cannot parse null or empty JSON payload.");
                return null;
            }

            ScenarioDefinition result = null;

            if (backend == JsonBackend.Newtonsoft || backend == JsonBackend.Auto)
            {
                result = TryParseWithNewtonsoft(json);
            }

            // Fallback or explicit JsonUtility
            if (result == null && (backend == JsonBackend.JsonUtility || backend == JsonBackend.Auto))
            {
                result = TryParseWithJsonUtility(json);
            }

            if (result == null)
            {
                Debug.LogError("[ScenarioLoader] All JSON parsing backends failed to deserialize ScenarioDefinition.");
            }
            else
            {
                result.Validate();
            }

            return result;
        }

        /// <summary>
        /// Scans the StreamingAssets/Scenarios directory and returns all valid scenario definitions.
        /// </summary>
        public static List<ScenarioDefinition> LoadAllScenarios(JsonBackend backend = JsonBackend.Auto)
        {
            var scenarios = new List<ScenarioDefinition>();
            string directory = ScenariosDirectoryPath;

            if (!Directory.Exists(directory))
            {
                Debug.LogWarning($"[ScenarioLoader] Scenarios folder does not exist at '{directory}'. Creating empty directory.");
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ScenarioLoader] Could not create directory at '{directory}': {ex.Message}");
                }
                return scenarios;
            }

            string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);

            foreach (string filePath in files)
            {
                ScenarioDefinition scenario = LoadScenarioFromFile(filePath, backend);
                if (scenario != null)
                {
                    scenarios.Add(scenario);
                }
            }

            Debug.Log($"[ScenarioLoader] Discovered and loaded {scenarios.Count} scenarios from {directory}");
            return scenarios;
        }

        /// <summary>
        /// Serializes and writes a ScenarioDefinition to disk inside StreamingAssets/Scenarios.
        /// </summary>
        public static bool SaveScenarioToFile(ScenarioDefinition scenario, string fileName, bool prettyPrint = true)
        {
            if (scenario == null)
            {
                Debug.LogError("[ScenarioLoader] Cannot save null scenario.");
                return false;
            }

            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".json";
            }

            string targetDir = ScenariosDirectoryPath;
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            string targetPath = Path.Combine(targetDir, fileName);

            try
            {
                string json = SerializeToJson(scenario, prettyPrint);
                File.WriteAllText(targetPath, json);
                Debug.Log($"[ScenarioLoader] Scenario saved successfully to {targetPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScenarioLoader] Failed to save scenario to '{targetPath}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Serializes a scenario definition to JSON string.
        /// </summary>
        public static string SerializeToJson(ScenarioDefinition scenario, bool prettyPrint = true)
        {
            if (scenario == null) return string.Empty;

            // Attempt Newtonsoft serialization first if available
            Type newtonsoftType = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json");
            if (newtonsoftType != null)
            {
                try
                {
                    var settingsType = Type.GetType("Newtonsoft.Json.JsonSerializerSettings, Newtonsoft.Json");
                    var formattingType = Type.GetType("Newtonsoft.Json.Formatting, Newtonsoft.Json");
                    object formatting = Enum.Parse(formattingType, prettyPrint ? "Indented" : "None");

                    var serializeMethod = newtonsoftType.GetMethod("SerializeObject", new[] { typeof(object), formattingType });
                    if (serializeMethod != null)
                    {
                        return (string)serializeMethod.Invoke(null, new object[] { scenario, formatting });
                    }
                }
                catch
                {
                    // Fall back to JsonUtility
                }
            }

            return JsonUtility.ToJson(scenario, prettyPrint);
        }

        private static ScenarioDefinition TryParseWithNewtonsoft(string json)
        {
            try
            {
                Type newtonsoftType = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json");
                if (newtonsoftType != null)
                {
                    var deserializeMethod = newtonsoftType.GetMethod("DeserializeObject", 1, new[] { typeof(string) });
                    if (deserializeMethod != null)
                    {
                        var genericMethod = deserializeMethod.MakeGenericMethod(typeof(ScenarioDefinition));
                        return (ScenarioDefinition)genericMethod.Invoke(null, new object[] { json });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ScenarioLoader] Newtonsoft.Json parsing attempt encountered an issue: {ex.Message}. Falling back to JsonUtility.");
            }

            return null;
        }

        private static ScenarioDefinition TryParseWithJsonUtility(string json)
        {
            try
            {
                // Preprocess string enum names to their integer equivalents so JsonUtility deserializes cleanly
                string normalizedJson = NormalizeEnumsForJsonUtility(json);
                return JsonUtility.FromJson<ScenarioDefinition>(normalizedJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScenarioLoader] JsonUtility parsing failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts human-readable string enum values into integers to ensure Unity's JsonUtility
        /// accurately parses them across all Unity versions.
        /// </summary>
        private static string NormalizeEnumsForJsonUtility(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            // Replace TerrainType strings
            foreach (TerrainType val in Enum.GetValues(typeof(TerrainType)))
            {
                json = System.Text.RegularExpressions.Regex.Replace(
                    json, 
                    $"\"terrain\"\\s*:\\s*\"{val}\"", 
                    $"\"terrain\": {(int)val}", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace TimeOfDay strings
            foreach (TimeOfDay val in Enum.GetValues(typeof(TimeOfDay)))
            {
                json = System.Text.RegularExpressions.Regex.Replace(
                    json, 
                    $"\"timeOfDay\"\\s*:\\s*\"{val}\"", 
                    $"\"timeOfDay\": {(int)val}", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace WeatherCondition strings
            foreach (WeatherCondition val in Enum.GetValues(typeof(WeatherCondition)))
            {
                json = System.Text.RegularExpressions.Regex.Replace(
                    json, 
                    $"\"weather\"\\s*:\\s*\"{val}\"", 
                    $"\"weather\": {(int)val}", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace ThreatType strings
            foreach (ThreatType val in Enum.GetValues(typeof(ThreatType)))
            {
                json = System.Text.RegularExpressions.Regex.Replace(
                    json, 
                    $"\"type\"\\s*:\\s*\"{val}\"", 
                    $"\"type\": {(int)val}", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace FlightPattern strings
            foreach (FlightPattern val in Enum.GetValues(typeof(FlightPattern)))
            {
                json = System.Text.RegularExpressions.Regex.Replace(
                    json, 
                    $"\"flightPattern\"\\s*:\\s*\"{val}\"", 
                    $"\"flightPattern\": {(int)val}", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return json;
        }

        private static string ResolveScenarioPath(string fileNameOrPath)
        {
            if (Path.IsPathRooted(fileNameOrPath))
            {
                return fileNameOrPath;
            }

            if (!fileNameOrPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileNameOrPath += ".json";
            }

            return Path.Combine(ScenariosDirectoryPath, fileNameOrPath);
        }
    }
}
