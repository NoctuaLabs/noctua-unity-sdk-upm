using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk.Events
{
    /// <summary>
    /// Abstraction for sending analytics events. Used by presenters/services
    /// so they don't depend on the concrete EventSender infrastructure class.
    /// </summary>
    public interface IEventSender
    {
        /// <summary>
        /// Enqueues an analytics event with the given name and optional payload data.
        /// </summary>
        /// <param name="name">The event name (e.g. "purchase_completed", "level_up").</param>
        /// <param name="data">Optional key-value pairs to include as event properties.</param>
        void Send(string name, Dictionary<string, IConvertible> data = null);

        /// <summary>
        /// Sets persistent properties that will be attached to all subsequent events.
        /// Pass <c>null</c> to leave a property unchanged; pass <c>0</c> or <c>""</c> to clear it.
        /// </summary>
        /// <param name="userId">The authenticated user ID.</param>
        /// <param name="playerId">The current player ID.</param>
        /// <param name="credentialId">The credential ID used for authentication.</param>
        /// <param name="credentialProvider">The credential provider name (e.g. "google", "facebook").</param>
        /// <param name="gameId">The current game ID.</param>
        /// <param name="gamePlatformId">The game platform ID.</param>
        /// <param name="sessionId">The current session identifier.</param>
        /// <param name="ipAddress">The device IP address.</param>
        /// <param name="isSandbox">Whether the SDK is running in sandbox mode.</param>
        void SetProperties(
            long? userId = 0,
            long? playerId = 0,
            long? credentialId = 0,
            string credentialProvider = "",
            long? gameId = 0,
            long? gamePlatformId = 0,
            string sessionId = "",
            string ipAddress = "",
            bool? isSandbox = null
        );

        /// <summary>
        /// Flush queued events to the server immediately.
        /// Used by <see cref="SessionTracker"/> on pause/dispose to ensure
        /// session lifecycle events are delivered promptly.
        /// </summary>
        void Flush();

        /// <summary>
        /// A deterministic anonymous user identifier derived from device identity.
        /// </summary>
        string PseudoUserId { get; }
    }
}
