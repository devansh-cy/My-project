using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Scenarios;

namespace DroneTrainer.Drones
{
    /// <summary>
    /// Coordinates autonomous drone swarms using a 3D Craig Reynolds Boids model
    /// (Separation, Alignment, Cohesion) augmented with Leader-Waypoint guidance,
    /// dynamic obstacle avoidance, and zero-allocation object pooling for 5–50 drones.
    /// </summary>
    public class SwarmController : MonoBehaviour
    {
        [Header("Swarm Configuration")]
        [SerializeField] [Range(5, 50)] private int swarmCount = 15;
        [SerializeField] private string swarmPrefix = "SWARM-";
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private float spawnSpreadRadius = 30f;

        [Header("Leader & Mission Waypoint")]
        [SerializeField] private Transform leaderTarget;
        [SerializeField] private List<Vector3> leaderWaypoints = new List<Vector3>();
        [SerializeField] private float leaderSpeed = 16f;
        [SerializeField] private float waypointArrivalThreshold = 6f;
        [SerializeField] private bool loopLeaderRoute = true;

        [Header("Boid Behavioral Weights")]
        [Tooltip("Force repelling drones that are too close to each other.")]
        [SerializeField] [Range(0f, 10f)] private float separationWeight = 2.5f;

        [Tooltip("Force aligning heading with average velocity of nearby swarm mates.")]
        [SerializeField] [Range(0f, 10f)] private float alignmentWeight = 1.2f;

        [Tooltip("Force attracting drones toward the center of mass of the flock.")]
        [SerializeField] [Range(0f, 10f)] private float cohesionWeight = 1.0f;

        [Tooltip("Force guiding drones toward the mission leader waypoint.")]
        [SerializeField] [Range(0f, 10f)] private float leaderFollowWeight = 2.2f;

        [Tooltip("Force steering away from terrain and structural obstacles.")]
        [SerializeField] [Range(0f, 15f)] private float obstacleAvoidanceWeight = 4.0f;

        [Header("Sensory Radii")]
        [SerializeField] [Range(5f, 60f)] private float neighborRadius = 25f;
        [SerializeField] [Range(1f, 15f)] private float separationRadius = 7f;
        [SerializeField] [Range(2f, 25f)] private float obstacleLookAhead = 15f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        [Header("Kinematics Limits")]
        [SerializeField] [Range(5f, 40f)] private float maxSpeed = 18f;
        [SerializeField] [Range(5f, 50f)] private float maxSteerForce = 30f;

        [Header("Object Pooling")]
        [SerializeField] private DronePool pool;
        [SerializeField] private DroneAgent dronePrefab;

        // Runtime state
        private readonly List<DroneAgent> activeSwarm = new List<DroneAgent>();
        private int currentLeaderWpIndex = 0;
        private Vector3 virtualLeaderPosition;
        private bool isPincerActive = false;

        public static SwarmController Instance { get; private set; }
        public IReadOnlyList<DroneAgent> ActiveSwarm => activeSwarm;
        public Vector3 LeaderPosition => leaderTarget != null ? leaderTarget.position : virtualLeaderPosition;
        public bool IsPincerActive => isPincerActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsurePool();
        }

        /// <summary>
        /// Phase 4: Tactical Pincer Swarm Maneuver.
        /// Splits swarm into a high-altitude radar decoy wing and a low-altitude terrain-hugging urban flank wing.
        /// </summary>
        public void TriggerPincerManeuver()
        {
            if (activeSwarm.Count < 2) return;
            isPincerActive = true;

            var tower = GameObject.Find("SensorTower") ?? GameObject.Find("FOB_Watchtower");
            Vector3 targetBase = tower != null ? tower.transform.position : Vector3.zero;

            int splitIndex = activeSwarm.Count / 2;

            // Wing 1 (Decoy High Diversion - 50%): High altitude, direct flight, large radar cross section
            for (int i = 0; i < splitIndex; i++)
            {
                DroneAgent agent = activeSwarm[i];
                if (agent == null) continue;

                Vector3 highWp = new Vector3(targetBase.x + Random.Range(-25f, 25f), 85f, targetBase.z + Random.Range(-25f, 25f));
                agent.SetFlightMode(DroneFlightMode.Straight);
                agent.SetWaypoints(new List<Vector3> { agent.transform.position + Vector3.up * 35f, highWp });
            }

            // Wing 2 (Low Urban Flank - 50%): Nap-of-the-earth (12m altitude) flanking through urban canyons
            for (int i = splitIndex; i < activeSwarm.Count; i++)
            {
                DroneAgent agent = activeSwarm[i];
                if (agent == null) continue;

                Vector3 flankWp1 = agent.transform.position + agent.transform.right * 70f;
                flankWp1.y = 12f;
                Vector3 flankWp2 = targetBase + (agent.transform.right * 50f);
                flankWp2.y = 12f;

                agent.SetFlightMode(DroneFlightMode.TerrainHugging);
                agent.SetWaypoints(new List<Vector3> { flankWp1, flankWp2, targetBase });
            }

            Debug.Log($"[SwarmController] Executing tactical PINCER maneuver: {splitIndex} drones high decoy, {activeSwarm.Count - splitIndex} low urban flank.");
        }

        private void Start()
        {
            virtualLeaderPosition = transform.position;

            if (leaderWaypoints == null || leaderWaypoints.Count == 0)
            {
                GenerateDynamicAttackRoute();
            }

            if (spawnOnStart)
            {
                SpawnSwarm(swarmCount);
            }
        }

        public void GenerateDynamicAttackRoute()
        {
            if (leaderWaypoints == null) leaderWaypoints = new List<Vector3>();
            leaderWaypoints.Clear();
            Vector3 origin = transform.position;
            Vector3 baseTarget = Vector3.zero;

            var tower = GameObject.Find("SensorTower") ?? GameObject.Find("FOB_Watchtower");
            if (tower != null) baseTarget = tower.transform.position;

            // Ingress waypoints weaving through airspace towards base
            Vector3 wp1 = Vector3.Lerp(origin, baseTarget, 0.35f) + new Vector3(Random.Range(-80f, 80f), Random.Range(-5f, 10f), 0f);
            leaderWaypoints.Add(wp1);

            Vector3 wp2 = Vector3.Lerp(origin, baseTarget, 0.70f) + new Vector3(Random.Range(-40f, 40f), -10f, Random.Range(-20f, 20f));
            wp2.y = Mathf.Max(18f, wp2.y);
            leaderWaypoints.Add(wp2);

            // Orbiting ring around target perimeter
            leaderWaypoints.Add(baseTarget + new Vector3(45f, 25f, 30f));
            leaderWaypoints.Add(baseTarget + new Vector3(-35f, 20f, 40f));
            leaderWaypoints.Add(baseTarget + new Vector3(-40f, 22f, -35f));
            leaderWaypoints.Add(baseTarget + new Vector3(30f, 28f, -40f));

            loopLeaderRoute = true;
        }

        private void Update()
        {
            UpdateLeaderPosition();
            SimulateBoids();
        }

        #region Swarm Lifecycle & Spawning

        /// <summary>
        /// Spawns the specified number of drones (clamped between 5 and 50) using the object pool.
        /// </summary>
        [ContextMenu("Spawn / Reset Swarm")]
        public void RespawnSwarmFromInspector()
        {
            SpawnSwarm(swarmCount);
        }

        public void SpawnSwarm(int count)
        {
            EnsurePool();
            DespawnSwarm();

            int targetCount = Mathf.Clamp(count, 5, 50);
            Vector3 origin = transform.position;

            for (int i = 0; i < targetCount; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * spawnSpreadRadius;
                randomOffset.y = Mathf.Abs(randomOffset.y) * 0.5f; // keep altitude grouped above ground
                Vector3 spawnPos = origin + randomOffset;
                Quaternion spawnRot = Quaternion.LookRotation((Vector3.zero - spawnPos).normalized, Vector3.up);

                DroneAgent agent = pool.Get(spawnPos, spawnRot);
                if (agent != null)
                {
                    float individualSpeed = maxSpeed + Random.Range(-2.5f, 3.5f);
                    agent.Initialize($"{swarmPrefix}{i + 1:D2}", true, DroneFlightMode.SwarmBoid, individualSpeed);

                    // Add intelligent evasive adversarial behavior
                    var adv = agent.GetComponent<AdversarialSwarmAgent>();
                    if (adv == null)
                    {
                        agent.gameObject.AddComponent<AdversarialSwarmAgent>();
                    }

                    // Broadcast ground-truth spawn event for telemetry logging and scoring
                    ThreatType threat = (i % 3 == 0) ? ThreatType.TacticalQuadcopter 
                                      : ((i % 4 == 0) ? ThreatType.FPVKamikaze : ThreatType.CommercialQuadcopter);
                    DroneTrainer.Core.Events.EventBus.Publish(new DroneTrainer.Logging.DroneSpawnedEvent(
                        Time.time,
                        agent.DroneId,
                        threat,
                        spawnPos,
                        true
                    ));

                    activeSwarm.Add(agent);
                }
            }

            Debug.Log($"[SwarmController] Successfully deployed {activeSwarm.Count} drones into formation.");
        }

        /// <summary>
        /// Recycles all active swarm drones back to the object pool.
        /// </summary>
        [ContextMenu("Despawn Swarm")]
        public void DespawnSwarm()
        {
            if (pool != null)
            {
                for (int i = 0; i < activeSwarm.Count; i++)
                {
                    pool.ReturnToPool(activeSwarm[i]);
                }
            }
            activeSwarm.Clear();
        }

        private void EnsurePool()
        {
            if (pool == null)
            {
                pool = GetComponent<DronePool>();
                if (pool == null)
                {
                    pool = gameObject.AddComponent<DronePool>();
                }
            }
        }

        #endregion

        #region Leader Waypoint Navigation

        private void UpdateLeaderPosition()
        {
            if (leaderTarget != null)
            {
                virtualLeaderPosition = leaderTarget.position;
                return;
            }

            // Follow virtual waypoints if no physical Transform assigned
            if (leaderWaypoints == null || leaderWaypoints.Count == 0)
            {
                // Advance straight forward if no waypoints defined
                virtualLeaderPosition += transform.forward * (leaderSpeed * Time.deltaTime);
                return;
            }

            Vector3 targetWp = leaderWaypoints[currentLeaderWpIndex];
            Vector3 toWp = targetWp - virtualLeaderPosition;

            if (toWp.magnitude <= waypointArrivalThreshold)
            {
                currentLeaderWpIndex++;
                if (currentLeaderWpIndex >= leaderWaypoints.Count)
                {
                    currentLeaderWpIndex = loopLeaderRoute ? 0 : leaderWaypoints.Count - 1;
                }
                targetWp = leaderWaypoints[currentLeaderWpIndex];
                toWp = targetWp - virtualLeaderPosition;
            }

            virtualLeaderPosition += toWp.normalized * (leaderSpeed * Time.deltaTime);
        }

        #endregion

        #region Boids Flocking Simulation

        private void SimulateBoids()
        {
            int count = activeSwarm.Count;
            if (count == 0) return;

            float neighborRadiusSqr = neighborRadius * neighborRadius;
            float separationRadiusSqr = separationRadius * separationRadius;
            Vector3 currentLeader = LeaderPosition;

            for (int i = 0; i < count; i++)
            {
                DroneAgent agent = activeSwarm[i];
                if (agent == null || agent.IsNeutralized) continue;

                Vector3 agentPos = agent.transform.position;
                Vector3 agentVel = agent.Velocity;

                Vector3 separationSum = Vector3.zero;
                Vector3 alignmentSum = Vector3.zero;
                Vector3 cohesionSum = Vector3.zero;
                int neighborCount = 0;

                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;

                    DroneAgent neighbor = activeSwarm[j];
                    if (neighbor == null || neighbor.IsNeutralized) continue;

                    Vector3 diff = agentPos - neighbor.transform.position;
                    float distSqr = diff.sqrMagnitude;

                    if (distSqr < neighborRadiusSqr && distSqr > 0.0001f)
                    {
                        neighborCount++;

                        // Cohesion & Alignment accumulation
                        cohesionSum += neighbor.transform.position;
                        alignmentSum += neighbor.Velocity;

                        // Separation: stronger push when closer
                        if (distSqr < separationRadiusSqr)
                        {
                            float dist = Mathf.Sqrt(distSqr);
                            separationSum += (diff / (dist * dist)); // Inverse square repulsion
                        }
                    }
                }

                Vector3 totalSteering = Vector3.zero;

                if (neighborCount > 0)
                {
                    // 1. Separation
                    Vector3 desiredSeparation = (separationSum / neighborCount).normalized * maxSpeed;
                    Vector3 steerSeparation = Vector3.ClampMagnitude(desiredSeparation - agentVel, maxSteerForce);
                    totalSteering += steerSeparation * separationWeight;

                    // 2. Alignment
                    Vector3 averageVel = (alignmentSum / neighborCount).normalized * maxSpeed;
                    Vector3 steerAlignment = Vector3.ClampMagnitude(averageVel - agentVel, maxSteerForce);
                    totalSteering += steerAlignment * alignmentWeight;

                    // 3. Cohesion
                    Vector3 centerOfMass = cohesionSum / neighborCount;
                    Vector3 toCenter = (centerOfMass - agentPos).normalized * maxSpeed;
                    Vector3 steerCohesion = Vector3.ClampMagnitude(toCenter - agentVel, maxSteerForce);
                    totalSteering += steerCohesion * cohesionWeight;
                }

                // 4. Leader Waypoint Following
                Vector3 toLeader = (currentLeader - agentPos).normalized * maxSpeed;
                Vector3 steerLeader = Vector3.ClampMagnitude(toLeader - agentVel, maxSteerForce);
                totalSteering += steerLeader * leaderFollowWeight;

                // 5. Obstacle / Terrain Avoidance
                Vector3 steerAvoidance = ComputeObstacleAvoidance(agentPos, agent.transform.forward);
                totalSteering += steerAvoidance * obstacleAvoidanceWeight;

                // Clamp total applied force and dispatch to agent
                totalSteering = Vector3.ClampMagnitude(totalSteering, maxSteerForce);
                agent.SetBoidSteeringForce(totalSteering);
            }
        }

        private Vector3 ComputeObstacleAvoidance(Vector3 origin, Vector3 forward)
        {
            if (Physics.SphereCast(origin, 1.5f, forward, out RaycastHit hit, obstacleLookAhead, obstacleLayerMask))
            {
                // Steer perpendicularly away from the surface normal
                Vector3 reflectDir = Vector3.Reflect(forward, hit.normal);
                return reflectDir.normalized * maxSpeed;
            }

            return Vector3.zero;
        }

        #endregion

        #region Gizmos & Debug Visualization

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(LeaderPosition, 2f);

            if (leaderWaypoints != null && leaderWaypoints.Count > 1)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < leaderWaypoints.Count - 1; i++)
                {
                    Gizmos.DrawLine(leaderWaypoints[i], leaderWaypoints[i + 1]);
                }
                if (loopLeaderRoute)
                {
                    Gizmos.DrawLine(leaderWaypoints[leaderWaypoints.Count - 1], leaderWaypoints[0]);
                }
            }

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, neighborRadius);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, separationRadius);
        }

        #endregion
    }
}
