using UnityEngine;

namespace com.noctuagames.sdk.Events
{
    public class SessionTrackerBehaviour : MonoBehaviour
    {
        public SessionTracker SessionTracker;

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