using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using com.noctuagames.sdk.AdPlaceholder;

namespace com.noctuagames.sdk.UI
{
    internal class UIFactory
    {
        private readonly GameObject _rootObject;
        private readonly PanelSettings _panelSettings;
        private readonly NoctuaLocale _locale;
        private readonly LoadingProgressPresenter _loading;
        private readonly GeneralNotificationPresenter _notification;
        private readonly BannedConfirmationDialogPresenter _confirmDialog;
        private readonly RetryDialogPresenter _retryDialog;
        private readonly StartGameErrorDialogPresenter _startGameErrorDialog;
        private readonly NoctuaAdPlaceholder _adPlaceholder;

        internal UIFactory(GameObject rootObject, PanelSettings panelSettings, NoctuaLocale locale)
        {
            _locale = locale;

            _rootObject = rootObject;
            _panelSettings = panelSettings;

            _loading = Create<LoadingProgressPresenter, object>(new object());
            _loading.GetComponent<UIDocument>().sortingOrder = 1;
            _notification = Create<GeneralNotificationPresenter, object>(new object());
            _notification.GetComponent<UIDocument>().sortingOrder = 1;
            _confirmDialog = Create<BannedConfirmationDialogPresenter, object>(new object());
            _confirmDialog.GetComponent<UIDocument>().sortingOrder = 1;
            _retryDialog = Create<RetryDialogPresenter, object>(new object());
            _retryDialog.GetComponent<UIDocument>().sortingOrder = 1;
            _startGameErrorDialog = Create<StartGameErrorDialogPresenter, object>(new object());
            _startGameErrorDialog.GetComponent<UIDocument>().sortingOrder = 1;
            _adPlaceholder = Create<NoctuaAdPlaceholder, object>(new object());
            _adPlaceholder.GetComponent<UIDocument>().sortingOrder = 1;
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
            presenter.Init(model, _panelSettings, _locale);

            var visualElementRoot = gameObject.GetComponent<UIDocument>().rootVisualElement;
            ApplyLocalization(visualElementRoot, typeof(TPresenter).Name, _locale.GetTranslations());

            gameObject.SetActive(true);
            
            return presenter;
        }

        public async UniTask<bool> ShowRetryDialog(string message, string context = "general")
        {
            return await _retryDialog.Show(message, context);
        }

        public async UniTask<bool> ShowBannedConfirmationDialog()
        {
            return await _confirmDialog.Show();
        }

        public void ShowLoadingProgress(bool isShow)
        {
            _loading.Show(isShow);
        }

        public void ShowGeneralNotification(string message, bool isSuccess = false, uint durationMs = 3000)
        {
            _notification.Show(message, isSuccess, durationMs);
        }
        
        public void ShowInfo(string message)
        {
            _notification.Show(message, true);
        }
        
        public void ShowError(string message)
        {
            _notification.Show(message, false);
        }

        public void ShowInfo(LocaleTextKey textKey)
        {
            _notification.Show(textKey, true);
        }
        
        public void ShowError(LocaleTextKey textKey)
        {
            _notification.Show(textKey, false);
        }
        
        public async UniTask ShowStartGameErrorDialog(string errorMessage)
        {
            await _startGameErrorDialog.Show(errorMessage);
        }

        public void ShowAdPlaceholder(AdPlaceholderType adType)
        {
            _adPlaceholder.Show(adType: adType);
        }

        public void CloseAdPlaceholder()
        {
            _adPlaceholder.CloseAdPlaceholder();
        }

        private void ApplyLocalization(VisualElement root, string uxmlName, Dictionary<string, string> localization)
        {
            Utility.ApplyTranslations(root, uxmlName, localization);
        }
    }
}
