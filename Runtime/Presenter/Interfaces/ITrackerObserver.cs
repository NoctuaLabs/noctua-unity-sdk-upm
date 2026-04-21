using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Observer invoked when a 3rd-party tracker event transitions phase.
    /// Fired by both Unity-layer wrappers (on dispatch) and native bridges
    /// (when the Swift/Kotlin log-tailers or Adjust callbacks fire).
    /// Registered on <see cref="TrackerObserverRegistry"/>.
    /// </summary>
    public interface ITrackerObserver
    {
        void OnEvent(
            string provider,
            string eventName,
            IReadOnlyDictionary<string, object> payload,
            IReadOnlyDictionary<string, object> extraParams,
            TrackerEventPhase phase,
            string error);
    }
}
