using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Application = UnityEngine.Device.Application;
using SystemInfo = UnityEngine.Device.SystemInfo;
using Newtonsoft.Json;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Web;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    

    public class NoctuaAuthentication
    {
        public readonly List<string> SsoCloseWebViewKeywords = new() { "https://developers.google.com/identity/protocols/oauth2" };

        // AccountList will be synced data from AccountContainer.Accounts
        public Dictionary<string,UserBundle> AccountList => _service.AccountList;

        public bool IsAuthenticated => _service.IsAuthenticated;

        public UserBundle RecentAccount => _service.RecentAccount;

        public event Action<UserBundle> OnAccountChanged
        {
            add => _service.OnAccountChanged += value;
            remove => _service.OnAccountChanged -= value;
        }
        
        public event Action<Player> OnAccountDeleted
        {
            add => _service.OnAccountDeleted += value;
            remove => _service.OnAccountDeleted -= value;
        }

        private readonly Config _config;
        private readonly GameObject _uiObject;
        private readonly NoctuaAuthenticationBehaviour _uiComponent;
        private readonly NoctuaAuthenticationService _service;
        private HttpServer _oauthHttpServer;

        internal NoctuaAuthentication(Config config)
        {
            _service = new NoctuaAuthenticationService(config.BaseUrl, config.ClientId);
            
            _uiObject = new GameObject("NoctuaAuthenticationUI");
            _uiComponent = _uiObject.AddComponent<NoctuaAuthenticationBehaviour>();
            _uiComponent.AuthService = _service;
        }

        public string GetAccessToken()
        {
            return RecentAccount?.Player?.AccessToken;
        }

        public async UniTask<UserBundle> LoginAsGuest()
        {
            return await _service.LoginAsGuest();
        }

        public async UniTask<UserBundle> ExchangeToken(string accessToken)
        {
            return await _service.ExchangeToken(accessToken);
        }

        public async UniTask<string> GetSocialLoginRedirectURL(string provider)
        {
            return await _service.GetSocialLoginRedirectURL(provider);
        }

        public async UniTask<UserBundle> SocialLogin(string provider, SocialLoginRequest payload)
        {
            return await _service.SocialLogin(provider, payload);
        }

        public async UniTask<PlayerToken> Bind(BindRequest payload)
        {
            return await _service.Bind(payload);
        }

        /// <summary>
        /// Authenticates the user and returns the user bundle.
        /// 
        /// If the user bundle is not detected, it will show the account selection UI.
        /// 
        /// After authentication, it will show a welcome toast for the user.
        /// 
        /// Returns the authenticated user bundle.
        /// </summary>
        /// <returns>A UserBundle object representing the selected account.</returns>
        public async UniTask<UserBundle> AuthenticateAsync()
        {
            return await _service.AuthenticateAsync();
        }

        public async UniTask<UserBundle> SocialLogin(string provider)
        {
            if (RecentAccount == null)
            {
                throw NoctuaException.NoRecentAccount;
            }
            
            Debug.Log("SocialLogin: " + provider);

            var socialLoginUrl = await GetSocialLoginRedirectURL(provider);

            Debug.Log("SocialLogin: " + provider + " " + socialLoginUrl);

#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL

            // Start HTTP server to listen to the callback with random port
            // open the browser with the redirect URL

            var task = new TaskCompletionSource<Dictionary<string, string>>();

            void OnCallbackReceived(string callbackData)
            {
                Debug.Log("HTTP Server received callback: " + callbackData);

                task.TrySetResult(ParseQueryString(callbackData));
            }

            if (_oauthHttpServer is { IsRunning: true })
            {
                _oauthHttpServer.Stop();
            }
            
            _oauthHttpServer = new HttpServer();
            _oauthHttpServer.OnCallbackReceived += OnCallbackReceived;
            _oauthHttpServer.Start();

            var redirectUrl = $"http://localhost:{_oauthHttpServer.Port}";
            var url = $"{socialLoginUrl}&redirect_uri={HttpUtility.UrlEncode(redirectUrl)}";
            Debug.Log($"Open URL with system browser: {url}");

            Application.OpenURL(url);

            var callbackDataMap = await task.Task;


            Debug.Log("HTTP Server received callback, stopping the server");

            _oauthHttpServer.Stop();
            _oauthHttpServer = null;

#elif UNITY_IOS || UNITY_ANDROID
            // Open the browser with the redirect URL

            var task = new TaskCompletionSource<Dictionary<string, string>>();
            
            Application.deepLinkActivated += (uri) =>
            {
                Debug.Log("Deep link activated: " + uri);
                
                var callbackDataMap = ParseQueryString(uri);
                
                task.TrySetResult(callbackDataMap);
            };
            
            var redirectUrl = $"{Application.identifier}:/auth";
            var url = $"{socialLoginUrl}&redirect_uri={redirectUrl}";

            Debug.Log($"Open URL with system browser: {url}");
            Application.OpenURL(url);
            
            var callbackDataMap = await task.Task;
#endif

            var socialLoginRequest = new SocialLoginRequest
            {
                Code = callbackDataMap["code"],
                State = callbackDataMap["state"],
                RedirectUri = redirectUrl
            };

            return await _service.SocialLogin(provider, socialLoginRequest);
        }

        private static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var queryParameters = new Dictionary<string, string>();
            queryString = queryString[(queryString.IndexOf('?') + 1)..];

            var pairs = queryString.Split('&');
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=');

                if (keyValue.Length != 2) continue;

                var key = Uri.UnescapeDataString(keyValue[0]);
                var value = Uri.UnescapeDataString(keyValue[1]);
                queryParameters[key] = value;
            }

            return queryParameters;
        }

        public void CustomerService()
        {
            var customerServiceUrl = Constants.CustomerServiceBaseUrl + "&gameCode=" + this.RecentAccount?.Player?.GameName + "&uid=" + this.RecentAccount?.User?.Id;

            Debug.Log("Open URL with system browser: " + customerServiceUrl);
            Application.OpenURL(customerServiceUrl);
        }

        /// <summary>
        /// Displays the account selection user interface.
        /// 
        /// This function does not take any parameters and returns a UserBundle object.
        /// </summary>
        /// <returns>A UserBundle object representing the selected account.</returns>
        // TODO ganti ke ShowSwitchAccountUI()
        public void SwitchAccount()
        {
            _uiComponent.ShowAccountSelection();
        }

        // TODO not a public facing API, need to be removed
        public void ShowRegisterDialogUI()
        {
            _uiComponent.ShowEmailRegistration(true);
        }

        // TODO not a public facing API, need to be removed
        public void ShowEmailVerificationDialogUI()
        {
            _uiComponent.ShowEmailVerification("foo", "bar", 123);
        }

        public void Reset() {
            _service.ResetAccounts();
        }

        // TODO: Add support for phone
        public async UniTask<CredentialVerification> RegisterWithEmail(string email, string password)
        {
            return await _service.RegisterWithEmail(email, password);
        }

        public async UniTask<UserBundle> VerifyEmailRegistration(int id, string code)
        {
            return await _service.VerifyEmailRegistration(id, code);
        }

        // TODO: Add support for phone
        public async UniTask<UserBundle> LoginWithEmail(string email, string password)
        {
            return await _service.LoginWithEmail(email, password);
        }

        // TODO: Add support for phone
        public async UniTask<CredentialVerification> RequestResetPassword(string email)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/auth/email/reset-password")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email
                    }
                );

            var response = await request.Send<CredentialVerification>();
            return response;
        }

        // TODO: Add support for phone
        public async UniTask<PlayerToken> ConfirmResetPassword(int id, string code, string newPassword)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/auth/email/verify-reset-password")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code,
                        NewPassword = newPassword,
                    }
                );

            var response = await request.Send<PlayerToken>();
            return response;
        }

        public void SwitchAccount(UserBundle user)
        {
            _service.SwitchAccount(user);
        }

        public async UniTask<UserBundle> UpdatePlayerAccountAsync(PlayerAccountData playerAccountData)
        {
            return await _service.UpdatePlayerAccountAsync(playerAccountData);
        }
        
        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }

        public void UserCenter()
        {
            _uiComponent.ShowUserCenter();
        }
    }
    
    internal class HttpServer
    {
        private readonly HttpListener _listener = new();

        public event Action<string> OnCallbackReceived;
        public string Path;
        public int Port;
        public bool IsRunning => _listener.IsListening;

        public void Start(string path = "")
        {
            Path = path;
            Port = GetRandomUnusedPort();
            
            _listener.Prefixes.Add($"http://localhost:{Port}/{path.Trim('/')}/");
            _listener.Start();
            
            Debug.Log($"HTTP Server started on port {Port} with path {Path}");
            
            UniTask.Create(Listen);
        }

        public void Stop()
        {
            _listener.Stop();
            Debug.Log("HTTP Server stopped");
        }

        private async UniTask Listen()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;
                    
                    if (request.HttpMethod != "GET")
                    {
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        response.Close();
                        continue;
                    }

                    var callbackData = request.Url.Query;

                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "text/plain";
                    var buffer = System.Text.Encoding.UTF8.GetBytes("Social login completed. You can close this window now.");
                    response.ContentLength64 = buffer.Length;
                    
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    
                    response.Close();

                    OnCallbackReceived?.Invoke(callbackData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"HTTP Server error: {ex.Message}");
                }
            }
        }

        private int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
