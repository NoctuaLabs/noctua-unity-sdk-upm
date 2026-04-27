using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter that manages a full-screen loading spinner overlay, shown during async operations.
    /// </summary>
    internal class LoadingProgressPresenter : Presenter<object>
    {
        private VisualElement _loadingProgress;
        protected override void Attach()
        {}

        protected override void Detach()
        {}

        /// <summary>
        /// Shows or hides the loading spinner overlay.
        /// </summary>
        /// <param name="isShow"><c>true</c> to display the spinner; <c>false</c> to hide and clear it.</param>
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
