using System.Collections;
using com.noctuagames.sdk.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    /*
    Actually, we were using UI-Presenter-Model pattern where the presenter being the place of the main logics.
    
    But in our case, we have unique conditions:
    1. Our model (*Services.cs) is the public facing API
    2. Our public facing API need to cover both UI and non-UI stuff.

    Thus, we have to tweak the pattern to UI-Presenter-Model-API
    1. UI:
    1. Presenter: where we control the state of the UI, but not the main logics
    2. Model: main logics + public facing API
    3. API: the actual model, where we talk to either HTTP API or local storage (like player prefs)

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
        
        private AccountSelectionDialogPresenter _accountSelectionDialog;
        private SwitchAccountConfirmationDialogPresenter _switchAccountConfirmationDialog;
        private LoginOptionsDialogPresenter _loginOptionsDialog;
        private EmailLoginDialogPresenter _emailLoginDialog;
        private RegisterDialogPresenter _registerDialog;
        private EmailVerificationDialogPresenter _emailVerificationDialog;
        private WelcomeNotificationPresenter _welcome;

        public NoctuaAuthService AuthService => Noctua.Auth;

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

            _registerDialog = gameObject.AddComponent<RegisterDialogPresenter>();
            _registerDialog.SetModel(this);
        }

        public void ShowAccountSelectionDialogUI()
        {
            _accountSelectionDialog.Show();
        }

        public void ShowRegisterDialogUI()
        {
            // Disable all dialogs first
            _accountSelectionDialog.Visible = false;

            _registerDialog.Show();
        }

        public void ShowEmailVerificationDialogUI(string email, string password, int verificationID)
        {
            _emailVerificationDialog.Show(email, password, verificationID);
        }

        public void ShowLoginOptionsDialogUI(UserBundle recentAccount){
            _loginOptionsDialog.Show(recentAccount);
        }

        public void ShowLoginWithEmailDialogUI()
        {
            _emailLoginDialog.Show();
        }

        public void ShowSwitchAccountConfirmationDialogUI(UserBundle recentAccount)
        {
            _switchAccountConfirmationDialog.Show(recentAccount);
        }

        public void ShowWelcomeToast(UserBundle recentAccount){
            _welcome.Show(recentAccount);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _accountSelectionDialog.Visible = !_accountSelectionDialog.Visible;
            }
        }
    }
}