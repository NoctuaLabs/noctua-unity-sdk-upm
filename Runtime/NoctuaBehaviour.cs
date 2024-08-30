using System;
using System.Collections;
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

    NoctuaBehaviour purposes:
    1. To allow our SDK instance (including UI) to be injected into the Scene
    3. To allow an UI presenter call another UI presenter
    2. To allow model layer (logic) to call an UI presenter
    */

    public class NoctuaBehaviour : MonoBehaviour
    {
        public string Action;
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

        public NoctuaAuthService AuthService => Noctua.Auth;

        public event Action<UserBundle> OnAccountChanged
        {
            add => AuthService.OnAccountChanged += value;
            remove => AuthService.OnAccountChanged -= value;
        }

        private void Awake()
        {
            Noctua.Init();

            _panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            _uiDocument = gameObject.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;
            _uiDocument.visualTreeAsset = ScriptableObject.CreateInstance<VisualTreeAsset>();
            _uiDocument.rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>("Root"));
            _uiDocument.rootVisualElement.AddToClassList("root");
            _uiDocument.rootVisualElement.focusable = true;
            _uiDocument.rootVisualElement.Focus();

            _welcome = gameObject.AddComponent<WelcomeNotificationPresenter>();
            _welcome.SetModel(this);

            _accountSelectionDialog = gameObject.AddComponent<AccountSelectionDialogPresenter>();
            _accountSelectionDialog.SetModel(this);

            _switchAccountConfirmationDialog = gameObject.AddComponent<SwitchAccountConfirmationDialogPresenter>();
            _switchAccountConfirmationDialog.SetModel(this);

            _loginOptionsDialog = gameObject.AddComponent<LoginOptionsDialogPresenter>();
            _loginOptionsDialog.SetModel(this);

            _emailLoginDialog = gameObject.AddComponent<EmailLoginDialogPresenter>();
            _emailLoginDialog.SetModel(this);

            _emailVerificationDialog = gameObject.AddComponent<EmailVerificationDialogPresenter>();
            _emailVerificationDialog.SetModel(this);

            _emailRegisterDialog = gameObject.AddComponent<EmailRegisterDialogPresenter>();
            _emailRegisterDialog.SetModel(this);

            _emailResetPasswordDialog = gameObject.AddComponent<EmailResetPasswordDialogPresenter>();
            _emailResetPasswordDialog.SetModel(this);

            _emailConfirmResetPasswordDialog = gameObject.AddComponent<EmailConfirmResetPasswordDialogPresenter>();
            _emailConfirmResetPasswordDialog.SetModel(this);
            
            _userCenter = gameObject.AddComponent<UserCenterPresenter>();
            _userCenter.SetModel(this);

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

        public void ShowLoginOptions(UserBundle recentAccount)
        {
            _loginOptionsDialog.Show(recentAccount);
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

        public void ShowWelcomeToast(UserBundle recentAccount)
        {
            _welcome.Show(recentAccount);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _userCenter.Visible = !_userCenter.Visible;
            }
        }

        public void ShowSocialLogin(string provider, Action<LoginResult> onLoginDone)
        {
            UniTask.Void(async () => await SocialLogin(provider, onLoginDone));
        }

        private async UniTask SocialLogin(string provider, Action<LoginResult> onLoginDone)
        {
            try
            {
                var result = await AuthService.SocialLogin(provider);

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
        }
    }
}