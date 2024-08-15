using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class WelcomeNotificationPresenter : MonoBehaviour
    {
        private TemplateContainer _viewRoot;
        private VisualElement _welcomeBox;
        private Label _playerName;

        private void Awake()
        {
            _viewRoot = NoctuaUI.CreateUIFromResource("WelcomeNotification", "Noctua");
            gameObject.GetComponent<UIDocument>().rootVisualElement.Add(_viewRoot);
            
            _welcomeBox = _viewRoot.Q<VisualElement>("NoctuaWelcomeBox");
            _playerName = _viewRoot.Q<Label>("NoctuaWelcomePlayerName");
        }

        public IEnumerator Show()
        {
            while (!Noctua.Auth.IsAuthenticated)
            {
                yield return null;
            }
            
            yield return new WaitForSeconds(1);
            
            _playerName.text = Noctua.Auth.Player.User.Nickname;
            _welcomeBox.AddToClassList("welcome-show");
            
            yield return new WaitForSeconds(3);
            
            _welcomeBox.RemoveFromClassList("welcome-show");
            _welcomeBox.AddToClassList("welcome-hide");
        }
    }
}