using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter for temporary toast-style notifications that slide in and auto-dismiss after a configurable duration.
    /// </summary>
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

        /// <summary>
        /// Displays a toast notification with the specified message text.
        /// </summary>
        /// <param name="textMessage">The message to display.</param>
        /// <param name="isNotifSuccess"><c>true</c> for green success styling; <c>false</c> for red error styling.</param>
        /// <param name="durationMs">Duration in milliseconds before the notification auto-dismisses.</param>
        public void Show(string textMessage, bool isNotifSuccess, uint durationMs = 3000)
        {
            StartCoroutine(RunAnimation(textMessage, isNotifSuccess, durationMs));
        }

        /// <summary>
        /// Displays a toast notification using a localized text key.
        /// </summary>
        /// <param name="textKey">The locale text key to translate and display.</param>
        /// <param name="isNotifSuccess"><c>true</c> for green success styling; <c>false</c> for red error styling.</param>
        /// <param name="durationMs">Duration in milliseconds before the notification auto-dismisses.</param>
        public void Show(LocaleTextKey textKey, bool isNotifSuccess, uint durationMs = 3000)
        {
            StartCoroutine(RunAnimation(Locale.GetTranslation(textKey), isNotifSuccess, durationMs));
        }

        /// <summary>
        /// Coroutine that animates the notification slide-in, displays the message, and slides out after the specified duration.
        /// </summary>
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
