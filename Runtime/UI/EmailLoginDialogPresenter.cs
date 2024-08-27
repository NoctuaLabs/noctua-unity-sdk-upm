using com.noctuagames.sdk.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    public class EmailLoginDialogPresenter : Presenter<AccountSelection>
    {
        protected override void Attach()
        {
            Model.OnLoginWithEmailRequested += OnLoginWithEmailRequested;
        }

        protected override void Detach()
        {
            Model.OnLoginWithEmailRequested -= OnLoginWithEmailRequested;
        }

        private void Awake()
        {
            LoadView();
            SetupInputFields();
        }

        private void SetupInputFields()
        {
            var emailField = View.Q<TextField>("EmailTF");
            var passwordField = View.Q<TextField>("PasswordTF");

            passwordField.isPasswordField = true;

            emailField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(emailField));
            passwordField.RegisterValueChangedCallback(evt => AdjustHideLabelElement(passwordField));

        }

        private void AdjustHideLabelElement(TextField textField) {
            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;

            }
        }

        private void OnLoginWithEmailRequested()
        {
            Visible = true;
        }
    }
}
