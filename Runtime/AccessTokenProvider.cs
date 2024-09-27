namespace com.noctuagames.sdk
{
    internal interface IAccessTokenProvider  
    {
        public string AccessToken { get; }
        
        public bool IsAuthenticated { get; }
    }
    
    internal class AccessTokenProvider : IAccessTokenProvider
    {
        internal AccessTokenProvider(NoctuaAuthenticationService auth)
        {
            auth.OnAccountChanged += OnAccountChanged;
            auth.OnAccountDeleted += OnAccountDeleted;
        }

        public string AccessToken
        {
            get
            {
                if (string.IsNullOrEmpty(_accessToken))
                {
                    throw new NoctuaException(NoctuaErrorCode.Authentication, "User is not authenticated");
                }
                
                return _accessToken;
            }

            private set => _accessToken = value;
        }
        
        private string _accessToken;

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