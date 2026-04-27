using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Lazy adapter that bridges <see cref="NoctuaAuthentication"/> to the <see cref="IAuthProvider"/> interface.
    /// Uses deferred initialization because IAP is constructed before Auth in the Noctua constructor.
    /// </summary>
    internal class LazyAuthProvider : IAuthProvider
    {
        private NoctuaAuthentication _auth;

        /// <summary>Sets the real auth instance once it is constructed.</summary>
        internal void SetAuth(NoctuaAuthentication auth) => _auth = auth;

        /// <inheritdoc />
        public long? PlayerId => _auth?.RecentAccount?.Player?.Id;

        /// <inheritdoc />
        public UserBundle RecentAccount => _auth?.RecentAccount;

        /// <inheritdoc />
        public UniTask<UserBundle> AuthenticateAsync() => _auth.AuthenticateAsync();

        /// <inheritdoc />
        public UniTask UpdatePlayerAccountAsync(PlayerAccountData data)
            => _auth.UpdatePlayerAccountAsync(data);
    }
}
