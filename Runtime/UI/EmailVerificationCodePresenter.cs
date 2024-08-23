using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    public class EmailVerificationCodePresenter : MonoBehaviour
    {
        private UIDocument _uiDoc;

        private void Awake()
        {
            var visualTree = Resources.Load<VisualTreeAsset>("EmailVerificationCodeDialog");
            var panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            var styleSheet = Resources.Load<StyleSheet>("Noctua");
            
            _uiDoc = gameObject.AddComponent<UIDocument>();
            _uiDoc.panelSettings = panelSettings;
            _uiDoc.visualTreeAsset = visualTree;
            _uiDoc.rootVisualElement.styleSheets.Add(styleSheet);

            SetupView();
        }

        private void SetupView()
        {
            var code1 = _uiDoc.rootVisualElement.Q<TextField>("Code1");
            var code2 = _uiDoc.rootVisualElement.Q<TextField>("Code2");
            var code3 = _uiDoc.rootVisualElement.Q<TextField>("Code3");
            var code4 = _uiDoc.rootVisualElement.Q<TextField>("Code4");
            var code5 = _uiDoc.rootVisualElement.Q<TextField>("Code5");
            var code6 = _uiDoc.rootVisualElement.Q<TextField>("Code6");

            var resendCode = _uiDoc.rootVisualElement.Q<Label>("ResendCode");
            var verifyButton = _uiDoc.rootVisualElement.Q<Button>("VerifyButton");

            verifyButton.RegisterCallback<ClickEvent>(OnVerifyButtonClick);

        }

         private void OnVerifyButtonClick(ClickEvent evt)
        {
            
            
        }
    }
}
