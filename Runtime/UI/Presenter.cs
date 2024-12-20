﻿using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public abstract class Presenter<TModel> : MonoBehaviour
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(Presenter<TModel>));
        protected TModel Model;
        protected VisualElement View;
        protected NoctuaLocale Locale;

        private UIDocument _uiDoc;

        public virtual bool Visible
        {
            get => View.visible;
            set
            {
                _log.Debug(value ? $"showing {_uiDoc.visualTreeAsset.name}" : $"hiding {_uiDoc.visualTreeAsset.name}");

                View.visible = value;
            }
        }

        public void Init(TModel model, PanelSettings panelSettings, NoctuaLocale locale)
        {
            LoadView(panelSettings);
            SetModel(model);
            Locale = locale;
            Locale.OnLanguageChanged += OnLanguageChanged;
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

            _uiDoc = gameObject.AddComponent<UIDocument>();

            var visualTreeAsset = Resources.Load<VisualTreeAsset>(viewResourceName);

            if (visualTreeAsset is null)
            {
                _log.Error($"View not found for {viewResourceName}");
            }

            _uiDoc.visualTreeAsset = visualTreeAsset ?? throw new ArgumentNullException(nameof(visualTreeAsset));
            View = _uiDoc.rootVisualElement;
            View.visible = false;
            
            _uiDoc.panelSettings = panelSettings ?? throw new ArgumentNullException(nameof(panelSettings));
        }
        
        private void OnLanguageChanged(string language)
        {
            Utility.ApplyTranslations(View, GetType().Name, Locale.GetTranslations());
        }
    }
}