using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public abstract class Presenter<TModel> : MonoBehaviour
    {
        protected TModel Model;
        protected VisualElement View;

        private GameObject _uiGameObject;
        private UIDocument _uiDoc;

        public virtual bool Visible
        {
            get => View.visible;
            set
            {
                View.visible = value;

                if (value) View.Focus();
            }
        }

        public void Init(TModel model, PanelSettings panelSettings)
        {
            LoadView(panelSettings);
            SetModel(model);
        }

        private void SetModel(TModel model)
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

        protected virtual void Attach()
        {
        }

        protected virtual void Detach()
        {
        }

        private void LoadView(PanelSettings panelSettings)
        {
            var viewResourceName = GetType().Name.Replace("Presenter", "");

            _uiGameObject = new GameObject(viewResourceName);
            _uiGameObject.transform.SetParent(gameObject.transform);
            _uiDoc = _uiGameObject.AddComponent<UIDocument>();

            var visualTreeAsset = Resources.Load<VisualTreeAsset>(viewResourceName);

            if (visualTreeAsset is null)
            {
                Debug.LogError($"View not found for {viewResourceName}");
            }

            _uiDoc.visualTreeAsset = visualTreeAsset ?? throw new ArgumentNullException(nameof(visualTreeAsset));
            View = _uiDoc.rootVisualElement;
            View.focusable = true;
            View.visible = false;
            
            _uiDoc.panelSettings = panelSettings ?? throw new ArgumentNullException(nameof(panelSettings));
        }
    }
}