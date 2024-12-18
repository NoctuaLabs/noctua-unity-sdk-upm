using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

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
        private readonly Dictionary<string, string> _translations;
        private readonly string _language;
        internal UIFactory(string gameObjectName, string panelSettingsPath = "NoctuaPanelSettings", string themeStyleSheetPath = "NoctuaTheme")
        {
            _translations = Utility.LoadTranslations(_language);

            _rootObject = new GameObject(gameObjectName);
            _panelSettings = Resources.Load<PanelSettings>(panelSettingsPath);
            _panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(themeStyleSheetPath);

            _loading = CreateLoadingPresenter();
            _notification = CreateNotificationPresenter();
            _confirmDialog = CreateConfirmDialogPresenter();
            _retryDialog = CreateRetryDialogPresenter();
        }
        
        internal UIFactory(GameObject rootObject, PanelSettings panelSettings, GlobalConfig config, NoctuaLocale locale)
        {
            _locale = locale;
            _language = locale.GetLanguage();
            _translations = Utility.LoadTranslations(_language);

            _rootObject = rootObject;
            _panelSettings = panelSettings;

            _loading = CreateLoadingPresenter();
            _loading.GetComponent<UIDocument>().sortingOrder = 1;
            _notification = CreateNotificationPresenter();
            _notification.GetComponent<UIDocument>().sortingOrder = 1;
            _confirmDialog = CreateConfirmDialogPresenter();
            _confirmDialog.GetComponent<UIDocument>().sortingOrder = 1;
            _retryDialog = CreateRetryDialogPresenter();
            _retryDialog.GetComponent<UIDocument>().sortingOrder = 1;
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
            ApplyLocalization(visualElementRoot, typeof(TPresenter).Name, _translations);

            gameObject.SetActive(true);
            
            return presenter;
        }

        public async UniTask<bool> ShowRetryDialog(string message, string context = "general")
        {
            return await _retryDialog.Show(message, context);
        }

        public async UniTask<bool> ShowBannedConfirmationDialog()
        {
            return await _confirmDialog.Show(_language);
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

        private RetryDialogPresenter CreateRetryDialogPresenter()
        {
            return Create<RetryDialogPresenter, object>(new object());
        }

        private CustomPaymentCompleteDialogPresenter CreateCustomPaymentCompleteDialogPresenter()
        {
            return Create<CustomPaymentCompleteDialogPresenter, object>(new object());
        }

        private BannedConfirmationDialogPresenter CreateConfirmDialogPresenter()
        {
            return Create<BannedConfirmationDialogPresenter, object>(new object());
        }

        private LoadingProgressPresenter CreateLoadingPresenter()
        {
            return Create<LoadingProgressPresenter, object>(new object());
        }

        private GeneralNotificationPresenter CreateNotificationPresenter()
        {
            return Create<GeneralNotificationPresenter, object>(new object());
        }

        private void ApplyLocalization(VisualElement root, string uxmlName, Dictionary<string, string> localization)
        {
            Utility.ApplyTranslations(root, uxmlName, localization);
        }
    }
}
