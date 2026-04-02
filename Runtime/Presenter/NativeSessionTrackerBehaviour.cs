using UnityEngine;

namespace com.noctuagames.sdk.Events
{
    /// <summary>
    /// MonoBehaviour that bridges native lifecycle callbacks to <see cref="NativeSessionTracker"/>.
    /// Registers the native callback on Start and unregisters + disposes on Destroy.
    /// </summary>
    internal class NativeSessionTrackerBehaviour : MonoBehaviour
    {
        internal NativeSessionTracker NativeSessionTracker;
        internal INativeLifecycle NativeLifecycle;

        private void Start()
        {
            NativeLifecycle?.RegisterNativeLifecycleCallback(OnNativeLifecycleEvent);
        }

        private void OnNativeLifecycleEvent(string lifecycleEvent)
        {
            switch (lifecycleEvent)
            {
                case "resume":
                    NativeSessionTracker?.OnNativeResume();
                    break;
                case "pause":
                    NativeSessionTracker?.OnNativePause();
                    break;
            }
        }

        private void OnDestroy()
        {
            NativeLifecycle?.RegisterNativeLifecycleCallback(null);
            NativeSessionTracker?.Dispose();
        }
    }
}
