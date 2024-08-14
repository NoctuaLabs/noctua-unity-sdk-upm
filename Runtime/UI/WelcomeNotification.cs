using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class WelcomeNotification : MonoBehaviour
    {
        private UIDocument _uiDoc;
        private VisualElement _welcomeBox;
        private Label _playerName;
        private DateTime _startTime;
        private State _state = State.Start;
        
        private void OnEnable()
        {
            var visualTree = Resources.Load<VisualTreeAsset>("WelcomeNotification");
            var panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            var styleSheet = Resources.Load<StyleSheet>("Noctua");
            
            _uiDoc = gameObject.AddComponent<UIDocument>();
            _uiDoc.panelSettings = panelSettings;
            _uiDoc.visualTreeAsset = visualTree;
            _uiDoc.rootVisualElement.styleSheets.Add(styleSheet);
            
            _welcomeBox = _uiDoc.rootVisualElement.Q<VisualElement>("NoctuaWelcomeBox");
            _playerName = _uiDoc.rootVisualElement.Q<Label>("NoctuaWelcomePlayerName");
        }

        public void Update()
        {
            switch (_state)
            {
            case State.Start:
                if (Noctua.Auth.IsAuthenticated)
                {
                    _playerName.text = Noctua.Auth.Player.User.Nickname;
                    _startTime = DateTime.UtcNow;
                    _welcomeBox.AddToClassList("welcome-show");
                    _state = State.Active;
                }

                break;
            case State.Active:
                if (DateTime.UtcNow - _startTime > TimeSpan.FromSeconds(3))
                {
                    _welcomeBox.RemoveFromClassList("welcome-show");
                    _welcomeBox.AddToClassList("welcome-hide");
                    _state = State.End;
                }

                break;
            case State.End:
                break;
            default:
                throw new ArgumentOutOfRangeException();
            }
        }

        private enum State
        {
            Start,
            Active,
            End
        }
    }
}