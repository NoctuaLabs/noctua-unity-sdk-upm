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

    NoctuaAuthenticationBehavour purposes:
    1. To allow our SDK instance (including UI) to be injected into the Scene
    3. To allow an UI presenter call another UI presenter
    2. To allow model layer (logic) to call an UI presenter
    */

    internal class NoctuaAuthenticationBehaviour : MonoBehaviour
    {
        private PanelSettings _panelSettings;
        private UIDocument _uiDocument;
        
        // IMPORTANT NOTES!!!
        // Your UI need to apply USS absolute property to the first VisualElement of the UI
        // before being added to the UI Document.
        // Violation of this rule will cause the UI (and the other UI too) to be unable to be displayed properly.
        private AccountSelectionDialogPresenter _accountSelectionDialog;
        private SwitchAccountConfirmationDialogPresenter _switchAccountConfirmationDialog;
        private LoginOptionsDialogPresenter _loginOptionsDialog;
        private EmailLoginDialogPresenter _emailLoginDialog;
        private EmailRegisterDialogPresenter _emailRegisterDialog;
        private EmailVerificationDialogPresenter _emailVerificationDialog;
        private WelcomeNotificationPresenter _welcome;
        private EmailResetPasswordDialogPresenter _emailResetPasswordDialog;
        private EmailConfirmResetPasswordDialogPresenter _emailConfirmResetPasswordDialog;
        private UserCenterPresenter _userCenter;
        private GeneralNotificationPresenter _generalNotification;

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

        private void Awake()
        {
            gameObject.SetActive(false);
            
            _panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            _panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("NoctuaTheme");
            
            _userCenter = gameObject.AddComponent<UserCenterPresenter>();
            _userCenter.Init(this, _panelSettings);

            _accountSelectionDialog = gameObject.AddComponent<AccountSelectionDialogPresenter>();
            _accountSelectionDialog.Init(this, _panelSettings);

            _switchAccountConfirmationDialog = gameObject.AddComponent<SwitchAccountConfirmationDialogPresenter>();
            _switchAccountConfirmationDialog.Init(this, _panelSettings);

            _loginOptionsDialog = gameObject.AddComponent<LoginOptionsDialogPresenter>();
            _loginOptionsDialog.Init(this, _panelSettings);

            _emailLoginDialog = gameObject.AddComponent<EmailLoginDialogPresenter>();
            _emailLoginDialog.Init(this, _panelSettings);

            _emailVerificationDialog = gameObject.AddComponent<EmailVerificationDialogPresenter>();
            _emailVerificationDialog.Init(this, _panelSettings);

            _emailRegisterDialog = gameObject.AddComponent<EmailRegisterDialogPresenter>();
            _emailRegisterDialog.Init(this, _panelSettings);

            _emailResetPasswordDialog = gameObject.AddComponent<EmailResetPasswordDialogPresenter>();
            _emailResetPasswordDialog.Init(this, _panelSettings);

            _emailConfirmResetPasswordDialog = gameObject.AddComponent<EmailConfirmResetPasswordDialogPresenter>();
            _emailConfirmResetPasswordDialog.Init(this, _panelSettings);
            
             _generalNotification = gameObject.AddComponent<GeneralNotificationPresenter>();
            _generalNotification.Init(this, _panelSettings);

            _welcome = gameObject.AddComponent<WelcomeNotificationPresenter>();
            _welcome.Init(this, _panelSettings);
            
            gameObject.SetActive(true);

            // IMPORTANT NOTES!!!
            // Your UI need to apply USS absolute property to the first 
            // VisualElement of the UI before being added to the UI Document.
            // Violation of this rule will cause the UI (and the other UI too)
            // to be unable to be displayed properly.
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
                uniWebView.SetUserAgent("Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Mobile Safari/537.3");
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                uniWebView.SetUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 14_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1");
            }
            
            var tcs = new UniTaskCompletionSource<string>();
            
            void OnSocialLoginWebviewStarted(UniWebView webView, string url)
            {
                Debug.Log("URL started to load: " + url);

                if (url.Contains($"api/v1/auth/{provider}/code")) {
                    webView.Hide();
                    tcs.TrySetResult(url);
                }
                else if (url.Contains("error")) {
                    webView.Hide();
                    tcs.TrySetException(new NoctuaException(NoctuaErrorCode.Authentication, $"{provider} login failed"));
                }
            }

            void OnSocialLoginWebviewFinished(UniWebView webView, int statusCode, string url)
            {
                Debug.Log("URL finished to load: " + url);
                
                if (url.Contains($"api/v1/auth/{provider}/code")) {
                    webView.Hide();
                    tcs.TrySetResult(url);
                }
                else if (url.Contains("error")) {
                    webView.Hide();                
                    tcs.TrySetException(new NoctuaException(NoctuaErrorCode.Authentication, $"{provider} login failed"));
                }
            }        

            uniWebView.OnPageFinished += OnSocialLoginWebviewFinished;
            uniWebView.OnPageStarted += OnSocialLoginWebviewStarted;

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

            var callbackData = await tcs.Task;

            uniWebView.OnPageFinished -= OnSocialLoginWebviewFinished;
            uniWebView.OnPageStarted -= OnSocialLoginWebviewStarted;

            Destroy(uniWebView);

            var callbackDataMap = ParseQueryString(callbackData);
            
            callbackDataMap["redirect_uri"] = socialLoginUrlQueries["redirect_uri"];
#endif

            return callbackDataMap;
        }

        private static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var queryParameters = new Dictionary<string, string>();
            queryString = queryString[(queryString.IndexOf('?') + 1)..];

            Debug.Log("Query string: " + queryString);

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