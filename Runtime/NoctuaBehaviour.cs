using System.Collections;
using com.noctuagames.sdk.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    public class NoctuaBehaviour : MonoBehaviour
    {
        public string Action;
        private PanelSettings _panelSettings;
        private UIDocument _uiDocument;
        
        public AccountSelectionDialogPresenter _accountSelectionDialog;
        public RegisterDialogPresenter _registerDialog;

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
            
            var welcome = gameObject.AddComponent<WelcomeNotificationPresenter>();
            welcome.SetModel(Noctua.Auth);
            
            _accountSelectionDialog = gameObject.AddComponent<AccountSelectionDialogPresenter>();
            _accountSelectionDialog.SetModel(Noctua.Auth);
            _accountSelectionDialog.Visible = false;

            _registerDialog = gameObject.AddComponent<RegisterDialogPresenter>();
            _registerDialog.SetModel(Noctua.Auth);
            _registerDialog.Visible = false;
        }

        public void SetAccountSelectionDialogVisibility(bool show)
        {
            // Disable all dialogs first
            _registerDialog.Visible = false;

            _accountSelectionDialog.Visible = show;
        }

        public void ShowRegisterDialogUI()
        {
            // Disable all dialogs first
            _accountSelectionDialog.Visible = false;

            _registerDialog.Show();
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