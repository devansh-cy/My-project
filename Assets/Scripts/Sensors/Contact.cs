using System;
using UnityEngine;

namespace DroneTrainer.Sensors
{
    /// <summary>
    /// Type of surveillance sensor producing the contact.
    /// </summary>
    public enum SensorType
    {
        Radar,
        EOCamera,
        IRThermalCamera,
        RFDirectionFinder,
        AcousticArray
    }

    /// <summary>
    /// Represents an estimated target detection produced by a sensor system.
    /// Contains measured kinematics, confidence scoring, and ground-truth association flags.
    /// </summary>
    [Serializable]
    public class Contact
    {
        [Tooltip("Unique track / plot identifier assigned by the sensor processor.")]
        public string id;

        [Tooltip("Estimated 3D world coordinates with sensor measurement noise and jitter applied.")]
        public Vector3 estimatedPosition;

        [Tooltip("Confidence metric between 0.0 (marginal signal) and 1.0 (unambiguous high SNR lock).")]
        [Range(0f, 1f)]
        public float confidence;

        [Tooltip("Sensor subsystem that generated this plot.")]
        public SensorType sensorType;

        [Tooltip("Simulation timestamp when this contact was captured.")]
        public float timestamp;

        [Tooltip("Estimated velocity vector (Doppler or multi-sweep tracker).")]
        public Vector3 estimatedVelocity;

        [Tooltip("Estimated Signal-to-Noise Ratio (dB).")]
        public float snrDb;

        [Tooltip("True if this is an atmospheric or clutter false alarm, false if a real airborne track.")]
        public bool isFalseAlarm;

        [Tooltip("Internal ground-truth drone identifier (null if false blip).")]
        public string trueDroneId;

        public Contact() { }

        public Contact(string id, Vector3 estimatedPosition, float confidence, 
                       SensorType sensorType, float timestamp, Vector3 estimatedVelocity = default, 
                       float snrDb = 10f, bool isFalseAlarm = false, string trueDroneId = null)
        {
            this.id = id;
            this.estimatedPosition = estimatedPosition;
            this.confidence = Mathf.Clamp01(confidence);
            this.sensorType = sensorType;
            this.timestamp = timestamp;
            this.estimatedVelocity = estimatedVelocity;
            this.snrDb = snrDb;
            this.isFalseAlarm = isFalseAlarm;
            this.trueDroneId = trueDroneId;
        }

        public override string ToString()
        {
            return $"[{sensorType}] ID: {id} | Pos: {estimatedPosition} | Conf: {confidence:P0} | SNR: {snrDb:F1}dB | False: {isFalseAlarm}";
        }
    }

    /// <summary>
    /// Event dispatched through EventBus whenever a sensor generates a contact or track update.
    /// </summary>
    public struct ContactDetectedEvent
    {
        public Contact Contact { get; }
        public float SimulationTime { get; }

        public ContactDetectedEvent(Contact contact, float simulationTime)
        {
            Contact = contact;
            SimulationTime = simulationTime;
        }
    }
}
