using System;
using System.IO;
using UnityEngine;
using DroneTrainer.Core.Events;
using DroneTrainer.Sensors;
using DroneTrainer.Trainee;

namespace DroneTrainer.Logging
{
    /// <summary>
    /// Central telemetry logger that records ground-truth simulation events (spawns,
    /// range crossings, impacts, neutralizations), sensor contact updates, and trainee actions.
    /// Manages serializable session streams and exports logs for AAR replay and scoring.
    /// </summary>
    public class EventLogger : MonoBehaviour
    {
        [Header("Session Metadata")]
        [SerializeField] private string sessionId = "SESSION-LIVE-01";
        [SerializeField] private string scenarioId = "scenario_01_single_quad_day";
        [SerializeField] private string traineeId = "OPERATOR-ALPHA";

        [Header("Persistence")]
        [SerializeField] private bool autoSaveOnDisable = true;
        [SerializeField] private string sessionLogSubfolder = "SessionLogs";

        private SessionLog activeSession;
        private float sessionStartTime = 0f;
        private bool isRecording = false;

        public SessionLog ActiveSession => activeSession;
        public bool IsRecording => isRecording;

        private void Awake()
        {
            StartNewSession(sessionId, scenarioId, traineeId);
        }

        private void OnEnable()
        {
            RegisterEventHandlers();
        }

        private void OnDisable()
        {
            UnregisterEventHandlers();
            if (autoSaveOnDisable && isRecording)
            {
                EndSessionAndSave();
            }
        }

        #region Session Lifecycle

        public void StartNewSession(string newSessionId, string newScenarioId, string newTraineeId)
        {
            sessionId = string.IsNullOrEmpty(newSessionId) ? $"SESSION-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}" : newSessionId;
            scenarioId = newScenarioId;
            traineeId = newTraineeId;
            sessionStartTime = Time.time;

            activeSession = new SessionLog(sessionId, scenarioId, traineeId);
            isRecording = true;

            Debug.Log($"[EventLogger] Started recording session '{sessionId}' for scenario '{scenarioId}'.");
        }

        public string EndSessionAndSave()
        {
            if (!isRecording || activeSession == null) return null;

            isRecording = false;
            float duration = Time.time - sessionStartTime;
            activeSession.FinalizeSession(duration);

            string folderPath = Path.Combine(Application.persistentDataPath, sessionLogSubfolder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, $"{activeSession.sessionId}.json");
            try
            {
                string json = activeSession.ToJson(true);
                File.WriteAllText(filePath, json);
                Debug.Log($"[EventLogger] Session log saved successfully to: {filePath} ({activeSession.entries.Count} entries)");
                return filePath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EventLogger] Failed to save session log to '{filePath}': {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Event Subscriptions

        private void RegisterEventHandlers()
        {
            EventBus.Subscribe<DroneSpawnedEvent>(OnDroneSpawned);
            EventBus.Subscribe<DroneEnteredRangeEvent>(OnDroneEnteredRange);
            EventBus.Subscribe<DroneImpactEvent>(OnDroneImpact);
            EventBus.Subscribe<DroneNeutralizedEvent>(OnDroneNeutralized);
            EventBus.Subscribe<ContactDetectedEvent>(OnContactDetected);
            EventBus.Subscribe<TraineeActionEvent>(OnTraineeAction);
        }

        private void UnregisterEventHandlers()
        {
            EventBus.Unsubscribe<DroneSpawnedEvent>(OnDroneSpawned);
            EventBus.Unsubscribe<DroneEnteredRangeEvent>(OnDroneEnteredRange);
            EventBus.Unsubscribe<DroneImpactEvent>(OnDroneImpact);
            EventBus.Unsubscribe<DroneNeutralizedEvent>(OnDroneNeutralized);
            EventBus.Unsubscribe<ContactDetectedEvent>(OnContactDetected);
            EventBus.Unsubscribe<TraineeActionEvent>(OnTraineeAction);
        }

        private void OnDroneSpawned(DroneSpawnedEvent evt)
        {
            if (!isRecording) return;
            string payload = JsonUtility.ToJson(evt);
            activeSession.AddEntry(evt.Timestamp, evt.EventType, evt.DroneId, evt.Summary, payload);
        }

        private void OnDroneEnteredRange(DroneEnteredRangeEvent evt)
        {
            if (!isRecording) return;
            string payload = JsonUtility.ToJson(evt);
            activeSession.AddEntry(evt.Timestamp, evt.EventType, evt.DroneId, evt.Summary, payload);
        }

        private void OnDroneImpact(DroneImpactEvent evt)
        {
            if (!isRecording) return;
            string payload = JsonUtility.ToJson(evt);
            activeSession.AddEntry(evt.Timestamp, evt.EventType, evt.DroneId, evt.Summary, payload);
        }

        private void OnDroneNeutralized(DroneNeutralizedEvent evt)
        {
            if (!isRecording) return;
            string payload = JsonUtility.ToJson(evt);
            activeSession.AddEntry(evt.Timestamp, evt.EventType, evt.DroneId, evt.Summary, payload);
        }

        private void OnContactDetected(ContactDetectedEvent evt)
        {
            if (!isRecording || evt.Contact == null) return;
            // Record contact telemetry
            string payload = JsonUtility.ToJson(evt.Contact);
            string summary = $"[CONTACT] ID={evt.Contact.id} Sensor={evt.Contact.sensorType} Conf={evt.Contact.confidence:P0} False={evt.Contact.isFalseAlarm}";
            activeSession.AddEntry(evt.SimulationTime, nameof(ContactDetectedEvent), evt.Contact.id, summary, payload);
        }

        private void OnTraineeAction(TraineeActionEvent evt)
        {
            if (!isRecording) return;
            string payload = JsonUtility.ToJson(evt);
            activeSession.AddEntry(evt.Timestamp, nameof(TraineeActionEvent), evt.ContactId, evt.ToString(), payload);
        }

        #endregion
    }
}
