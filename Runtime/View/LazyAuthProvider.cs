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

        internal void SetAuth(NoctuaAuthentication auth) => _auth = auth;

        public long? PlayerId => _auth?.RecentAccount?.Player?.Id;

        public UserBundle RecentAccount => _auth?.RecentAccount;

        public UniTask<UserBundle> AuthenticateAsync() => _auth.AuthenticateAsync();

        public UniTask UpdatePlayerAccountAsync(PlayerAccountData data)
            => _auth.UpdatePlayerAccountAsync(data);
    }
}
