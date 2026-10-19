using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Trainee;

namespace DroneTrainer.Drones
{
    /// <summary>
    /// Autonomous flight movement modes for DroneAgent.
    /// </summary>
    public enum DroneFlightMode
    {
        Straight,
        Orbit,
        ZigZag,
        TerrainHugging,
        SwarmBoid,
        Hover
    }

    /// <summary>
    /// Individual drone simulation agent supporting standalone pattern following
    /// (Straight, Orbit, ZigZag, TerrainHugging) and swarm Boid participation.
    /// Simulates realistic banking, pitch tilt, altitude management, and waypoint progression.
    /// </summary>
    [SelectionBase]
    public class DroneAgent : MonoBehaviour
    {
        [Header("Identity & Telemetry")]
        [SerializeField] private string droneId = "DRONE-001";
        [SerializeField] private bool isHostile = true;
        [SerializeField] private bool isNeutralized = false;
        [SerializeField] private UasMilSpecGroup milSpecGroup = UasMilSpecGroup.Group1_Micro;

        public UasMilSpecGroup MilSpecGroup
        {
            get => milSpecGroup;
            set => milSpecGroup = value;
        }

        [Header("Flight Parameters")]
        [SerializeField] [Range(1f, 60f)] private float maxSpeed = 15f;
        [SerializeField] [Range(1f, 40f)] private float acceleration = 10f;
        [SerializeField] [Range(30f, 360f)] private float turnSpeedDeg = 120f;
        [SerializeField] [Range(0.5f, 20f)] private float waypointArrivalDistance = 4f;

        [Header("Banking & Visual Aerodynamics")]
        [SerializeField] [Range(0f, 60f)] private float maxBankAngle = 35f;
        [SerializeField] [Range(0f, 45f)] private float maxPitchAngle = 25f;
        [SerializeField] private float tiltDamping = 6f;

        [Header("Pattern: Orbit")]
        [SerializeField] private Vector3 orbitCenter = Vector3.zero;
        [SerializeField] private float orbitRadius = 60f;
        [SerializeField] private float orbitSpeedMultiplier = 1f;
        [SerializeField] private bool orbitClockwise = true;

        [Header("Pattern: ZigZag (Evasive)")]
        [SerializeField] private float zigZagAmplitude = 18f;
        [SerializeField] private float zigZagFrequency = 1.2f;

        [Header("Pattern: Terrain Hugging (Nap-of-the-Earth)")]
        [SerializeField] private float targetGroundClearance = 12f;
        [SerializeField] private LayerMask groundLayerMask = ~0;
        [SerializeField] private float groundRayDistance = 150f;
        [SerializeField] private float verticalFollowSpeed = 6f;

        [Header("Active Waypoints")]
        [SerializeField] private List<Vector3> waypoints = new List<Vector3>();
        [SerializeField] private bool loopWaypoints = false;

        // Current runtime state
        private DroneFlightMode currentMode = DroneFlightMode.Straight;
        private int currentWaypointIndex = 0;
        private Vector3 velocity = Vector3.zero;
        private Vector3 externalSteeringForce = Vector3.zero;
        private float patternTimer = 0f;
        private float currentOrbitAngle = 0f;
        private ResponseOption neutralizingCause = ResponseOption.Monitor;
        private Vector3 tumbleAngularVelocity = Vector3.zero;
        private float fallSpeed = 0f;
        private bool hasHitGround = false;

        // Properties
        public string DroneId => droneId;
        public bool IsHostile => isHostile;
        public bool IsNeutralized => isNeutralized;
        public Vector3 Velocity => velocity;
        public float CurrentSpeed => velocity.magnitude;
        public DroneFlightMode CurrentMode => currentMode;

        private void Update()
        {
            if (isNeutralized)
            {
                ApplyNeutralizedPhysics();
                return;
            }

            patternTimer += Time.deltaTime;

            Vector3 desiredVelocity = CalculateDesiredVelocity();

            // Blend with external boid/swarm forces if present
            if (currentMode == DroneFlightMode.SwarmBoid)
            {
                desiredVelocity += externalSteeringForce;
                if (desiredVelocity.sqrMagnitude > maxSpeed * maxSpeed)
                {
                    desiredVelocity = desiredVelocity.normalized * maxSpeed;
                }
            }

            // Smoothly accelerate towards desired velocity
            velocity = Vector3.MoveTowards(velocity, desiredVelocity, acceleration * Time.deltaTime);

            // Apply terrain hugging altitude correction across any mode if enabled
            if (currentMode == DroneFlightMode.TerrainHugging || currentMode == DroneFlightMode.Straight || currentMode == DroneFlightMode.SwarmBoid)
            {
                AdjustAltitudeForTerrain();
            }

            // Integrate position
            transform.position += velocity * Time.deltaTime;

            // Apply realistic body banking & orientation
            UpdateOrientationAndTilt();
        }

        #region Setup & Configuration

        /// <summary>
        /// Initializes the drone agent for runtime or pooled reuse.
        /// </summary>
        public void Initialize(string id, bool hostile, DroneFlightMode mode, float speed, List<Vector3> routeWaypoints = null)
        {
            droneId = id;
            isHostile = hostile;
            currentMode = mode;
            maxSpeed = Mathf.Max(1f, speed);
            isNeutralized = false;
            hasHitGround = false;
            fallSpeed = 0f;
            patternTimer = 0f;
            currentWaypointIndex = 0;
            velocity = transform.forward * (maxSpeed * 0.25f);
            externalSteeringForce = Vector3.zero;

            waypoints.Clear();
            if (routeWaypoints != null && routeWaypoints.Count > 0)
            {
                waypoints.AddRange(routeWaypoints);
            }

            // If entering Orbit mode, calculate initial angle relative to center
            if (mode == DroneFlightMode.Orbit)
            {
                Vector3 toCenter = transform.position - orbitCenter;
                toCenter.y = 0f;
                currentOrbitAngle = Mathf.Atan2(toCenter.z, toCenter.x);
                if (toCenter.magnitude > 5f)
                {
                    orbitRadius = toCenter.magnitude;
                }
            }
        }

        public void SetFlightMode(DroneFlightMode mode)
        {
            currentMode = mode;
        }

        public void SetWaypoints(List<Vector3> newWaypoints, bool loop = false)
        {
            waypoints = newWaypoints != null ? new List<Vector3>(newWaypoints) : new List<Vector3>();
            currentWaypointIndex = 0;
            loopWaypoints = loop;
        }

        public void SetOrbitParameters(Vector3 center, float radius, bool clockwise = true)
        {
            orbitCenter = center;
            orbitRadius = Mathf.Max(5f, radius);
            orbitClockwise = clockwise;
            currentMode = DroneFlightMode.Orbit;

            Vector3 offset = transform.position - center;
            offset.y = 0f;
            currentOrbitAngle = Mathf.Atan2(offset.z, offset.x);
        }

        public void SetBoidSteeringForce(Vector3 force)
        {
            externalSteeringForce = force;
        }

        public void Neutralize()
        {
            isNeutralized = true;
            currentMode = DroneFlightMode.Hover;
        }

        #endregion

        #region Flight Pattern Computations

        private Vector3 CalculateDesiredVelocity()
        {
            switch (currentMode)
            {
                case DroneFlightMode.Straight:
                    return ComputeStraightOrWaypointVelocity();

                case DroneFlightMode.Orbit:
                    return ComputeOrbitVelocity();

                case DroneFlightMode.ZigZag:
                    return ComputeZigZagVelocity();

                case DroneFlightMode.TerrainHugging:
                    return ComputeTerrainHuggingVelocity();

                case DroneFlightMode.SwarmBoid:
                    // Swarm controller drives steering force; base desired velocity keeps forward momentum
                    return transform.forward * maxSpeed;

                case DroneFlightMode.Hover:
                default:
                    return Vector3.zero;
            }
        }

        private Vector3 ComputeStraightOrWaypointVelocity()
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                return transform.forward * maxSpeed;
            }

            Vector3 target = waypoints[currentWaypointIndex];
            Vector3 toTarget = target - transform.position;
            float distance = toTarget.magnitude;

            if (distance <= waypointArrivalDistance)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Count)
                {
                    if (loopWaypoints)
                    {
                        currentWaypointIndex = 0;
                    }
                    else
                    {
                        currentWaypointIndex = waypoints.Count - 1;
                        return Vector3.zero; // Reached final destination
                    }
                }
                target = waypoints[currentWaypointIndex];
                toTarget = target - transform.position;
            }

            return toTarget.normalized * maxSpeed;
        }

        private Vector3 ComputeOrbitVelocity()
        {
            float directionSign = orbitClockwise ? -1f : 1f;
            float angularSpeed = (maxSpeed / orbitRadius) * orbitSpeedMultiplier;
            currentOrbitAngle += directionSign * angularSpeed * Time.deltaTime;

            Vector3 idealPosition = orbitCenter + new Vector3(
                Mathf.Cos(currentOrbitAngle) * orbitRadius,
                0f,
                Mathf.Sin(currentOrbitAngle) * orbitRadius
            );
            idealPosition.y = orbitCenter.y;

            // Blend tangential movement with radial correction to stay locked on orbit circle
            Vector3 radialCorrection = (idealPosition - transform.position) * 1.5f;
            Vector3 tangentDirection = new Vector3(-Mathf.Sin(currentOrbitAngle) * directionSign, 0f, Mathf.Cos(currentOrbitAngle) * directionSign);

            Vector3 orbitVelocity = (tangentDirection * maxSpeed) + radialCorrection;
            return Vector3.ClampMagnitude(orbitVelocity, maxSpeed);
        }

        private Vector3 ComputeZigZagVelocity()
        {
            Vector3 baseForward = transform.forward;
            if (waypoints != null && waypoints.Count > 0 && currentWaypointIndex < waypoints.Count)
            {
                Vector3 toWp = waypoints[currentWaypointIndex] - transform.position;
                if (toWp.magnitude > waypointArrivalDistance)
                {
                    baseForward = toWp.normalized;
                }
                else
                {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= waypoints.Count && loopWaypoints)
                        currentWaypointIndex = 0;
                }
            }

            Vector3 lateralRight = Vector3.Cross(Vector3.up, baseForward).normalized;
            float lateralSineOffset = Mathf.Sin(patternTimer * zigZagFrequency * Mathf.PI * 2f) * zigZagAmplitude;

            Vector3 desiredHeading = (baseForward * maxSpeed) + (lateralRight * lateralSineOffset);
            return desiredHeading.normalized * maxSpeed;
        }

        private Vector3 ComputeTerrainHuggingVelocity()
        {
            Vector3 baseVelocity = ComputeStraightOrWaypointVelocity();
            return baseVelocity;
        }

        private void AdjustAltitudeForTerrain()
        {
            Ray ray = new Ray(transform.position + Vector3.up * 10f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, groundRayDistance, groundLayerMask))
            {
                float desiredY = hit.point.y + targetGroundClearance;
                float newY = Mathf.Lerp(transform.position.y, desiredY, verticalFollowSpeed * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }
        }

        #endregion

        #region Banking and Attitude Control

        private void UpdateOrientationAndTilt()
        {
            if (velocity.sqrMagnitude < 0.1f) return;

            // Desired yaw rotation towards horizontal velocity
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            if (horizontalVelocity.sqrMagnitude > 0.05f)
            {
                Quaternion targetYaw = Quaternion.LookRotation(horizontalVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetYaw, turnSpeedDeg * Time.deltaTime);
            }

            // Calculate lateral acceleration for banking (turning left/right)
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            float angularVelocityYaw = transform.InverseTransformDirection(Vector3.Cross(transform.forward, velocity.normalized)).y;
            float targetBank = -Mathf.Clamp(angularVelocityYaw * maxBankAngle, -maxBankAngle, maxBankAngle);

            // Calculate forward acceleration/speed for pitch (leaning forward when flying fast)
            float speedRatio = Mathf.Clamp01(velocity.magnitude / maxSpeed);
            float targetPitch = speedRatio * maxPitchAngle;

            // Apply smoothed tilt relative to current heading
            Vector3 currentEuler = transform.eulerAngles;
            float smoothedPitch = Mathf.LerpAngle(currentEuler.x, targetPitch, tiltDamping * Time.deltaTime);
            float smoothedRoll = Mathf.LerpAngle(currentEuler.z, targetBank, tiltDamping * Time.deltaTime);

            transform.rotation = Quaternion.Euler(smoothedPitch, currentEuler.y, smoothedRoll);
        }

        public void Neutralize(ResponseOption cause)
        {
            if (isNeutralized) return;
            isNeutralized = true;
            neutralizingCause = cause;
            fallSpeed = Mathf.Max(2f, velocity.y);

            switch (cause)
            {
                case ResponseOption.DirectedEnergyLaser:
                    tumbleAngularVelocity = new Vector3(Random.Range(180f, 360f), Random.Range(300f, 600f), Random.Range(180f, 360f));
                    break;
                case ResponseOption.ElectronicJammer:
                    tumbleAngularVelocity = new Vector3(Random.Range(45f, 90f), Random.Range(120f, 240f), Random.Range(45f, 90f));
                    break;
                case ResponseOption.NetCatcher:
                    tumbleAngularVelocity = new Vector3(Random.Range(15f, 35f), Random.Range(10f, 30f), Random.Range(15f, 35f));
                    break;
                case ResponseOption.KineticInterceptor:
                    tumbleAngularVelocity = Random.insideUnitSphere * 720f;
                    break;
                default:
                    tumbleAngularVelocity = new Vector3(90f, 180f, 45f);
                    break;
            }

            // Publish ground-truth neutralization event
            DroneTrainer.Core.Events.EventBus.Publish(new DroneTrainer.Logging.DroneNeutralizedEvent(
                Time.time,
                droneId,
                cause.ToString(),
                transform.position
            ));
        }

        private void ApplyNeutralizedPhysics()
        {
            if (hasHitGround) return;

            // Gravity acceleration
            fallSpeed += 9.81f * 2.2f * Time.deltaTime;
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, 4f * Time.deltaTime);
            velocity.y = -fallSpeed;

            transform.position += velocity * Time.deltaTime;
            transform.Rotate(tumbleAngularVelocity * Time.deltaTime);

            // Ground impact check
            if (transform.position.y <= 0.35f)
            {
                hasHitGround = true;
                Vector3 p = transform.position;
                p.y = 0.2f;
                transform.position = p;
                transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), transform.eulerAngles.y, Random.Range(-20f, 20f));
                SpawnCrashSmoke(p);
            }
        }

        private void SpawnCrashSmoke(Vector3 position)
        {
            GameObject smokeObj = new GameObject($"CrashSmoke_{droneId}");
            smokeObj.transform.position = position;

            ParticleSystem ps = smokeObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 3.5f;
            main.startSpeed = 1.2f;
            main.startSize = 0.8f;
            main.startColor = new Color(0.25f, 0.25f, 0.25f, 0.65f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.duration = 4f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 12f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.3f, 0.3f, 0.3f), 0f), new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = smokeObj.GetComponent<ParticleSystemRenderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                ?? Shader.Find("Particles/Standard Unlit") 
                ?? Shader.Find("Sprites/Default");
            renderer.material = new Material(unlit);

            ps.Play();
            Destroy(smokeObj, 6f);
        }

        #endregion
    }
}
