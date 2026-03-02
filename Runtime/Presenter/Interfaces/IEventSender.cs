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
        void Send(string name, Dictionary<string, IConvertible> data = null);

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
    }
}
