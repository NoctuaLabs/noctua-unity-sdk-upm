namespace com.noctuagames.sdk
{
    /// <summary>
    /// Identifies which part of the stack originated a <c>client_error</c> event.
    /// Used as the <c>source</c> field in the error payload sent to the Noctua dashboard.
    /// </summary>
    public enum ClientErrorSource
    {
        /// <summary>
        /// Error originated in game logic code (outside the Noctua SDK namespace).
        /// This is the default for <see cref="NoctuaEventService.ReportError"/>.
        /// </summary>
        Game,

        /// <summary>
        /// Error originated inside the Noctua SDK itself.
        /// Use this when reporting errors from SDK wrapper or integration code.
        /// </summary>
        Sdk,

        /// <summary>
        /// Error originated in native (C++/Obj-C/Kotlin) code outside the Unity runtime.
        /// Typically set automatically by the SDK's native crash pipeline — not normally
        /// passed by game developers via <see cref="NoctuaEventService.ReportError"/>.
        /// </summary>
        Native
    }

    /// <summary>
    /// Severity level of a <c>client_error</c> event.
    /// Used as the <c>severity</c> field in the error payload sent to the Noctua dashboard.
    /// </summary>
    public enum ClientErrorSeverity
    {
        /// <summary>
        /// Non-critical issue; the game can recover and continue normally.
        /// Use for expected edge cases that should be monitored but do not affect core gameplay.
        /// </summary>
        Warning,

        /// <summary>
        /// Significant failure that may degrade gameplay or cause data loss.
        /// This is the default for <see cref="NoctuaEventService.ReportError"/>.
        /// </summary>
        Error,

        /// <summary>
        /// Unrecoverable failure; the game is expected to crash or force a restart.
        /// Use when continuing execution would leave the game in a corrupt state.
        /// </summary>
        Fatal
    }
}
