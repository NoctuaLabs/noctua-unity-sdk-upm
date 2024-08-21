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

        private void ModelOnAuthenticated(Player player)
        {
            StartCoroutine(Show(player));
        }

        private IEnumerator Show(Player player)
        {
            yield return new WaitForSeconds(1);
            
            _playerName.text = player.User.Nickname;
            _welcomeBox.AddToClassList("welcome-show");
            
            yield return new WaitForSeconds(3);
            
            _welcomeBox.RemoveFromClassList("welcome-show");
            _welcomeBox.AddToClassList("welcome-hide");
        }
    }
}