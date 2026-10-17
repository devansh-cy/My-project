using UnityEngine;
using DroneTrainer.Sensors;

namespace DroneTrainer.Drones
{
    /// <summary>
    /// AI-Enabled Adversarial Swarm Agent.
    /// Analyzes the trainee's active sensor gaze, turret orientation, and defense coverage,
    /// dynamically rerouting flight trajectories to exploit radar blind spots, urban shadows,
    /// and operator attention gaps.
    /// </summary>
    [RequireComponent(typeof(DroneAgent))]
    public class AdversarialSwarmAgent : MonoBehaviour
    {
        [Header("Adversarial Intelligence Tuning")]
        [SerializeField] private bool enableEvasiveRerouting = true;
        [SerializeField] [Range(10f, 180f)] private float operatorAwarenessConeDeg = 60f;
        [SerializeField] private float flankDistance = 120f;
        [SerializeField] private float rerouteCooldown = 4f;

        [Header("Defense Station Reference")]
        [SerializeField] private Transform defenseBaseTransform;
        [SerializeField] private EOCameraSim traineeCamera;

        private DroneAgent agent;
        private float lastRerouteTime = -10f;
        private bool isCurrentlyFlanking = false;

        private void Awake()
        {
            agent = GetComponent<DroneAgent>();

            if (defenseBaseTransform == null)
            {
                var baseObj = GameObject.Find("SensorTower") ?? GameObject.Find("FOB_Watchtower");
                if (baseObj != null) defenseBaseTransform = baseObj.transform;
            }

            if (traineeCamera == null)
            {
                traineeCamera = FindAnyObjectByType<EOCameraSim>();
            }
        }

        private void Update()
        {
            if (!enableEvasiveRerouting || agent == null || agent.IsNeutralized) return;

            if (Time.time - lastRerouteTime > rerouteCooldown)
            {
                EvaluateOperatorThreatAwareness();
            }
        }

        private void EvaluateOperatorThreatAwareness()
        {
            Vector3 basePos = defenseBaseTransform != null ? defenseBaseTransform.position : Vector3.zero;
            Vector3 toDrone = (transform.position - basePos).normalized;

            // Determine active operator gaze / surveillance vector
            Vector3 operatorLookDirection = Vector3.forward;
            if (traineeCamera != null)
            {
                operatorLookDirection = traineeCamera.transform.forward;
            }

            // Calculate angle between operator's active gaze and this drone's position
            float angularOffset = Vector3.Angle(operatorLookDirection, toDrone);

            // If the drone is inside the operator's active visual / tracking cone, it is under direct watch!
            if (angularOffset < operatorAwarenessConeDeg * 0.5f)
            {
                if (!isCurrentlyFlanking)
                {
                    ExecuteEvasiveFlank(operatorLookDirection, basePos);
                }
            }
            else
            {
                isCurrentlyFlanking = false;
            }
        }

        private void ExecuteEvasiveFlank(Vector3 operatorLookDir, Vector3 basePos)
        {
            lastRerouteTime = Time.time;
            isCurrentlyFlanking = true;

            // Compute perpendicular evasion vector away from the operator's primary gaze axis
            Vector3 lateralRight = Vector3.Cross(Vector3.up, operatorLookDir).normalized;

            // Choose lateral direction that swings further into blind spots
            float sideSign = (Vector3.Dot(transform.position - basePos, lateralRight) >= 0) ? 1f : -1f;
            Vector3 flankTarget = transform.position + (lateralRight * flankDistance * sideSign);
            flankTarget.y = Mathf.Clamp(transform.position.y * 0.7f, 15f, 50f); // drop lower to utilize Nap-of-the-Earth clutter

            // Inject dynamic waypoint to break line of sight
            var evasiveRoute = new System.Collections.Generic.List<Vector3>
            {
                flankTarget,
                flankTarget + (transform.forward * 100f),
                basePos + new Vector3(sideSign * 80f, 20f, 0f)
            };

            agent.SetWaypoints(evasiveRoute, false);
            agent.SetFlightMode(DroneFlightMode.ZigZag);

            Debug.Log($"<color=#ffaa00>[Adversarial AI]</color> Drone '{agent.DroneId}' detected trainee surveillance! Executing evasive flank to {flankTarget}.");
        }
    }
}
