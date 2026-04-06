using UnityEngine;

namespace com.noctuagames.sdk.Events
{
    /// <summary>
    /// MonoBehaviour that bridges native lifecycle callbacks to <see cref="NativeSessionTracker"/>.
    /// The callback is registered externally by <c>Noctua.Initialization</c> immediately after
    /// construction (not in Start/Awake) to avoid missing the first native onResume.
    /// Unregisters and disposes on Destroy.
    /// </summary>
    internal class NativeSessionTrackerBehaviour : MonoBehaviour
    {
        internal NativeSessionTracker NativeSessionTracker;
        internal INativeLifecycle NativeLifecycle;

        // Registration is done externally (Noctua.Initialization.cs) immediately after
        // both NativeSessionTracker and NativeLifecycle are assigned, so that the callback
        // is registered before any native onResume/onPause fires.

        internal void OnNativeLifecycleEvent(string lifecycleEvent)
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
