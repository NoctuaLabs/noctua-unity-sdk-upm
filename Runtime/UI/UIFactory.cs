using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class UIFactory
    {
        private readonly GameObject _rootObject;
        private readonly PanelSettings _panelSettings;
        internal UIFactory(string gameObjectName, string panelSettingsPath = "NoctuaPanelSettings", string themeStyleSheetPath = "NoctuaTheme")
        {
            _rootObject = new GameObject(gameObjectName);
            _panelSettings = Resources.Load<PanelSettings>(panelSettingsPath);
            _panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(themeStyleSheetPath);
        }
        
        internal UIFactory(GameObject rootObject, PanelSettings panelSettings)
        {
            _rootObject = rootObject;
            _panelSettings = panelSettings;
        }
        
        internal TPresenter Create<TPresenter, TModel>(TModel model) where TPresenter : Presenter<TModel>
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }
            
            var gameObject = new GameObject(typeof(TPresenter).Name);
            gameObject.SetActive(false);
            var presenter = gameObject.AddComponent<TPresenter>();
            presenter.transform.SetParent(_rootObject.transform);
            presenter.Init(model, _panelSettings);
            gameObject.SetActive(true);
            
            return presenter;
        }
    }
}