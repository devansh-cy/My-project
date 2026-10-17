namespace DroneTrainer.Trainee
{
    /// <summary>
    /// Trainee classification options for an aerial contact.
    /// </summary>
    public enum TraineeClassification
    {
        Unknown,
        Bird,
        Quadcopter,
        FixedWing,
        LoiteringMunition,
        Swarm
    }

    /// <summary>
    /// Perceived threat severity level assigned by the operator.
    /// </summary>
    public enum ThreatLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Countermeasure and response tactical decisions available to the operator.
    /// </summary>
    public enum ResponseOption
    {
        Monitor,
        Alert,
        Jam,
        Engage
    }

    /// <summary>
    /// Discrete trainee action categories for telemetry tracking and scoring.
    /// </summary>
    public enum TraineeActionType
    {
        SelectContact,
        ClassifyTarget,
        SetThreatLevel,
        ExecuteResponse
    }
}
