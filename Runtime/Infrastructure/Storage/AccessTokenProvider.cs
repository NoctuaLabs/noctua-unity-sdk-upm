using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides access to the current user's authentication token and authentication status.
    /// </summary>
    internal interface IAccessTokenProvider
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
    internal class AccessTokenProvider : IAccessTokenProvider
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(AccessTokenProvider));

        /// <summary>
        /// Initializes the provider and subscribes to account changed/deleted events.
        /// </summary>
        /// <param name="auth">The account events source to subscribe to for token updates.</param>
        internal AccessTokenProvider(IAccountEvents auth)
        {
            auth.OnAccountChanged += OnAccountChanged;
            auth.OnAccountDeleted += OnAccountDeleted;
        }

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
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);
        
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