using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class WelcomeNotificationPresenter : Presenter<NoctuaAuthService>
    {
        private VisualElement _welcomeBox;
        private Label _playerName;
        
        protected override void Attach()
        {
            Model.OnAuthenticated += ModelOnAuthenticated;
        }
        
        protected override void Detach()
        {
            Model.OnAuthenticated -= ModelOnAuthenticated;
        }
        
        private void Awake()
        {
            LoadView();
            
            _welcomeBox = View.Q<VisualElement>("NoctuaWelcomeBox");
            _playerName = View.Q<Label>("NoctuaWelcomePlayerName");
        }

        private void ModelOnAuthenticated(UserBundle userBundle)
        {
            StartCoroutine(Show(userBundle));
        }

        private IEnumerator Show(UserBundle userBundle)
        {
            yield return new WaitForSeconds(1);

            if (userBundle?.Player?.Username != null) {
                // Use player username from in-game if possible
                _playerName.text = userBundle.Player.Username;
            } else if (userBundle?.User?.Nickname != null) {
                // Fallback to user's nickname if the player username is not available
                _playerName.text = userBundle.User.Nickname;
            }
            
            _welcomeBox.AddToClassList("welcome-show");
            
            yield return new WaitForSeconds(3);
            
            _welcomeBox.RemoveFromClassList("welcome-show");
            _welcomeBox.AddToClassList("welcome-hide");
        }
    }
}