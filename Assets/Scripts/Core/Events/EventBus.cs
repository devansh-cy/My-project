using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneTrainer.Core.Events
{
    /// <summary>
    /// Lightweight, zero-garbage, type-safe EventBus providing decoupled publish/subscribe
    /// messaging across all simulation subsystems (Sensors, Scoring, Trainee UI, AAR).
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> subscribers = new Dictionary<Type, List<Delegate>>();

        /// <summary>
        /// Subscribes a listener callback to an event type.
        /// </summary>
        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;

            Type type = typeof(T);
            if (!subscribers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                subscribers[type] = list;
            }

            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribes a listener callback from an event type.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;

            Type type = typeof(T);
            if (subscribers.TryGetValue(type, out var list))
            {
                list.Remove(handler);
                if (list.Count == 0)
                {
                    subscribers.Remove(type);
                }
            }
        }

        /// <summary>
        /// Publishes an event payload to all registered listeners.
        /// </summary>
        public static void Publish<T>(T eventData)
        {
            Type type = typeof(T);
            if (!subscribers.TryGetValue(type, out var list)) return;

            // Iterate backwards so handlers can safely unsubscribe during invocation
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i < list.Count && list[i] is Action<T> action)
                {
                    try
                    {
                        action.Invoke(eventData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EventBus] Exception handling event {type.Name}: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Clears all event subscriptions (useful between scenario resets or scene transitions).
        /// </summary>
        public static void ClearAll()
        {
            subscribers.Clear();
        }
    }
}
