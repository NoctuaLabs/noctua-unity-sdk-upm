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
        
        private void Start()
        {
            SessionTracker?.OnApplicationPause(false);
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            SessionTracker?.OnApplicationPause(pauseStatus);
        }
        
        private void OnDestroy()
        {
            SessionTracker?.Dispose();
        }
    }
}