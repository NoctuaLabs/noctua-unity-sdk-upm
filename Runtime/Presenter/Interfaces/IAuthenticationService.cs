using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Contract for the authentication presenter. Defines the auth operations
    /// available to the View/UI layer without depending on the concrete service.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>All accounts known to the SDK.</summary>
        IReadOnlyList<UserBundle> AccountList { get; }

        /// <summary>Accounts for the current game.</summary>
        IReadOnlyList<UserBundle> CurrentGameAccountList { get; }

        /// <summary>Accounts from other games.</summary>
        IReadOnlyList<UserBundle> OtherGamesAccountList { get; }

        /// <summary>Whether any account is currently authenticated.</summary>
        bool IsAuthenticated { get; }

        /// <summary>The most recently used account.</summary>
        UserBundle RecentAccount { get; }

        /// <summary>Raised when the active account changes.</summary>
        event Action<UserBundle> OnAccountChanged;

        /// <summary>Raised when an account is deleted.</summary>
        event Action<Player> OnAccountDeleted;

        /// <summary>Log in as a guest (creates a new anonymous account).</summary>
        UniTask<UserBundle> LoginAsGuestAsync();

        /// <summary>Authenticate using the most recent account or prompt for login.</summary>
        UniTask<UserBundle> AuthenticateAsync();

        /// <summary>Log out the current user.</summary>
        UniTask<UserBundle> LogoutAsync();
    }
}
