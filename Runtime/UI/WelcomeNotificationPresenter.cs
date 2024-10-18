using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class WelcomeNotificationPresenter : Presenter<AuthenticationModel>
    {
        private VisualElement _root;
        private Label _playerName;
        private VisualElement _playerAvatarImage;
        private GlobalConfig _config;
        protected override void Attach()
        {
            Model.OnAccountChanged += OnAccountChanged;
        }

        protected override void Detach()
        {
            Model.OnAccountChanged -= OnAccountChanged;
        }
        
        private void OnAccountChanged(UserBundle userBundle)
        {
            if (userBundle == null) return;
            
            Show(userBundle);
        }

        private void Start()
        {
            View.visible = true;
            _root = View.Q<VisualElement>("WelcomeNotification");
            _playerName = View.Q<Label>("PlayerName");
            _playerAvatarImage = View.Q<VisualElement>("PlayerAvatarImage");
        }

        public void Show(UserBundle userBundle)
        {
            Debug.Log("Welcome " + userBundle.User.Nickname);
            Debug.Log("Provider: " + userBundle.Credential.Provider);
            StartCoroutine(RunAnimation(userBundle));
        }

        public void SetBehaviourWhitelabel(GlobalConfig config)
        {
            _config = config;
        }

        public IEnumerator RunAnimation(UserBundle userBundle)
        {
            if (!string.IsNullOrEmpty(_config.Noctua.Flags) && _config.Noctua.Flags.Contains("VN") && userBundle.IsGuest)
            {
                Model.ShowEmailLogin();
                yield break;
            }

            View.visible = true;
            
            yield return new WaitForSeconds(1);

            _playerName.text = userBundle.DisplayName;
            
            var logoClass = userBundle.Credential.Provider switch
            {
                "google" => "google-player-avatar",
                "facebook" => "facebook-player-avatar",
                "email" => "email-player-avatar",
                _ => "guest-player-avatar"
            };
            
            _playerAvatarImage.ClearClassList();
            _playerAvatarImage.AddToClassList(logoClass);
            _playerAvatarImage.AddToClassList("player-avatar");
            
            _root.RemoveFromClassList("welcome-hide");
            _root.AddToClassList("welcome-show");
            
            yield return new WaitForSeconds(3);
            
            _root.RemoveFromClassList("welcome-show");
            _root.AddToClassList("welcome-hide");
        }
    }
}