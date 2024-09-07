using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using com.noctuagames.sdk.UI;
using Cysharp.Threading.Tasks;
using UnityEditor;
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
            Debug.Log("SocialLogin: " + provider);

            var socialLoginUrl = await AuthService.GetSocialLoginRedirectURLAsync(provider);

            Debug.Log("SocialLogin: " + provider + " " + socialLoginUrl);

#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL

            // Start HTTP server to listen to the callback with random port
            // open the browser with the redirect URL

            var oauthRedirectListener = new OauthRedirectListener();

            var redirectUrl = $"http://localhost:{oauthRedirectListener.Port}";
            var url = $"{socialLoginUrl}&redirect_uri={HttpUtility.UrlEncode(redirectUrl)}";
            Debug.Log($"Open URL with system browser: {url}");

            Application.OpenURL(url);
            
            var callbackData = await oauthRedirectListener.ListenAsync();
            var callbackDataMap = ParseQueryString(callbackData);

#elif UNITY_IOS || UNITY_ANDROID
            var task = new UniTaskCompletionSource<Dictionary<string, string>>();
            
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
            
            callbackDataMap["redirect_uri"] = redirectUrl;
            
            return callbackDataMap;
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