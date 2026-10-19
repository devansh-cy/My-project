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
    /// NATO / US DoD UAS Grouping Classification.
    /// </summary>
    public enum UasMilSpecGroup
    {
        Unknown,
        Group1_Micro,    // < 20 lbs (Commercial Quads, FPV Kamikazes)
        Group2_Tactical, // 21 - 55 lbs (Tactical Quads, Heavy Octocopters)
        Group3_Medium,   // 56 - 1320 lbs (Fixed-Wing Recon, Loitering Munitions)
        BiologicalDecoy  // Birds / Non-UAS Wildlife
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
    /// In Urban areas: Jam and Net are collateral-safe; Kinetic has high collateral damage risk!
    /// </summary>
    public enum ResponseOption
    {
        Monitor,
        Alert,
        Jam,      // Electronic Warfare Jammer (Non-kinetic, zero collateral debris)
        Net,      // Physical Net Catcher (Non-explosive containment, safe in urban sectors)
        Kinetic,  // Kinetic Missile Strike (Lethal but severe collateral debris hazard over urban zone)
        Laser,    // Directed Energy Laser (Precision thermal beam, minimal collateral debris)
        Engage = Kinetic,

        // Semantic aliases
        ElectronicJammer = Jam,
        NetCatcher = Net,
        KineticInterceptor = Kinetic,
        DirectedEnergyLaser = Laser
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
