namespace com.noctuagames.sdk
{
    /// <summary>
    /// Observer invoked when a log line is captured from any source (Unity,
    /// native iOS/Android logs, or 3rd-party SDK verbose tails). Registered
    /// on <see cref="LogInspectorHooks"/>. Implementations may be called from
    /// any thread — they must be thread-safe.
    /// </summary>
    public interface ILogObserver
    {
        void OnLog(LogEntry entry);
    }
}
