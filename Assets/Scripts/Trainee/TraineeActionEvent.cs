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
        public float timestamp;
        public string contactId;
        public TraineeActionType actionType;
        public TraineeClassification classification;
        public ThreatLevel threatLevel;
        public ResponseOption response;
        public string details;

        public float Timestamp => timestamp;
        public string ContactId => contactId;
        public TraineeActionType ActionType => actionType;
        public TraineeClassification Classification => classification;
        public ThreatLevel ThreatLevel => threatLevel;
        public ResponseOption Response => response;
        public string Details => details;

        public TraineeActionEvent(float timestamp, string contactId, TraineeActionType actionType, 
                                  TraineeClassification classification = TraineeClassification.Unknown, 
                                  ThreatLevel threatLevel = ThreatLevel.None, 
                                  ResponseOption response = ResponseOption.Monitor, 
                                  string details = "")
        {
            this.timestamp = timestamp;
            this.contactId = contactId;
            this.actionType = actionType;
            this.classification = classification;
            this.threatLevel = threatLevel;
            this.response = response;
            this.details = details;
        }

        public override string ToString()
        {
            return $"[TraineeAction] T={Timestamp:F2}s | Contact: {ContactId} | Action: {ActionType} | Class: {Classification} | Threat: {ThreatLevel} | Response: {Response} ({Details})";
        }
    }
}
