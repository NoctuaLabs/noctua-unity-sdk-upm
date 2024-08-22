using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    public class RegisterDialogPresenter : MonoBehaviour
    {
        private UIDocument _uiDoc;

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>("RegisterDialog");
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
            var rePasswordField = _uiDoc.rootVisualElement.Q<TextField>("RePasswordTF");

            passwordField.isPasswordField = true;
            rePasswordField.isPasswordField = true;

            emailField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(emailField));
            passwordField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(passwordField));
            rePasswordField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(rePasswordField));

        }

        private void AdjustHideLabelElement(TextField textField) {
            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;

            }
        } 
    }
}
