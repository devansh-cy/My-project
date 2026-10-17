using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneTrainer.AAR
{
    /// <summary>
    /// Compact summary of a completed simulation session for historical tracking and trend analysis.
    /// </summary>
    [Serializable]
    public class HistoricalSessionSummary
    {
        public string sessionId;
        public string scenarioId;
        public string traineeId;
        public string unitId;
        public string timestampIso;
        public float durationSeconds;

        public float overallScore;
        public float detectionScore;
        public float classificationScore;
        public float engagementScore;

        public int totalContacts;
        public int correctClassifications;
        public int partialClassifications;
        public int wrongClassifications;
        public int roeViolations;

        public HistoricalSessionSummary() { }

        public HistoricalSessionSummary(string sessionId, string scenarioId, string traineeId, string unitId,
                                        float durationSeconds, float overallScore, float detectionScore, 
                                        float classificationScore, float engagementScore, int totalContacts,
                                        int correct, int partial, int wrong, int roeViolations)
        {
            this.sessionId = sessionId;
            this.scenarioId = scenarioId;
            this.traineeId = traineeId;
            this.unitId = unitId;
            this.timestampIso = DateTime.UtcNow.ToString("o");
            this.durationSeconds = durationSeconds;
            this.overallScore = overallScore;
            this.detectionScore = detectionScore;
            this.classificationScore = classificationScore;
            this.engagementScore = engagementScore;
            this.totalContacts = totalContacts;
            this.correctClassifications = correct;
            this.partialClassifications = partial;
            this.wrongClassifications = wrong;
            this.roeViolations = roeViolations;
        }
    }

    /// <summary>
    /// Cumulative performance profile for an individual trainee or operational military unit.
    /// </summary>
    [Serializable]
    public class TraineeProfile
    {
        public string traineeId;
        public string unitId = "1st C-UAS Battery";
        public int totalSessionsCompleted;
        public float averageOverallScore;
        public float averageDetectionScore;
        public float averageClassificationScore;
        public float averageEngagementScore;
        public float highestScore;
        public int totalRoeViolations;
        public string lastActiveDateIso;

        public TraineeProfile() { }

        public TraineeProfile(string traineeId, string unitId)
        {
            this.traineeId = traineeId;
            this.unitId = unitId;
            this.totalSessionsCompleted = 0;
            this.averageOverallScore = 0f;
            this.highestScore = 0f;
            this.lastActiveDateIso = DateTime.UtcNow.ToString("o");
        }

        public void UpdateWithSession(HistoricalSessionSummary session)
        {
            totalSessionsCompleted++;
            if (session.overallScore > highestScore)
            {
                highestScore = session.overallScore;
            }

            // Running incremental averages
            float n = totalSessionsCompleted;
            averageOverallScore = ((averageOverallScore * (n - 1)) + session.overallScore) / n;
            averageDetectionScore = ((averageDetectionScore * (n - 1)) + session.detectionScore) / n;
            averageClassificationScore = ((averageClassificationScore * (n - 1)) + session.classificationScore) / n;
            averageEngagementScore = ((averageEngagementScore * (n - 1)) + session.engagementScore) / n;

            totalRoeViolations += session.roeViolations;
            lastActiveDateIso = DateTime.UtcNow.ToString("o");
        }
    }

    /// <summary>
    /// Master persistent database holding historical sessions and trainee profiles.
    /// Serializes cleanly to local JSON.
    /// </summary>
    [Serializable]
    public class AARHistoryDatabase
    {
        public List<HistoricalSessionSummary> sessions = new List<HistoricalSessionSummary>();
        public List<TraineeProfile> profiles = new List<TraineeProfile>();

        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static AARHistoryDatabase FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new AARHistoryDatabase();
            return JsonUtility.FromJson<AARHistoryDatabase>(json) ?? new AARHistoryDatabase();
        }
    }

    /// <summary>
    /// Moving average trends calculated across the last N sessions.
    /// </summary>
    public struct MetricTrendSummary
    {
        public int sessionCount;
        public float avgOverallScore;
        public float avgDetectionScore;
        public float avgClassificationScore;
        public float avgEngagementScore;
        public float scoreDeltaFirstToLast; // Positive if improving, negative if deteriorating
        public float classificationAccuracyPct;
        public int totalRoeViolations;
    }
}
