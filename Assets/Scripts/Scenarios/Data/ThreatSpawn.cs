using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneTrainer.Scenarios
{
    /// <summary>
    /// Configuration entry defining a threat spawn wave, trajectory, hostile classification,
    /// and kinematics within a scenario.
    /// </summary>
    [Serializable]
    public class ThreatSpawn
    {
        [Tooltip("Identifier or label for this threat unit or group.")]
        public string threatId = "THREAT-01";

        [Tooltip("Drone or target classification category.")]
        public ThreatType type = ThreatType.CommercialQuadcopter;

        [Tooltip("Number of drones in this wave / formation.")]
        public int count = 1;

        [Tooltip("World coordinate origin where this wave spawns.")]
        public Vector3Data spawnPosition = new Vector3Data(0f, 50f, 500f);

        [Tooltip("Flight pattern behavior executed by the threat.")]
        public FlightPattern flightPattern = FlightPattern.DirectInbound;

        [Tooltip("Cruising speed in meters per second.")]
        public float speed = 15.0f;

        [Tooltip("True if this is a hostile threat; false if civilian/friendly or decoy.")]
        public bool isHostile = true;

        [Tooltip("Time in seconds from scenario start when this spawn triggers.")]
        public float spawnTime = 5.0f;

        [Tooltip("Spread radius in meters when spawning multiple drones in a swarm.")]
        public float swarmSpreadRadius = 25.0f;

        [Tooltip("Optional intermediate tactical waypoints for WaypointRoute or PerimeterLoiter flight patterns.")]
        public List<Vector3Data> waypoints = new List<Vector3Data>();

        [Tooltip("Brief intelligence or visual description of the payload or equipment mounted.")]
        public string payloadDescription = "None";

        public ThreatSpawn() { }

        public ThreatSpawn(string threatId, ThreatType type, int count, Vector3Data spawnPosition, 
                           FlightPattern flightPattern, float speed, bool isHostile, float spawnTime, 
                           float swarmSpreadRadius = 20f, string payloadDescription = "None")
        {
            this.threatId = threatId;
            this.type = type;
            this.count = Mathf.Max(1, count);
            this.spawnPosition = spawnPosition;
            this.flightPattern = flightPattern;
            this.speed = Mathf.Max(0.1f, speed);
            this.isHostile = isHostile;
            this.spawnTime = Mathf.Max(0f, spawnTime);
            this.swarmSpreadRadius = Mathf.Max(0f, swarmSpreadRadius);
            this.payloadDescription = payloadDescription;
            this.waypoints = new List<Vector3Data>();
        }

        /// <summary>
        /// Validates that wave parameters are structurally sound for simulation execution.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(threatId))
            {
                threatId = $"THREAT-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            }

            count = Mathf.Max(1, count);
            speed = Mathf.Max(0.5f, speed);
            spawnTime = Mathf.Max(0f, spawnTime);
            swarmSpreadRadius = Mathf.Max(0f, swarmSpreadRadius);

            if (waypoints == null)
            {
                waypoints = new List<Vector3Data>();
            }
        }
    }
}
