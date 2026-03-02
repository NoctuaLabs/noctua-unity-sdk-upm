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

        public void Show(string textMessage, bool isNotifSuccess, uint durationMs = 3000)
        {
            StartCoroutine(RunAnimation(textMessage, isNotifSuccess, durationMs));
        }

        public void Show(LocaleTextKey textKey, bool isNotifSuccess, uint durationMs = 3000)
        {
            StartCoroutine(RunAnimation(Locale.GetTranslation(textKey), isNotifSuccess, durationMs));
        }

        public IEnumerator RunAnimation(string textMessage, bool isNotifSuccess, uint durationMs)
        {
            Color color = isNotifSuccess ? ColorModule.greenSuccess : ColorModule.redFailed;

            _notifIconBox.style.display = DisplayStyle.None;
            _messageName.style.color = color;

            View.visible = true;
            
            yield return new WaitForSeconds(1);

            _messageName.text = textMessage;
            
            _root.AddToClassList("expanded");
            
            yield return new WaitForSeconds(durationMs / 1000.0f);
            
            _root.RemoveFromClassList("expanded");
        }
    }
}
