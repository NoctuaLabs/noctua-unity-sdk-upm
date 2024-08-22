using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    public class LoginDialogPresenter : MonoBehaviour
    {
        private UIDocument _uiDoc;

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>("LoginDialog");
            var panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            var styleSheet = Resources.Load<StyleSheet>("Noctua");
            
            _uiDoc = gameObject.AddComponent<UIDocument>();
            _uiDoc.panelSettings = panelSettings;
            _uiDoc.visualTreeAsset = visualTree;
            _uiDoc.rootVisualElement.styleSheets.Add(styleSheet);

            SetupInputFields();
        }

        private void SetupInputFields()
        {
            var emailField = _uiDoc.rootVisualElement.Q<TextField>("EmailTF");
            var passwordField = _uiDoc.rootVisualElement.Q<TextField>("PasswordTF");

            passwordField.isPasswordField = true;

            emailField.RegisterValueChangedCallback(evt => AdjustTopPositionLabel(emailField));
            passwordField.RegisterValueChangedCallback(evt => AdjustTopPositionLabel(passwordField));

        }

        private void AdjustTopPositionLabel(TextField textField) {
            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;

            }
        } 
    }
}
