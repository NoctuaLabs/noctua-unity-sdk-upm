using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    public class EditProfileDialogPresenter : MonoBehaviour
    {
        private UIDocument _uiDoc;


        private void Start()
        {
            var visualTree = Resources.Load<VisualTreeAsset>("EditProfile");
            var panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            var styleSheet = Resources.Load<StyleSheet>("Noctua");
            var styleSheet1 = Resources.Load<StyleSheet>("UserCenter");
            var styleSheet2 = Resources.Load<StyleSheet>("EditProfile");
            
            _uiDoc = gameObject.AddComponent<UIDocument>();
            _uiDoc.panelSettings = panelSettings;
            _uiDoc.visualTreeAsset = visualTree;
            _uiDoc.rootVisualElement.styleSheets.Add(styleSheet);
            _uiDoc.rootVisualElement.styleSheets.Add(styleSheet1);
            _uiDoc.rootVisualElement.styleSheets.Add(styleSheet2);

            SetupInputFields();
        }

        private void SetupInputFields()
        {
            var nicknameField = _uiDoc.rootVisualElement.Q<TextField>("NicknameTF");
            var bithdateField = _uiDoc.rootVisualElement.Q<TextField>("BirthdateTF");
            var genderField = _uiDoc.rootVisualElement.Q<TextField>("GenderTF");
            var countryField = _uiDoc.rootVisualElement.Q<TextField>("CountryTF");
            var languangeField = _uiDoc.rootVisualElement.Q<TextField>("LanguageTF");
            var currencyField = _uiDoc.rootVisualElement.Q<TextField>("CurrencyTF");

            var changePictureButton = _uiDoc.rootVisualElement.Q<Button>("ChangePictureButton");
            var saveButton = _uiDoc.rootVisualElement.Q<Button>("SaveButton");

            nicknameField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(nicknameField));
            bithdateField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(bithdateField));
            genderField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(genderField));
            countryField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(countryField));
            languangeField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(languangeField));
            currencyField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(currencyField));

            changePictureButton.RegisterCallback<ClickEvent>(OnChangePictureButtonClick);
            saveButton.RegisterCallback<ClickEvent>(OnSaveButtonClick);

        }

        private void OnChangePictureButtonClick(ClickEvent evt)
        {
            
            
        }

        private void OnSaveButtonClick(ClickEvent evt)
        {
            
            
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
