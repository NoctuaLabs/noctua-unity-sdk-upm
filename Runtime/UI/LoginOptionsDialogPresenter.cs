using System;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class LoginOptionsDialogPresenter : Presenter<AccountSelection>
    {
        private Button _loginWithGoogleButton;
        private Button _loginWithFacebookButton;
        private Button _loginWithEmailButton;
        private Button _registerButton;
        private Button _backButton;
        
        protected override void Attach()
        {
            Model.OnLoginOptionsRequested += OnLoginOptionsRequested;
        }

        protected override void Detach()
        {
            Model.OnLoginOptionsRequested -= OnLoginOptionsRequested;
        }

        private void Awake()
        {
            LoadView();
            
            _loginWithGoogleButton = View.Q<Button>("LoginWithGoogleButton");
            _loginWithGoogleButton.clicked += OnLoginWithGoogleButtonClicked;
            
            _loginWithFacebookButton = View.Q<Button>("LoginWithFacebookButton");
            _loginWithFacebookButton.clicked += OnLoginWithFacebookButtonClicked;
            
            _loginWithEmailButton = View.Q<Button>("LoginWithEmailButton");
            _loginWithEmailButton.clicked += OnLoginWithEmailButtonClicked;
            
            _registerButton = View.Q<Button>("RegisterButton");
            _registerButton.clicked += OnRegisterButtonClicked;
            
            _backButton = View.Q<Button>("BackButton");
            _backButton.clicked += OnBackButtonClicked;
        }

        private void OnLoginWithFacebookButtonClicked()
        {
            Visible = false;
            UniTask.Void(async () =>
            {
                await Model.AuthService.SocialLogin("facebook");
            });
        }

        private void OnLoginWithGoogleButtonClicked()
        {
            Visible = false;
            UniTask.Void(async () =>
            {
                await Model.AuthService.SocialLogin("google");
            });
        }

        private void OnLoginWithEmailButtonClicked()
        {
            Visible = false;
            Model.RequestLoginWithEmail();
        }

        private void OnLoginOptionsRequested()
        {
            Visible = true;
        }
        
        private void OnRegisterButtonClicked()
        {
            Visible = false;
        }

        private void OnBackButtonClicked()
        {
            Visible = false;
            Model.RequestAccountSelection();
        }
    }
}