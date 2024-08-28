using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class EmailVerificationDialogPresenter : Presenter<NoctuaBehaviour>
    {
        private UIDocument _uiDoc;

        private string _email;
        private string _password;
        private int _verificationId;

        protected override void Attach(){}
        protected override void Detach(){}

        public void Show(string email, string password, int verificationId)
        {
            Debug.Log("EmailVerificationDialogPresenter.Show()");
            View.visible = true;

            _email = email;
            _password = password;
            _verificationId = verificationId;

            SetupView();
        }

        private void Awake()
        {
            LoadView();
            SetupView();
        }

        private void SetupView()
        {
            var code1 = View.Q<TextField>("Code1");
            var code2 = View.Q<TextField>("Code2");
            var code3 = View.Q<TextField>("Code3");
            var code4 = View.Q<TextField>("Code4");
            var code5 = View.Q<TextField>("Code5");
            var code6 = View.Q<TextField>("Code6");

            var resendCode = View.Q<Label>("ResendCode");
            var verifyButton = View.Q<Button>("VerifyButton");

            verifyButton.RegisterCallback<ClickEvent>(OnVerifyButtonClick);
        }

         private void OnVerifyButtonClick(ClickEvent evt)
        {
            
        }
    }
}
