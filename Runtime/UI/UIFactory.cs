using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class UIFactory
    {
        private readonly GameObject _rootObject;
        private readonly PanelSettings _panelSettings;
        private readonly LoadingProgressPresenter _loading;
        private readonly GeneralNotificationPresenter _notification;
        internal UIFactory(string gameObjectName, string panelSettingsPath = "NoctuaPanelSettings", string themeStyleSheetPath = "NoctuaTheme")
        {
            _rootObject = new GameObject(gameObjectName);
            _panelSettings = Resources.Load<PanelSettings>(panelSettingsPath);
            _panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(themeStyleSheetPath);

            _loading = CreateLoadingPresenter();
            _notification = CreateNotificationPresenter();
        }
        
        internal UIFactory(GameObject rootObject, PanelSettings panelSettings)
        {
            _rootObject = rootObject;
            _panelSettings = panelSettings;

            _loading = CreateLoadingPresenter();
            _notification = CreateNotificationPresenter();
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

        public void ShowLoadingProgress(bool isShow)
        {
            _loading.Show(isShow);
        }

        public void ShowGeneralNotification(string message, bool isSuccess = false)
        {
            _notification.Show(message, isSuccess);
        }

        private LoadingProgressPresenter CreateLoadingPresenter()
        {
            return Create<LoadingProgressPresenter, object>(new object());
        }

        private GeneralNotificationPresenter CreateNotificationPresenter()
        {
            return Create<GeneralNotificationPresenter, object>(new object());
        }
    }
}