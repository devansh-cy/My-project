using System;
using UnityEngine;
using DroneTrainer.Scenarios;

namespace DroneTrainer.Logging
{
    /// <summary>
    /// Base interface for all simulation events recorded in the telemetry session log.
    /// </summary>
    public interface ISimulationEvent
    {
        float Timestamp { get; }
        string EventType { get; }
        string Summary { get; }
    }

    /// <summary>
    /// Ground-truth event triggered when a threat drone or swarm unit spawns into the simulation world.
    /// </summary>
    [Serializable]
    public struct DroneSpawnedEvent : ISimulationEvent
    {
        public float Timestamp { get; }
        public string DroneId { get; }
        public ThreatType ThreatType { get; }
        public Vector3 SpawnPosition { get; }
        public bool IsHostile { get; }

        public string EventType => nameof(DroneSpawnedEvent);
        public string Summary => $"[SPAWN] Drone {DroneId} ({ThreatType}, Hostile={IsHostile}) at {SpawnPosition}";

        public DroneSpawnedEvent(float timestamp, string droneId, ThreatType threatType, Vector3 spawnPosition, bool isHostile)
        {
            Timestamp = timestamp;
            DroneId = droneId;
            ThreatType = threatType;
            SpawnPosition = spawnPosition;
            IsHostile = isHostile;
        }
    }

    /// <summary>
    /// Ground-truth event triggered when a target enters a sensor's active coverage or monitored perimeter fence.
    /// </summary>
    [Serializable]
    public struct DroneEnteredRangeEvent : ISimulationEvent
    {
        public float Timestamp { get; }
        public string DroneId { get; }
        public float RangeMeters { get; }
        public string SensorOrZoneId { get; }

        public string EventType => nameof(DroneEnteredRangeEvent);
        public string Summary => $"[ENTER_RANGE] Drone {DroneId} entered {SensorOrZoneId} at {RangeMeters:F1}m";

        public DroneEnteredRangeEvent(float timestamp, string droneId, float rangeMeters, string sensorOrZoneId)
        {
            Timestamp = timestamp;
            DroneId = droneId;
            RangeMeters = rangeMeters;
            SensorOrZoneId = sensorOrZoneId;
        }
    }

    /// <summary>
    /// Ground-truth event triggered when an incoming hostile threat impacts a protected asset or perimeter.
    /// </summary>
    [Serializable]
    public struct DroneImpactEvent : ISimulationEvent
    {
        public float Timestamp { get; }
        public string DroneId { get; }
        public Vector3 ImpactPosition { get; }
        public bool AssetDamaged { get; }

        public string EventType => nameof(DroneImpactEvent);
        public string Summary => $"[IMPACT] Drone {DroneId} impacted at {ImpactPosition}! Damage={AssetDamaged}";

        public DroneImpactEvent(float timestamp, string droneId, Vector3 impactPosition, bool assetDamaged)
        {
            Timestamp = timestamp;
            DroneId = droneId;
            ImpactPosition = impactPosition;
            AssetDamaged = assetDamaged;
        }
    }

    /// <summary>
    /// Ground-truth event triggered when a drone is successfully neutralized by an operator countermeasure.
    /// </summary>
    [Serializable]
    public struct DroneNeutralizedEvent : ISimulationEvent
    {
        public float Timestamp { get; }
        public string DroneId { get; }
        public string NeutralizedByEffector { get; }
        public Vector3 Position { get; }

        public string EventType => nameof(DroneNeutralizedEvent);
        public string Summary => $"[NEUTRALIZED] Drone {DroneId} neutralized by {NeutralizedByEffector} at {Position}";

        public DroneNeutralizedEvent(float timestamp, string droneId, string neutralizedByEffector, Vector3 position)
        {
            Timestamp = timestamp;
            DroneId = droneId;
            NeutralizedByEffector = neutralizedByEffector;
            Position = position;
        }
    }
}
