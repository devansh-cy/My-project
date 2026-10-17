using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneTrainer.Logging
{
    /// <summary>
    /// Individual serialized entry within a session telemetry stream.
    /// </summary>
    [Serializable]
    public class SessionLogEntry
    {
        public float timestamp;
        public string eventType;
        public string targetId;
        public string summary;
        public string payloadJson;

        public SessionLogEntry() { }

        public SessionLogEntry(float timestamp, string eventType, string targetId, string summary, string payloadJson = "")
        {
            this.timestamp = timestamp;
            this.eventType = eventType;
            this.targetId = targetId;
            this.summary = summary;
            this.payloadJson = payloadJson;
        }

        public override string ToString()
        {
            return $"[{timestamp:F2}s] [{eventType}] {summary}";
        }
    }

    /// <summary>
    /// Complete simulation session telemetry log capturing ground-truth and trainee interactions.
    /// Serializable to/from JSON for After-Action Reviews (AAR), replay analysis, and scoring.
    /// </summary>
    [Serializable]
    public class SessionLog
    {
        public string sessionId = "SESSION-001";
        public string scenarioId = "scenario_default";
        public string traineeId = "OPERATOR-TRAINEE";
        public string startTimeIso;
        public string endTimeIso;
        public float totalDurationSeconds;

        public List<SessionLogEntry> entries = new List<SessionLogEntry>();

        public SessionLog()
        {
            startTimeIso = DateTime.UtcNow.ToString("o");
            entries = new List<SessionLogEntry>();
        }

        public SessionLog(string sessionId, string scenarioId, string traineeId)
        {
            this.sessionId = sessionId;
            this.scenarioId = scenarioId;
            this.traineeId = traineeId;
            this.startTimeIso = DateTime.UtcNow.ToString("o");
            this.entries = new List<SessionLogEntry>();
        }

        public void AddEntry(float timestamp, string eventType, string targetId, string summary, string payloadJson = "")
        {
            entries.Add(new SessionLogEntry(timestamp, eventType, targetId, summary, payloadJson));
        }

        public void FinalizeSession(float duration)
        {
            totalDurationSeconds = duration;
            endTimeIso = DateTime.UtcNow.ToString("o");
        }

        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static SessionLog FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<SessionLog>(json);
        }
    }
}
