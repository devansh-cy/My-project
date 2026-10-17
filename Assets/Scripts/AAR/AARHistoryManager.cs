using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Logging;
using DroneTrainer.Scoring;

namespace DroneTrainer.AAR
{
    /// <summary>
    /// Manages persistent local history storage across sessions, maintains trainee/unit
    /// cumulative profiles, computes trend analytics over the last N sessions, and produces CSV exports.
    /// </summary>
    public class AARHistoryManager
    {
        private const string DatabaseFileName = "aar_history_database.json";
        private const string SubfolderName = "AAR";

        private AARHistoryDatabase database;
        private readonly string databaseFilePath;

        public AARHistoryDatabase Database => database;

        public AARHistoryManager(string customDirectory = null)
        {
            string baseDir = string.IsNullOrEmpty(customDirectory)
                ? Path.Combine(Application.persistentDataPath, SubfolderName)
                : customDirectory;

            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            databaseFilePath = Path.Combine(baseDir, DatabaseFileName);
            LoadDatabase();
        }

        #region Database Load & Save

        public void LoadDatabase()
        {
            if (File.Exists(databaseFilePath))
            {
                try
                {
                    string json = File.ReadAllText(databaseFilePath);
                    database = AARHistoryDatabase.FromJson(json);
                    Debug.Log($"[AARHistoryManager] Loaded history database with {database.sessions.Count} sessions and {database.profiles.Count} trainee profiles.");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AARHistoryManager] Failed to read database at '{databaseFilePath}': {ex.Message}");
                }
            }

            database = new AARHistoryDatabase();
        }

        public bool SaveDatabase()
        {
            if (database == null) return false;

            try
            {
                string json = database.ToJson(true);
                File.WriteAllText(databaseFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AARHistoryManager] Failed to save database to '{databaseFilePath}': {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Session Recording & Profile Management

        /// <summary>
        /// Registers a completed session into historical archives and updates the trainee's profile.
        /// </summary>
        public HistoricalSessionSummary RecordSession(SessionScoreReport report, float durationSeconds, string unitId = "1st C-UAS Battery")
        {
            if (report == null) return null;

            var summary = new HistoricalSessionSummary(
                sessionId: report.sessionId,
                scenarioId: report.scenarioId,
                traineeId: report.traineeId,
                unitId: unitId,
                durationSeconds: durationSeconds,
                overallScore: report.overallScorePercentage,
                detectionScore: report.averageDetectionScore,
                classificationScore: report.averageClassificationScore,
                engagementScore: report.averageEngagementScore,
                totalContacts: report.totalContactsEvaluated,
                correct: report.correctClassifications,
                partial: report.partialClassifications,
                wrong: report.wrongClassifications,
                roeViolations: report.roeViolations
            );

            database.sessions.Add(summary);

            // Update or create trainee profile
            TraineeProfile profile = GetOrCreateProfile(report.traineeId, unitId);
            profile.UpdateWithSession(summary);

            SaveDatabase();
            Debug.Log($"[AARHistoryManager] Logged session '{report.sessionId}'. Trainee '{report.traineeId}' now has {profile.totalSessionsCompleted} total sessions.");
            return summary;
        }

        public TraineeProfile GetOrCreateProfile(string traineeId, string unitId = "1st C-UAS Battery")
        {
            for (int i = 0; i < database.profiles.Count; i++)
            {
                if (database.profiles[i].traineeId.Equals(traineeId, StringComparison.OrdinalIgnoreCase))
                {
                    return database.profiles[i];
                }
            }

            var newProfile = new TraineeProfile(traineeId, unitId);
            database.profiles.Add(newProfile);
            return newProfile;
        }

        public List<HistoricalSessionSummary> GetSessionsForTrainee(string traineeId)
        {
            var list = new List<HistoricalSessionSummary>();
            for (int i = 0; i < database.sessions.Count; i++)
            {
                if (database.sessions[i].traineeId.Equals(traineeId, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(database.sessions[i]);
                }
            }
            return list;
        }

        #endregion

        #region Trend Calculation

        /// <summary>
        /// Computes trend statistics across the last N sessions (overall or filtered by trainee).
        /// </summary>
        public MetricTrendSummary ComputeTrends(int lastNSessions = 10, string filterTraineeId = null)
        {
            List<HistoricalSessionSummary> pool = string.IsNullOrEmpty(filterTraineeId)
                ? database.sessions
                : GetSessionsForTrainee(filterTraineeId);

            var result = new MetricTrendSummary();
            if (pool == null || pool.Count == 0) return result;

            int count = Mathf.Min(lastNSessions, pool.Count);
            int startIndex = pool.Count - count;

            float sumOverall = 0f;
            float sumDetect = 0f;
            float sumClass = 0f;
            float sumEngage = 0f;
            int totalContacts = 0;
            int totalCorrect = 0;
            int roeViolations = 0;

            for (int i = startIndex; i < pool.Count; i++)
            {
                var s = pool[i];
                sumOverall += s.overallScore;
                sumDetect += s.detectionScore;
                sumClass += s.classificationScore;
                sumEngage += s.engagementScore;

                totalContacts += s.totalContacts;
                totalCorrect += s.correctClassifications;
                roeViolations += s.roeViolations;
            }

            result.sessionCount = count;
            result.avgOverallScore = sumOverall / count;
            result.avgDetectionScore = sumDetect / count;
            result.avgClassificationScore = sumClass / count;
            result.avgEngagementScore = sumEngage / count;
            result.classificationAccuracyPct = totalContacts > 0 ? ((float)totalCorrect / totalContacts) * 100f : 0f;
            result.totalRoeViolations = roeViolations;

            // Trend Delta: Last session score minus oldest session score in this window
            float oldestScore = pool[startIndex].overallScore;
            float latestScore = pool[pool.Count - 1].overallScore;
            result.scoreDeltaFirstToLast = latestScore - oldestScore;

            return result;
        }

        #endregion

        #region CSV Export

        /// <summary>
        /// Exports a detailed per-contact breakdown report for a session to CSV.
        /// </summary>
        public string ExportSessionToCsv(SessionScoreReport report, string destinationPath = null)
        {
            if (report == null) return null;

            if (string.IsNullOrEmpty(destinationPath))
            {
                string folder = Path.Combine(Application.persistentDataPath, SubfolderName, "Exports");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                destinationPath = Path.Combine(folder, $"AAR_Report_{report.sessionId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            }

            var sb = new StringBuilder();

            // Header Metadata
            sb.AppendLine("# C-UAS SIMULATOR AFTER-ACTION REVIEW (AAR) REPORT");
            sb.AppendLine($"# Session ID,{EscapeCsv(report.sessionId)}");
            sb.AppendLine($"# Scenario ID,{EscapeCsv(report.scenarioId)}");
            sb.AppendLine($"# Trainee ID,{EscapeCsv(report.traineeId)}");
            sb.AppendLine($"# Overall Score %,{report.overallScorePercentage:F1}%");
            sb.AppendLine($"# Avg Detection Score,{report.averageDetectionScore:F1}");
            sb.AppendLine($"# Avg Classification Score,{report.averageClassificationScore:F1}");
            sb.AppendLine($"# Avg Engagement Score,{report.averageEngagementScore:F1}");
            sb.AppendLine($"# ROE Violations,{report.roeViolations}");
            sb.AppendLine();

            // Table Columns
            sb.AppendLine("Contact ID,Ground Truth Type,Hostile,First Appearance (s),First Action (s),Latency (s),Detection Score,Trainee Classification,Match Result,Classification Score,Trainee Decision,Engagement Score,Composite Score,ROE Notes");

            // Rows
            for (int i = 0; i < report.contactRecords.Count; i++)
            {
                var r = report.contactRecords[i];
                sb.AppendLine($"{EscapeCsv(r.contactOrDroneId)}," +
                              $"{EscapeCsv(r.groundTruthThreatType)}," +
                              $"{r.isHostile}," +
                              $"{r.firstAppearanceTime:F2}," +
                              $"{(r.firstTraineeActionTime >= 0 ? r.firstTraineeActionTime.ToString("F2") : "N/A")}," +
                              $"{r.detectionLatencySeconds:F2}," +
                              $"{r.detectionScore:F1}," +
                              $"{r.traineeClassification}," +
                              $"{r.classificationResult}," +
                              $"{r.classificationScore:F1}," +
                              $"{r.traineeResponse}," +
                              $"{r.engagementScore:F1}," +
                              $"{r.compositeScore:F1}," +
                              $"{EscapeCsv(r.roeNotes)}");
            }

            File.WriteAllText(destinationPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[AARHistoryManager] Exported session CSV to: {destinationPath}");
            return destinationPath;
        }

        /// <summary>
        /// Exports all historical session summaries to CSV.
        /// </summary>
        public string ExportHistoricalSummaryToCsv(string destinationPath = null)
        {
            if (string.IsNullOrEmpty(destinationPath))
            {
                string folder = Path.Combine(Application.persistentDataPath, SubfolderName, "Exports");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                destinationPath = Path.Combine(folder, $"AAR_History_Summary_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            }

            var sb = new StringBuilder();
            sb.AppendLine("Session ID,Scenario ID,Trainee ID,Unit ID,Date,Duration (s),Overall Score %,Detection Score,Classification Score,Engagement Score,Total Contacts,Correct,Partial,Wrong,ROE Violations");

            for (int i = 0; i < database.sessions.Count; i++)
            {
                var s = database.sessions[i];
                sb.AppendLine($"{EscapeCsv(s.sessionId)}," +
                              $"{EscapeCsv(s.scenarioId)}," +
                              $"{EscapeCsv(s.traineeId)}," +
                              $"{EscapeCsv(s.unitId)}," +
                              $"{EscapeCsv(s.timestampIso)}," +
                              $"{s.durationSeconds:F1}," +
                              $"{s.overallScore:F1}," +
                              $"{s.detectionScore:F1}," +
                              $"{s.classificationScore:F1}," +
                              $"{s.engagementScore:F1}," +
                              $"{s.totalContacts}," +
                              $"{s.correctClassifications}," +
                              $"{s.partialClassifications}," +
                              $"{s.wrongClassifications}," +
                              $"{s.roeViolations}");
            }

            File.WriteAllText(destinationPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[AARHistoryManager] Exported historical summary CSV to: {destinationPath}");
            return destinationPath;
        }

        private string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r"))
            {
                return $"\"{text.Replace("\"", "\"\"")}\"";
            }
            return text;
        }

        #endregion
    }
}
