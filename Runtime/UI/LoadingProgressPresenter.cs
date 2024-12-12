using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class LoadingProgressPresenter : Presenter<object>
    {
        private VisualElement _loadingProgress;
        protected override void Attach()
        {}

        protected override void Detach()
        {}

        public void Show(bool isShow)
        {
            if (_loadingProgress == null)
            {
                _loadingProgress = View.Q<VisualElement>("LoadingProgress");
            }

            if (isShow)
            {
                if (_loadingProgress.childCount == 0)
                {                    
                    _loadingProgress.Add(new Spinner());
                }
            }
            else
            {
                _loadingProgress.Clear();
            }

            Visible = isShow;
        }

    }
}
