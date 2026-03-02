using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides authentication context needed by the IAP service.
    /// Decouples IAP from the static Noctua.Auth singleton.
    /// </summary>
    public interface IAuthProvider
    {
        /// <summary>Current player ID, or null if not authenticated.</summary>
        long? PlayerId { get; }

        /// <summary>Full recent account for cases needing more context.</summary>
        UserBundle RecentAccount { get; }

        /// <summary>Perform authentication (used in offline-recovery flows).</summary>
        UniTask<UserBundle> AuthenticateAsync();

        /// <summary>Update player account data (e.g., in-game role/server info).</summary>
        UniTask UpdatePlayerAccountAsync(PlayerAccountData data);
    }
}
