using System;
using UnityEngine;

namespace DroneTrainer.Scenarios
{
    /// <summary>
    /// Serializable 3D coordinate structure compatible with both Unity's JsonUtility
    /// and Newtonsoft.Json, providing seamless implicit conversions with UnityEngine.Vector3.
    /// </summary>
    [Serializable]
    public struct Vector3Data : IEquatable<Vector3Data>
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3Data Zero => new Vector3Data(0f, 0f, 0f);
        public static Vector3Data One => new Vector3Data(1f, 1f, 1f);

        public static implicit operator Vector3(Vector3Data data)
        {
            return new Vector3(data.x, data.y, data.z);
        }

        public static implicit operator Vector3Data(Vector3 vector)
        {
            return new Vector3Data(vector.x, vector.y, vector.z);
        }

        public Vector3 ToUnityVector3()
        {
            return new Vector3(x, y, z);
        }

        public bool Equals(Vector3Data other)
        {
            return Mathf.Approximately(x, other.x) &&
                   Mathf.Approximately(y, other.y) &&
                   Mathf.Approximately(z, other.z);
        }

        public override bool Equals(object obj)
        {
            return obj is Vector3Data other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + x.GetHashCode();
                hash = hash * 23 + y.GetHashCode();
                hash = hash * 23 + z.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"({x:F1}, {y:F1}, {z:F1})";
        }
    }
}
