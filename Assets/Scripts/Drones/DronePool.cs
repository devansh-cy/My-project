using System.Collections.Generic;
using UnityEngine;

namespace DroneTrainer.Drones
{
    /// <summary>
    /// High-performance, zero-allocation object pool for DroneAgent instances.
    /// Manages pre-instantiated drones to eliminate GC hitches during large swarm operations.
    /// </summary>
    public class DronePool : MonoBehaviour
    {
        [Header("Pool Setup")]
        [SerializeField] private DroneAgent dronePrefab;
        [SerializeField] private int initialPoolSize = 50;
        [SerializeField] private bool canGrow = true;
        [SerializeField] private Transform poolContainer;

        private readonly Queue<DroneAgent> availablePool = new Queue<DroneAgent>();
        private readonly List<DroneAgent> activeDrones = new List<DroneAgent>();

        public IReadOnlyList<DroneAgent> ActiveDrones => activeDrones;
        public int AvailableCount => availablePool.Count;
        public int TotalAllocated => availablePool.Count + activeDrones.Count;

        private void Awake()
        {
            if (poolContainer == null)
            {
                poolContainer = transform;
            }

            PrewarmPool();
        }

        /// <summary>
        /// Pre-instantiates drones into the pool.
        /// </summary>
        public void PrewarmPool()
        {
            if (dronePrefab == null) return;

            while (availablePool.Count < initialPoolSize)
            {
                DroneAgent agent = CreateNewInstance();
                availablePool.Enqueue(agent);
            }
        }

        /// <summary>
        /// Retrieves an inactive drone from the pool or creates one if growth is allowed.
        /// </summary>
        public DroneAgent Get(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            DroneAgent agent = null;

            if (availablePool.Count > 0)
            {
                agent = availablePool.Dequeue();
            }
            else if (canGrow)
            {
                agent = CreateNewInstance();
            }
            else
            {
                Debug.LogWarning("[DronePool] Pool exhausted and growth is disabled.");
                return null;
            }

            agent.transform.position = spawnPosition;
            agent.transform.rotation = spawnRotation;
            agent.gameObject.SetActive(true);
            activeDrones.Add(agent);

            return agent;
        }

        /// <summary>
        /// Returns an active drone back to the pool.
        /// </summary>
        public void ReturnToPool(DroneAgent agent)
        {
            if (agent == null) return;

            if (activeDrones.Remove(agent))
            {
                agent.gameObject.SetActive(false);
                agent.transform.SetParent(poolContainer);
                availablePool.Enqueue(agent);
            }
        }

        /// <summary>
        /// Returns all currently active drones back to the pool.
        /// </summary>
        public void ReturnAll()
        {
            for (int i = activeDrones.Count - 1; i >= 0; i--)
            {
                DroneAgent agent = activeDrones[i];
                agent.gameObject.SetActive(false);
                agent.transform.SetParent(poolContainer);
                availablePool.Enqueue(agent);
            }
            activeDrones.Clear();
        }

        private DroneAgent CreateNewInstance()
        {
            if (dronePrefab == null)
            {
                // Create a runtime tactical drone GameObject with procedural composite visual hierarchy
                GameObject fallbackGo = new GameObject("TacticalDrone_Pooled");
                DroneAgent fallbackAgent = fallbackGo.AddComponent<DroneAgent>();
                fallbackGo.AddComponent<ProceduralDroneModel>();
                fallbackGo.transform.SetParent(poolContainer);
                fallbackGo.SetActive(false);
                return fallbackAgent;
            }

            DroneAgent newAgent = Instantiate(dronePrefab, poolContainer);
            newAgent.gameObject.SetActive(false);
            return newAgent;
        }
    }
}
