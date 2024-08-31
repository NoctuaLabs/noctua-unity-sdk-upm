using System;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class LoginOptionsDialogPresenter : Presenter<NoctuaAuthenticationBehaviour>
    {
        private Button _loginWithGoogleButton;
        private Button _loginWithFacebookButton;
        private Button _loginWithEmailButton;
        private Button _registerButton;
        private Button _backButton;
        private Action<LoginResult> _onDone; 
        
        public void Show(Action<LoginResult> onDone)
        {
            _onDone = onDone;
            Visible = true;
        }

        protected override void Attach(){}
        protected override void Detach(){}

        private void Awake()
        {
            LoadView();
            
            _loginWithGoogleButton = View.Q<Button>("LoginWithGoogleButton");
            _loginWithGoogleButton.RegisterCallback<PointerUpEvent>(_ => OnLoginWithGoogleButtonClicked());
            
            _loginWithFacebookButton = View.Q<Button>("LoginWithFacebookButton");
            _loginWithFacebookButton.RegisterCallback<PointerUpEvent>(_ => OnLoginWithFacebookButtonClicked());
            
            _loginWithEmailButton = View.Q<Button>("LoginWithEmailButton");
            _loginWithEmailButton.RegisterCallback<PointerUpEvent>(_ => OnLoginWithEmailButtonClicked());
            
            _registerButton = View.Q<Button>("RegisterButton");
            _registerButton.RegisterCallback<PointerUpEvent>(_ => OnRegisterButtonClicked());
            
            _backButton = View.Q<Button>("BackButton");
            _backButton.RegisterCallback<PointerUpEvent>(_ => OnBackButtonClicked());
        }

        private void OnLoginWithFacebookButtonClicked()
        {
            Visible = false;
            StartCoroutine(SocialLogin("facebook").ToCoroutine());
        }

        private void OnLoginWithGoogleButtonClicked()
        {
            Visible = false;
            StartCoroutine(SocialLogin("google").ToCoroutine());
        }

        private async UniTask SocialLogin(string provider)
        {
            try
            {
                var userBundle = await Model.SocialLogin("google");

                _onDone?.Invoke(
                    new LoginResult
                    {
                        Success = true,
                        User = userBundle,
                    }
                );
            }
            catch (Exception e)
            {
                _onDone?.Invoke(
                    new LoginResult
                    {
                        Success = false,
                        Error = e,
                    }
                );
            }
        }

        private void OnLoginWithEmailButtonClicked()
        {
            Visible = false;
            
            Model.ShowEmailLogin(
                result =>
                {
                    if (!result.Success)
                    {
                        Visible = true;
                    }
                }
            );
        }
        
        private void OnRegisterButtonClicked()
        {
            Visible = false;
            
            Model.ShowEmailRegistration(true);
        }

        private void OnBackButtonClicked()
        {
            Visible = false;

            Model.ShowAccountSelection();
        }
    }
}