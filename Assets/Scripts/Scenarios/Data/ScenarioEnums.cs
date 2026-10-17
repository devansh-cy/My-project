using System;

namespace DroneTrainer.Scenarios
{
    /// <summary>
    /// Operating terrain environment classifications.
    /// </summary>
    public enum TerrainType
    {
        Urban,
        Rural,
        Industrial,
        Coastal,
        Mountainous,
        SuburbanMilitaryBase
    }

    /// <summary>
    /// Diurnal lighting and illumination cycle.
    /// </summary>
    public enum TimeOfDay
    {
        Day,
        Dawn,
        Dusk,
        Night
    }

    /// <summary>
    /// Atmospheric and meteorological conditions affecting visual and RF propagation.
    /// </summary>
    public enum WeatherCondition
    {
        Clear,
        Overcast,
        LightRain,
        HeavyRain,
        DenseFog,
        DustStorm
    }

    /// <summary>
    /// Threat and non-threat airborne target classifications.
    /// </summary>
    public enum ThreatType
    {
        CommercialQuadcopter,
        TacticalQuadcopter,
        FixedWingRecon,
        FPVKamikaze,
        HeavyPayloadOctocopter,
        BirdDecoy,
        CivilianUltralight
    }

    /// <summary>
    /// Trajectory and tactical flight pattern modes.
    /// </summary>
    public enum FlightPattern
    {
        DirectInbound,
        PerimeterLoiter,
        HoverSurveillance,
        NapOfEarthLowAltitude,
        ZigZagEvasive,
        WaypointRoute,
        SwarmCohesion
    }
}
