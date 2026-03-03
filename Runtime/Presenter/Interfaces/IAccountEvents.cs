using System;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Minimal event contract exposing account lifecycle events.
    /// Used by Infrastructure layer (AccessTokenProvider) to subscribe
    /// to account changes without depending on the concrete auth service.
    /// </summary>
    internal interface IAccountEvents
    {
        event Action<UserBundle> OnAccountChanged;
        event Action<Player> OnAccountDeleted;
    }
}
