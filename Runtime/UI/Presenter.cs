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
                View.visible = value;
                
                if (value) View.Focus();
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
            Debug.Log("LoadView " + GetType().Name);
            var viewResourceName = GetType().Name.Replace("Presenter", "");
            Debug.Log("LoadView resource name " + viewResourceName);
            var view = Resources.Load<VisualTreeAsset>(viewResourceName).CloneTree();
            if (view is null)
            { 
                Debug.LogError($"View not found for {viewResourceName}");
            }

            View = view ?? throw new System.Exception($"View not found for {viewResourceName}");
            View.focusable = true;
            View.visible = false;

            var uiDoc = gameObject.GetComponent<UIDocument>();

            if (uiDoc is null)
            {
                Debug.Log("UIDocument component not found on the GameObject " + GetType().Name);
                throw new System.Exception("UIDocument component not found on the GameObject");
            }
            
            gameObject.GetComponent<UIDocument>().rootVisualElement.Add(View);
        }
    }
}