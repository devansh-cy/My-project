using System;

namespace DroneTrainer.Trainee
{
    /// <summary>
    /// Event broadcast across EventBus whenever an operator interacts with a contact,
    /// modifies classification, adjusts threat levels, or authorizes a tactical countermeasure.
    /// Used by EventLogger and the Scoring Engine to compute decision-tree metrics and reaction latency.
    /// </summary>
    [Serializable]
    public struct TraineeActionEvent
    {
        public float Timestamp { get; }
        public string ContactId { get; }
        public TraineeActionType ActionType { get; }
        public TraineeClassification Classification { get; }
        public ThreatLevel ThreatLevel { get; }
        public ResponseOption Response { get; }
        public string Details { get; }

        public TraineeActionEvent(float timestamp, string contactId, TraineeActionType actionType, 
                                  TraineeClassification classification = TraineeClassification.Unknown, 
                                  ThreatLevel threatLevel = ThreatLevel.None, 
                                  ResponseOption response = ResponseOption.Monitor, 
                                  string details = "")
        {
            Timestamp = timestamp;
            ContactId = contactId;
            ActionType = actionType;
            Classification = classification;
            ThreatLevel = threatLevel;
            Response = response;
            Details = details;
        }

        public override string ToString()
        {
            return $"[TraineeAction] T={Timestamp:F2}s | Contact: {ContactId} | Action: {ActionType} | Class: {Classification} | Threat: {ThreatLevel} | Response: {Response} ({Details})";
        }
    }
}
