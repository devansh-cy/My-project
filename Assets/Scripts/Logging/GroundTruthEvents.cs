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
        public float timestamp;
        public string droneId;
        public ThreatType threatType;
        public Vector3 spawnPosition;
        public bool isHostile;

        public float Timestamp => timestamp;
        public string DroneId => droneId;
        public ThreatType ThreatType => threatType;
        public Vector3 SpawnPosition => spawnPosition;
        public bool IsHostile => isHostile;

        public string EventType => nameof(DroneSpawnedEvent);
        public string Summary => $"[SPAWN] Drone {droneId} ({threatType}, Hostile={isHostile}) at {spawnPosition}";

        public DroneSpawnedEvent(float timestamp, string droneId, ThreatType threatType, Vector3 spawnPosition, bool isHostile)
        {
            this.timestamp = timestamp;
            this.droneId = droneId;
            this.threatType = threatType;
            this.spawnPosition = spawnPosition;
            this.isHostile = isHostile;
        }
    }

    /// <summary>
    /// Ground-truth event triggered when a target enters a sensor's active coverage or monitored perimeter fence.
    /// </summary>
    [Serializable]
    public struct DroneEnteredRangeEvent : ISimulationEvent
    {
        public float timestamp;
        public string droneId;
        public float rangeMeters;
        public string sensorOrZoneId;

        public float Timestamp => timestamp;
        public string DroneId => droneId;
        public float RangeMeters => rangeMeters;
        public string SensorOrZoneId => sensorOrZoneId;

        public string EventType => nameof(DroneEnteredRangeEvent);
        public string Summary => $"[ENTER_RANGE] Drone {droneId} entered {sensorOrZoneId} at {rangeMeters:F1}m";

        public DroneEnteredRangeEvent(float timestamp, string droneId, float rangeMeters, string sensorOrZoneId)
        {
            this.timestamp = timestamp;
            this.droneId = droneId;
            this.rangeMeters = rangeMeters;
            this.sensorOrZoneId = sensorOrZoneId;
        }
    }

    /// <summary>
    /// Ground-truth event triggered when an incoming hostile threat impacts a protected asset or perimeter.
    /// </summary>
    [Serializable]
    public struct DroneImpactEvent : ISimulationEvent
    {
        public float timestamp;
        public string droneId;
        public Vector3 impactPosition;
        public bool assetDamaged;

        public float Timestamp => timestamp;
        public string DroneId => droneId;
        public Vector3 ImpactPosition => impactPosition;
        public bool AssetDamaged => assetDamaged;

        public string EventType => nameof(DroneImpactEvent);
        public string Summary => $"[IMPACT] Drone {droneId} impacted at {impactPosition}! Damage={assetDamaged}";

        public DroneImpactEvent(float timestamp, string droneId, Vector3 impactPosition, bool assetDamaged)
        {
            this.timestamp = timestamp;
            this.droneId = droneId;
            this.impactPosition = impactPosition;
            this.assetDamaged = assetDamaged;
        }
    }

    /// <summary>
    /// Ground-truth event triggered when a drone is successfully neutralized by an operator countermeasure.
    /// </summary>
    [Serializable]
    public struct DroneNeutralizedEvent : ISimulationEvent
    {
        public float timestamp;
        public string droneId;
        public string neutralizedByEffector;
        public Vector3 position;

        public float Timestamp => timestamp;
        public string DroneId => droneId;
        public string NeutralizedByEffector => neutralizedByEffector;
        public Vector3 Position => position;

        public string EventType => nameof(DroneNeutralizedEvent);
        public string Summary => $"[NEUTRALIZED] Drone {droneId} neutralized by {neutralizedByEffector} at {position}";

        public DroneNeutralizedEvent(float timestamp, string droneId, string neutralizedByEffector, Vector3 position)
        {
            this.timestamp = timestamp;
            this.droneId = droneId;
            this.neutralizedByEffector = neutralizedByEffector;
            this.position = position;
        }
    }
}
