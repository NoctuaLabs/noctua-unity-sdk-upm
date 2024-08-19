using System.Collections;
using System.Diagnostics.Tracing;
using System.Linq;
using com.noctuagames.sdk.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    public class NoctuaBehaviour : MonoBehaviour
    {
        private PanelSettings _panelSettings;
        private UIDocument _uiDocument;
        
        private AccountSelectionDialogPresenter _accountSelectionDialog;

        private void Awake()
        {
            Noctua.Init();
            _panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            _uiDocument = gameObject.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;
            _uiDocument.visualTreeAsset = ScriptableObject.CreateInstance<VisualTreeAsset>();
            _uiDocument.rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>("Noctua"));
            _uiDocument.rootVisualElement.focusable = true;
            _uiDocument.rootVisualElement.Focus();
            
            var welcome = gameObject.AddComponent<WelcomeNotificationPresenter>();
            welcome.SetModel(Noctua.Auth);
            
            _accountSelectionDialog = gameObject.AddComponent<AccountSelectionDialogPresenter>();
            _accountSelectionDialog.SetModel(Noctua.Auth);
            
            _uiDocument.rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            Debug.Log($"Key pressed: {evt.keyCode}");
            
            if (evt.keyCode == KeyCode.Space)
            {
                _accountSelectionDialog.Visible = !_accountSelectionDialog.Visible;
            }
            
        }

        private void Start()
        {
            StartCoroutine(StartNoctua());
        }

        private IEnumerator StartNoctua()
        {
            yield return Noctua.Auth.Authenticate();
        }
    }
}