using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class WelcomeNotificationPresenter : Presenter<NoctuaBehaviour>
    {
        private VisualElement _welcomeBox;
        private Label _playerName;
        
        protected override void Attach()
        {
        }
        
        protected override void Detach()
        {
        }
        
        private void Awake()
        {
            LoadView();

            View.visible = true;
            _welcomeBox = View.Q<VisualElement>("NoctuaWelcomeBox");
            _playerName = View.Q<Label>("NoctuaWelcomePlayerName");
        }

        public void Show(UserBundle userBundle)
        {
            StartCoroutine(ShowWelcomeToast(userBundle));
        }

        private IEnumerator ShowWelcomeToast(UserBundle userBundle)
        {
            View.visible = true;
            
            yield return new WaitForSeconds(1);

            if (userBundle?.Player?.Username != null && userBundle?.Player?.Username.Length > 0) {
                // Use player username from in-game if possible
                _playerName.text = userBundle?.Player?.Username;
            } else if (userBundle?.User?.Nickname != null && userBundle?.User?.Nickname.Length > 0) {
                // Fallback to user's nickname if the player username is not available
                _playerName.text = userBundle?.User?.Nickname;
            } else if (userBundle?.Credential?.Provider == "device_id") {
                // Fallback to prefix guest
                _playerName.text = "Guest " + userBundle?.User?.Id.ToString();
            } else {
                // Fallback to prefix user
                _playerName.text = "User " + userBundle?.User?.Id.ToString();
            }
            
            _welcomeBox.RemoveFromClassList("welcome-hide");
            _welcomeBox.AddToClassList("welcome-show");
            
            yield return new WaitForSeconds(3);
            
            _welcomeBox.RemoveFromClassList("welcome-show");
            _welcomeBox.AddToClassList("welcome-hide");
        }
    }
}