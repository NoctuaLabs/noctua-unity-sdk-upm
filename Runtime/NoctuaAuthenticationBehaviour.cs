using System;
using System.Collections;
using System.Collections.Generic;
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
        
        private HttpServer _oauthHttpServer;

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

        public event Action<UserBundle> OnAccountChanged;

        private void Awake()
        {
            gameObject.SetActive(false);
            
            _panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            
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

        public void ShowAccountSelection()
        {
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

        public void ShowLoginOptions(Action<LoginResult> onLoginDone = null)
        {
            _loginOptionsDialog.Show(onLoginDone);
        }

        public void ShowEmailLogin(Action<LoginResult> onLoginDone)
        {
            _emailLoginDialog.Show(onLoginDone);
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

        public void ShowSocialLogin(string provider, Action<LoginResult> onLoginDone)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    var result = await SocialLogin(provider);

                    onLoginDone?.Invoke(
                        new LoginResult
                        {
                            Success = true,
                            User = result
                        }
                    );
                }
                catch (Exception e)
                {
                    onLoginDone?.Invoke(
                        new LoginResult
                        {
                            Success = false,
                            Error = e
                        }
                    );
                }
            });
        }

        public async UniTask<UserBundle> SocialLogin(string provider)
        {
            if (AuthService.RecentAccount == null)
            {
                throw NoctuaException.NoRecentAccount;
            }
            
            Debug.Log("SocialLogin: " + provider);

            var socialLoginUrl = await AuthService.GetSocialLoginRedirectURL(provider);

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

            return await AuthService.SocialLogin(provider, socialLoginRequest);
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

        private void OnDestroy()
        {
            if (_oauthHttpServer is { IsRunning: true })
            {
                _oauthHttpServer.Stop();
            }
            
            _oauthHttpServer = null;
        }

        public void ShowUserCenter()
        {
            _userCenter.Show();
        }
    }
}