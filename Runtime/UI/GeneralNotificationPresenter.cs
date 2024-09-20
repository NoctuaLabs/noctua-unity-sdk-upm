using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class GeneralNotificationPresenter : Presenter<AuthenticationModel>
    {
        private VisualElement _root;
        private Label _messageName;
        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            View.visible = true;
            _root = View.Q<VisualElement>("GeneralNotification");
            _messageName = View.Q<Label>("MessageName");
        }

        public void Show(string textMessage)
        {
            StartCoroutine(RunAnimation(textMessage));
        }

        public IEnumerator RunAnimation(string textMessage)
        {
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
