using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{

    /// <summary>
    /// Thread-safe event queue for IAA events to ensure they are processed on the main thread
    /// </summary>
    public static class IAAEventQueue
    {
        private static readonly NoctuaLogger _log = new(typeof(IAAEventQueue));
        private static readonly ConcurrentQueue<EventData> _eventQueue = new ConcurrentQueue<EventData>();
        private static bool _isProcessing = false;

        private struct EventData
        {
            public string EventName;
            public Dictionary<string, IConvertible> Payload;
        }

        /// <summary>
        /// Enqueues an event to be processed on the main thread
        /// </summary>
        public static void EnqueueEvent(string eventName, Dictionary<string, IConvertible> payload)
        {
            try
            {
                _eventQueue.Enqueue(new EventData
                {
                    EventName = eventName,
                    Payload = new Dictionary<string, IConvertible>(payload)
                });

                // Start processing if not already running
                if (!_isProcessing)
                {
                    ProcessQueueAsync().Forget();
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error enqueueing event '{eventName}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Processes the event queue on the main thread
        /// </summary>
        private static async UniTaskVoid ProcessQueueAsync()
        {
            if (_isProcessing)
                return;

            _isProcessing = true;

            try
            {
                // Ensure we're on the main thread
                await UniTask.SwitchToMainThread();

                while (_eventQueue.TryDequeue(out EventData eventData))
                {
                    try
                    {
                        _log.Debug($"Processing queued event: {eventData.EventName}");
                        Noctua.Event.TrackCustomEvent(eventData.EventName, eventData.Payload);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Error processing event '{eventData.EventName}': {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error in event queue processing: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _isProcessing = false;

                // If new events were added while processing, start processing again
                if (!_eventQueue.IsEmpty)
                {
                    ProcessQueueAsync().Forget();
                }
            }
        }
    }
}