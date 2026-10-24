using CrewChiefV4.GameState;

using System;
using System.Collections.Generic;
using System.Linq;

namespace CrewChiefV4.Events.PubSub
{
    public static class EventBus
    {
        private static readonly Dictionary<GameEvent, List<Action<GameStateData>>> subscribers =
            new Dictionary<GameEvent, List<Action<GameStateData>>>();

        /// <summary>
        /// Subscribe handler to event
        /// </summary>
        public static void Subscribe(GameEvent gameEvent, Action<GameStateData> handler)
        {
            if (!subscribers.TryGetValue(gameEvent, out var handlers))
            {
                handlers = new List<Action<GameStateData>>();
                subscribers[gameEvent] = handlers;
            }

            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }
        /// <summary>
        /// Unsubscribes the specified event handler from the specified game event.
        /// </summary>
        public static void Unsubscribe(GameEvent gameEvent, Action<GameStateData> handler)
        {
            if (subscribers.TryGetValue(gameEvent, out var list))
            {
                list.Remove(handler);
            }
        }

        /// <summary>
        /// Unsubscribes the specified event handler from all game events it is subscribed to.
        /// </summary>
        public static void Unsubscribe(Action<GameStateData> handler)
        {
            foreach (var list in subscribers.Values)
            {
                list.Remove(handler);
            }
        }

        /// <summary>
        /// Unsubscribes all event handlers associated with the specified subscriber object.
        /// </summary>
        public static void Unsubscribe(object subscriber)
        {
            foreach (var list in subscribers.Values)
            {
                list.RemoveAll(handler => ReferenceEquals(handler.Target, subscriber));
            }
        }
        public static void Publish(GameEvent gameEvent, GameStateData currentGameState)
        {
            if (!subscribers.TryGetValue(gameEvent, out var list))
            {
                return;
            }

            foreach (var handler in list.ToArray())
            {
                try
                {
                    Log.Event(() => $"Publishing event '{gameEvent}' to handler '{handler.Method.DeclaringType?.Name}.{handler.Method.Name}'");
                    handler(currentGameState);
                }
                catch (Exception ex)
                {
                    Log.Event(ex.ToString());
                }
            }
        }
        public static string[] GetSubscriberDescriptions(GameEvent gameEvent)
        {
            if (!subscribers.TryGetValue(gameEvent, out var handlers))
            {
                return new string[0];
            }

            return handlers
                .Select(handler =>
                    $"{handler.Method.DeclaringType?.Name}.{handler.Method.Name}")
                .OrderBy(name => name)
                .ToArray();
        }
    }
    public class Example
    {
        public Example()
        {
            EventBus.Subscribe(GameEvent.PlayerLeftPits, OnPlayerLeftPits);
            EventBus.Subscribe(GameEvent.NewLapStarted, OnNewLapStarted);
        }

        private void OnPlayerLeftPits(GameStateData currentGameState)
        {
            // Get whatever information this feature needs.
        }

        private void OnNewLapStarted(GameStateData currentGameState)
        {
            // Handle the new lap.
        }
    }
}
