using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using com.noctuagames.sdk.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    /*
    We were using Model-View-Presenter, further reading:
    - https://en.wikipedia.org/wiki/Model%E2%80%93view%E2%80%93presenter
    - https://medium.com/cr8resume/make-you-hand-dirty-with-mvp-model-view-presenter-eab5b5c16e42
    - https://www.baeldung.com/mvc-vs-mvp-pattern

    But in our case, we have unique conditions:
    1. Our model (*Services.cs) is the public facing API
    2. Our public facing API need to cover both UI and non-UI stuff.

    Thus, we have to tweak the pattern to API-Model-View-Presenter
    1. UI:
    1. Presenter: where we control the state of the UI, but not the main logics
    2. Model: main logics + public facing API
    3. API: the actual model, where we talk to either HTTP API or local storage like player prefs. (TODO)

    AuthenticationModel purposes:
    1. To allow our SDK instance (including UI) to be injected into the Scene
    3. To allow an UI presenter call another UI presenter
    2. To allow model layer (logic) to call an UI presenter
    */

    internal class AuthenticationModel
    {
        // IMPORTANT NOTES!!!
        // Your UI need to apply USS absolute property to the first VisualElement of the UI
        // before being added to the UI Document.
        // Violation of this rule will cause the UI (and the other UI too) to be unable to be displayed properly.
        private readonly UIFactory _uiFactory;
        
        private readonly AccountSelectionDialogPresenter _accountSelectionDialog;
        private readonly SwitchAccountConfirmationDialogPresenter _switchAccountConfirmationDialog;
        private readonly LoginOptionsDialogPresenter _loginOptionsDialog;
        private readonly EmailLoginDialogPresenter _emailLoginDialog;
        private readonly EmailRegisterDialogPresenter _emailRegisterDialog;
        private readonly EmailVerificationDialogPresenter _emailVerificationDialog;
        private readonly WelcomeNotificationPresenter _welcome;
        private readonly EmailResetPasswordDialogPresenter _emailResetPasswordDialog;
        private readonly EmailConfirmResetPasswordDialogPresenter _emailConfirmResetPasswordDialog;
        private readonly UserCenterPresenter _userCenter;
        private readonly GeneralNotificationPresenter _generalNotification;
        private readonly AccountDeletionConfirmationDialogPresenter _accountDeletionConfirmationDialog;

        private NoctuaAuthenticationService _authService;
        
        private readonly Stack<Action> _navigationStack = new();

        public NoctuaAuthenticationService AuthService
        {
            get => _authService;
            set
            {
                if (_authService != null)
                {
                    _authService.OnAccountChanged -= OnAccountChanged;
                }
                
                _authService = value;

                if (_authService != null)
                {
                    _authService.OnAccountChanged += OnAccountChanged;
                }
            }
        } 

        internal event Action<UserBundle> OnAccountChanged;

        private AuthType _currentAuthType = AuthType.SwitchAccount;

        internal AuthenticationModel(UIFactory uiFactory)
        {
            _uiFactory = uiFactory;
            
            _userCenter = _uiFactory.Create<UserCenterPresenter, AuthenticationModel>(this);
            _accountSelectionDialog = _uiFactory.Create<AccountSelectionDialogPresenter, AuthenticationModel>(this);
            _switchAccountConfirmationDialog = _uiFactory.Create<SwitchAccountConfirmationDialogPresenter, AuthenticationModel>(this);
            _loginOptionsDialog = _uiFactory.Create<LoginOptionsDialogPresenter, AuthenticationModel>(this);
            _emailLoginDialog = _uiFactory.Create<EmailLoginDialogPresenter, AuthenticationModel>(this);
            _emailVerificationDialog = _uiFactory.Create<EmailVerificationDialogPresenter, AuthenticationModel>(this);
            _emailRegisterDialog = _uiFactory.Create<EmailRegisterDialogPresenter, AuthenticationModel>(this);
            _emailResetPasswordDialog = _uiFactory.Create<EmailResetPasswordDialogPresenter, AuthenticationModel>(this);
            _emailConfirmResetPasswordDialog = _uiFactory.Create<EmailConfirmResetPasswordDialogPresenter, AuthenticationModel>(this);
            _accountDeletionConfirmationDialog = _uiFactory.Create<AccountDeletionConfirmationDialogPresenter, AuthenticationModel>(this);
            _generalNotification = _uiFactory.Create<GeneralNotificationPresenter, AuthenticationModel>(this);
            _welcome = _uiFactory.Create<WelcomeNotificationPresenter, AuthenticationModel>(this);
        }
        
        internal void PushNavigation(Action action)
        {
            if (action == null) return;
            
            _navigationStack.Push(action);
        }
        
        internal void ClearNavigation()
        {
            _navigationStack.Clear();
        }
        
        internal void NavigateBack()
        {
            if (_navigationStack.Count > 0)
            {
                _navigationStack.Pop().Invoke();
            }
        }

        public void ShowAccountSelection()
        {
            _currentAuthType = AuthType.SwitchAccount;
            _accountSelectionDialog.Show();
        }

        public void ShowEmailRegistration(bool clearForm)
        {
            _emailRegisterDialog.Show(clearForm);
        }

        public void ShowEmailVerification(string email, string password, int verificationID)
        {
            _emailVerificationDialog.Show(email, password, verificationID);
        }

        public void ShowLoginOptions()
        {
            _loginOptionsDialog.Show();
        }

        public void ShowEmailLogin(Action<UserBundle> onLoginSuccess = null)
        {
            _emailLoginDialog.Show(onLoginSuccess);
        }

        public void ShowEmailResetPassword(bool clearForm)
        {
            _emailResetPasswordDialog.Show(clearForm);
        }

        public void ShowEmailConfirmResetPassword(int credVerifyId)
        {
            _emailConfirmResetPasswordDialog.Show(credVerifyId);
        }

        public void ShowSwitchAccountConfirmation(UserBundle recentAccount)
        {
            _switchAccountConfirmationDialog.Show(recentAccount);
        }

        public void ShowAccountDeletionConfirmation(UserBundle recentAccount)
        {
            _accountDeletionConfirmationDialog.Show(recentAccount);
        }
        
        public void ShowUserCenter()
        {
            _currentAuthType = AuthType.LinkAccount;
            _userCenter.Show();
        }

        public void ShowGeneralNotificationError(string message) 
        {
            _generalNotification.Show(message);
        }

        internal async UniTask<CredentialVerification> RegisterWithEmailAsync(string email, string password)
        {
            return _currentAuthType switch
            {
                AuthType.SwitchAccount => await AuthService.RegisterWithEmailAsync(email, password),
                AuthType.LinkAccount => await AuthService.LinkWithEmailAsync(email, password),
                _ => throw new NotImplementedException(_currentAuthType.ToString())
            };
        }
        
        internal async UniTask<UserBundle> VerifyEmailRegistration(int credVerifyId, string code)
        {
            return _currentAuthType switch
            {
                AuthType.SwitchAccount => await AuthService.VerifyEmailRegistrationAsync(credVerifyId, code),
                AuthType.LinkAccount => await AuthService.VerifyEmailLinkingAsync(credVerifyId, code),
                _ => throw new NotImplementedException(_currentAuthType.ToString())
            };
        }

        public async UniTask<UserBundle> SocialLoginAsync(string provider)
        {
            var callbackDataMap = await GetSocialAuthParamsAsync(provider);

            var socialLoginRequest = new SocialLoginRequest
            {
                Code = callbackDataMap["code"],
                State = callbackDataMap["state"],
                RedirectUri = callbackDataMap["redirect_uri"]
            };

            return await AuthService.SocialLoginAsync(provider, socialLoginRequest);
        }

        public async UniTask<UserBundle> SocialLinkAsync(string provider)
        {
            var callbackDataMap = await GetSocialAuthParamsAsync(provider);
            
            var socialLinkRequest = new SocialLinkRequest
            {
                Code = callbackDataMap["code"],
                State = callbackDataMap["state"],
                RedirectUri = callbackDataMap["redirect_uri"]
            };

            return await AuthService.SocialLinkAsync(provider, socialLinkRequest);
        }

        private async UniTask<Dictionary<string, string>> GetSocialAuthParamsAsync(string provider)
        {
#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL

            // Start HTTP server to listen to the callback with random port
            // open the browser with the redirect URL

            var oauthRedirectListener = new OauthRedirectListener();

            var redirectUri = $"http://localhost:{oauthRedirectListener.Port}";

            var socialLoginUrl = await AuthService.GetSocialLoginRedirectURLAsync(provider, redirectUri);

            Debug.Log($"Open URL with system browser: {socialLoginUrl}");

            Application.OpenURL(socialLoginUrl);
            
            var callbackData = await oauthRedirectListener.ListenAsync();
            var callbackDataMap = ParseQueryString(callbackData);
            
            callbackDataMap["redirect_uri"] = redirectUri;

#elif UNITY_IOS || UNITY_ANDROID
            
            Debug.Log("Initializing WebView");
            
            var uniWebView = gameObject.AddComponent<UniWebView>();
            
            if (Application.platform == RuntimePlatform.Android)
            {
                if (provider == "facebook")
                {
                    uniWebView.SetUserAgent("Mozilla/5.0 (Linux; Android 4.4.4; One Build/KTU84L.H4) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/33.0.0.0 Mobile Safari/537.36 [FB_IAB/FB4A;FBAV/28.0.0.20.16;]");
                } else {
                    uniWebView.SetUserAgent("Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Mobile Safari/537.3");
                }
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                uniWebView.SetUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 14_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1");
            }
            
            var tcs = new UniTaskCompletionSource<string>();

            void OnSocialLoginWebviewClosed(UniWebView webView, string windowId)
            {
                tcs.TrySetException(new NoctuaException(NoctuaErrorCode.Authentication, $"{provider} login canceled"));
            }

            bool OnSocialLoginShouldClose(UniWebView webView)
            {
                if (!webView.Url.Contains($"api/v1/auth/{provider}/code")) {
                    Debug.Log("WebView closed by user before login completed");
                    var providerName = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(provider);
                    tcs.TrySetException(new NoctuaException(NoctuaErrorCode.Authentication, $"{providerName} login canceled"));
                }
                // Continue to close the WebView
                return true;
            }
            
            void OnSocialLoginWebviewStarted(UniWebView webView, string url)
            {
                Debug.Log("URL started to load: " + url);

                if (url.Contains($"api/v1/auth/{provider}/code")) {
                    webView.Hide();
                    tcs.TrySetResult(url);
                }
                else if (url.Contains("error") && provider == "google") { // "error" string does not apply for Facebook
                    var providerName = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(provider);
                    tcs.TrySetException(new NoctuaException(NoctuaErrorCode.Authentication, $"{providerName} login failed"));
                }
            }

            void OnSocialLoginWebviewFinished(UniWebView webView, int statusCode, string url)
            {
                Debug.Log("URL finished to load: " + url);
                
                if (url.Contains($"api/v1/auth/{provider}/code")) {
                    webView.Hide();
                    tcs.TrySetResult(url);
                }
                else if (url.Contains("error") && provider == "google") { // "error" string does not apply for Facebook
                    var providerName = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(provider);
                    tcs.TrySetException(new NoctuaException(NoctuaErrorCode.Authentication, $"{providerName} login failed"));
                }
            }        

            uniWebView.OnPageFinished += OnSocialLoginWebviewFinished;
            uniWebView.OnPageStarted += OnSocialLoginWebviewStarted;
            uniWebView.OnMultipleWindowClosed += OnSocialLoginWebviewClosed;
            uniWebView.OnShouldClose += OnSocialLoginShouldClose;

            uniWebView.SetBackButtonEnabled(true);
            uniWebView.EmbeddedToolbar.Show();
            uniWebView.EmbeddedToolbar.SetDoneButtonText("Close");
            uniWebView.EmbeddedToolbar.SetPosition(UniWebViewToolbarPosition.Top);
            uniWebView.Frame = new Rect(0, 0, Screen.width, Screen.height);

            var socialLoginUrl = await AuthService.GetSocialLoginRedirectURLAsync(provider);
            var socialLoginUrlQueries = ParseQueryString(socialLoginUrl);

            if (!socialLoginUrlQueries.ContainsKey("redirect_uri"))
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Redirect URI is not found in the social login URL");
            }

            Debug.Log("Showing WebView");
            uniWebView.Show();
            uniWebView.Load(socialLoginUrl);
            string callbackData = null;

            try 
            {
                callbackData = await tcs.Task;
            } 
            finally 
            {
                uniWebView.Hide();
                uniWebView.OnPageFinished -= OnSocialLoginWebviewFinished;
                uniWebView.OnPageStarted -= OnSocialLoginWebviewStarted;
                uniWebView.OnMultipleWindowClosed -= OnSocialLoginWebviewClosed;
                uniWebView.OnShouldClose -= OnSocialLoginShouldClose;

                Destroy(uniWebView);
            }

            var callbackDataMap = ParseQueryString(callbackData);
            
            callbackDataMap["redirect_uri"] = socialLoginUrlQueries["redirect_uri"];
#endif

            return callbackDataMap;
        }

        private static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var queryParameters = new Dictionary<string, string>();
            queryString = queryString[(queryString.IndexOf('?') + 1)..];
            queryString = queryString.Split('#')[0];

            var pairs = queryString.Split('&');
            foreach (var pair in pairs)
            {
                var splitIndex = pair.IndexOf('=');

                if (splitIndex < 1 || splitIndex == pair.Length - 1)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(pair[..splitIndex]);
                var value = Uri.UnescapeDataString(pair[(splitIndex + 1)..]);
                queryParameters[key] = value;
            }

            foreach (var (key, value) in queryParameters)
            {
                Debug.Log($"{key}: {value}");
            }

            return queryParameters;
        }
    }
   
    internal class OauthRedirectListener
    {
        private readonly HttpListener _listener = new();

        public string Path;
        public int Port;

        public OauthRedirectListener(string path = "")
        {
            Path = path;
            Port = GetRandomUnusedPort();
            _listener.Prefixes.Add($"http://localhost:{Port}/{path.Trim('/')}/");
            
            Debug.Log($"HTTP Server started on port {Port} with path {Path}");
        }
        
        public async UniTask<string> ListenAsync()
        {
            _listener.Start();

            var contextTask = _listener.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(180));
            
            var completedTask = await Task.WhenAny(contextTask, timeoutTask);
            
            if (completedTask != contextTask)
            {
                throw new TimeoutException("Timeout while waiting for the HTTP server to respond");
            }
            
            var request = contextTask.Result.Request;
            var response = contextTask.Result.Response;
            
            if (request.HttpMethod != "GET")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                response.Close();
                
                throw new ArgumentException("Only GET method is allowed");
            }

            var callbackData = request.Url.Query;

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/plain";
            var buffer = System.Text.Encoding.UTF8.GetBytes("Social login completed. You can close this window now.");
            response.ContentLength64 = buffer.Length;
            
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            
            response.Close();
            
            _listener.Stop();

            return callbackData;
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
    
    internal enum AuthType
    {
        SwitchAccount,
        LinkAccount,
    }
}