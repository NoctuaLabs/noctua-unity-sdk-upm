using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides access to the current user's authentication token and authentication status.
    /// </summary>
    public interface IAccessTokenProvider
    {
        /// <summary>Gets the current access token. Throws if the user is not authenticated.</summary>
        public string AccessToken { get; }

        /// <summary>Gets whether a valid access token is currently available.</summary>
        public bool IsAuthenticated { get; }
    }
    
    /// <summary>
    /// Stores and provides the current user's access token, updating it automatically when
    /// account change or deletion events are raised via <see cref="IAccountEvents"/>.
    /// Falls back to <see cref="PlayerPrefs"/> if the in-memory token is not set.
    /// </summary>
    public class AccessTokenProvider : IAccessTokenProvider
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(AccessTokenProvider));

        /// <summary>
        /// Initializes the provider and subscribes to account changed/deleted events.
        /// </summary>
        /// <param name="auth">The account events source to subscribe to for token updates.</param>
        public AccessTokenProvider(IAccountEvents auth)
        {
            auth.OnAccountChanged += OnAccountChanged;
            auth.OnAccountDeleted += OnAccountDeleted;
        }

        // KNOWN SMELL (tracked debt, intentionally not fixed in the hardening MR):
        // both AccessToken and IsAuthenticated do PlayerPrefs I/O inside a property
        // getter, AccessToken also throws and memoizes via a side-effecting write, and
        // neither validates token freshness/expiry. The clean fix is a non-throwing
        // method API (e.g. bool TryGetAccessToken(out string token)) added *alongside*
        // these members, with the throwing getter deprecated over a major version — a
        // breaking change that does not belong in a behaviour-preserving patch. Do not
        // "fix" the getters in place: callers depend on the throw and the lazy fallback.
        /// <inheritdoc />
        public string AccessToken
        {
            get
            {
                if (string.IsNullOrEmpty(_accessToken))
                {
                    _accessToken = PlayerPrefs.GetString("NoctuaAccessToken");

                    if (string.IsNullOrEmpty(_accessToken))
                    {
                         throw new NoctuaException(NoctuaErrorCode.Authentication, "User is not authenticated");
                    }

                    _log.Info("AccessTokenProvider retrieved access token from PlayerPrefs");

                    return _accessToken;
                }
                
                return _accessToken;
            }

            private set => _accessToken = value;
        }
        
        private string _accessToken;

        /// <inheritdoc />
        public bool IsAuthenticated
        {
            get
            {
                if (!string.IsNullOrEmpty(_accessToken))
                {
                    // Never log the token value — only its presence/source.
                    _log.Debug("IsAuthenticated=true (in-memory token present)");
                    return true;
                }

                // Mirror the AccessToken getter's PlayerPrefs fallback (without throwing):
                // a valid persisted token means the user is authenticated even if no code
                // has touched the AccessToken getter yet this session.
                var hasPersistedToken = !string.IsNullOrEmpty(PlayerPrefs.GetString("NoctuaAccessToken"));
                _log.Debug($"IsAuthenticated={hasPersistedToken} (in-memory token empty; PlayerPrefs fallback present={hasPersistedToken})");
                return hasPersistedToken;
            }
        }
        
        private void OnAccountChanged(UserBundle user)
        {
            _accessToken = user?.Player?.AccessToken;
        }
        
        private void OnAccountDeleted(Player player)
        {
            _accessToken = null;
        }
    }
}