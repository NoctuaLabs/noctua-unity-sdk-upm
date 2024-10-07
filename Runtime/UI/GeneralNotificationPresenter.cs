using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class GeneralNotificationPresenter : Presenter<object>
    {
        private VisualElement _root;
        private VisualElement _notifIconBox;
        private Label _messageName;
        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            View.visible = true;
            _root = View.Q<VisualElement>("GeneralNotification");
            _notifIconBox = View.Q<VisualElement>("NotifIconBox");
            _messageName = View.Q<Label>("MessageName");
        }

        public void Show(string textMessage, bool isNotifSuccess)
        {
            StartCoroutine(RunAnimation(textMessage, isNotifSuccess));
        }

        public IEnumerator RunAnimation(string textMessage, bool isNotifSuccess)
        {
            if(isNotifSuccess)
            {
                Color borderColor = new Color(6f / 255f, 208f / 255f, 1f / 255f);

                _root.style.borderTopColor = new StyleColor(borderColor);
                _root.style.borderRightColor = new StyleColor(borderColor);
                _root.style.borderBottomColor = new StyleColor(borderColor);
                _root.style.borderLeftColor = new StyleColor(borderColor);

                _notifIconBox.style.display = DisplayStyle.None;
                _messageName.style.color = borderColor;
            }

            View.visible = true;
            
            yield return new WaitForSeconds(1);

            _messageName.text = textMessage;
            
            _root.RemoveFromClassList("welcome-hide");
            _root.AddToClassList("welcome-show");
            
            yield return new WaitForSeconds(3);
            
            _root.RemoveFromClassList("welcome-show");
            _root.AddToClassList("welcome-hide");
        }
    }
}
