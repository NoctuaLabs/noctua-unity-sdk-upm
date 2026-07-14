using System;
using UnityEngine;

namespace com.noctuagames.sdk.Events
{
    /// <summary>
    /// MonoBehaviour bridge that forwards Unity lifecycle events (Start, OnApplicationPause, OnDestroy) to a <see cref="SessionTracker"/> instance.
    /// </summary>
    public class SessionTrackerBehaviour : MonoBehaviour
    {
        /// <summary>
        /// The session tracker instance to receive lifecycle callbacks. Must be assigned before Start().
        /// </summary>
        public SessionTracker SessionTracker;

        /// <summary>
        /// Fires when the app returns to the foreground, so other subscribers can piggyback on the
        /// session lifecycle instead of each adding their own MonoBehaviour. Handlers must not throw.
        /// </summary>
        public event Action OnResume;

        private void Start()
        {
            SessionTracker?.OnApplicationPause(false);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            SessionTracker?.OnApplicationPause(pauseStatus);

            if (!pauseStatus)
            {
                OnResume?.Invoke();
            }
        }

        private void OnDestroy()
        {
            SessionTracker?.Dispose();
        }
    }
}