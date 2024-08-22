using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public abstract class Presenter<TModel> : MonoBehaviour
    {
        protected TModel Model;
        protected VisualElement View;

        public bool Visible
        {
            get => View.visible;
            set
            {
                View.Focus();
                View.visible = value;
            }
        }

        public void SetModel(TModel model)
        {
            if (Model is not null)
            {
                Detach();
            }

            Model = model;

            if (Model is not null)
            {
                Attach();
            }
        }

        protected abstract void Attach();
        protected abstract void Detach();

        protected void LoadView()
        {
            var viewResourceName = GetType().Name.Replace("Presenter", "");
            View = Resources.Load<VisualTreeAsset>(viewResourceName).CloneTree();
            View.focusable = true;

            gameObject.GetComponent<UIDocument>()?.rootVisualElement.Add(View);
        }
    }
}