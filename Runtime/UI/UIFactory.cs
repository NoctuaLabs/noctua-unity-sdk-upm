using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using com.noctuagames.sdk.AdPlaceholder;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Central factory for creating and managing UI presenters and common dialogs.
    /// Provides convenience methods for showing loading indicators, notifications, retry dialogs, and ad placeholders.
    /// </summary>
    internal class UIFactory : IAdPlaceholderUI
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

        /// <summary>
        /// Initializes the UI factory and creates shared presenters for loading, notifications, and dialogs.
        /// </summary>
        /// <param name="rootObject">The root GameObject under which all presenter GameObjects are parented.</param>
        /// <param name="panelSettings">The UI Toolkit panel settings used for all presenters.</param>
        /// <param name="locale">The locale provider for translations.</param>
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
        
        /// <summary>
        /// Creates a new UI presenter of the specified type, initializes it with the given model, and applies localization.
        /// </summary>
        /// <typeparam name="TPresenter">The presenter type to create.</typeparam>
        /// <typeparam name="TModel">The model type for the presenter.</typeparam>
        /// <param name="model">The model instance to bind to the presenter.</param>
        /// <returns>The created and initialized presenter instance.</returns>
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

        /// <summary>
        /// Displays a retry dialog with the specified message and returns whether the user chose to retry.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        /// <param name="context">The context identifier for customer service routing.</param>
        /// <returns><c>true</c> if the user chose to retry; <c>false</c> if dismissed.</returns>
        public async UniTask<bool> ShowRetryDialog(string message, string context = "general")
        {
            return await _retryDialog.Show(message, context);
        }

        /// <summary>
        /// Displays a confirmation dialog informing the user they have been banned, with a link to customer service.
        /// </summary>
        /// <returns><c>true</c> when the dialog is acknowledged.</returns>
        public async UniTask<bool> ShowBannedConfirmationDialog()
        {
            return await _confirmDialog.Show();
        }

        /// <summary>
        /// Shows or hides the full-screen loading spinner overlay.
        /// </summary>
        /// <param name="isShow"><c>true</c> to show the loading indicator; <c>false</c> to hide it.</param>
        public void ShowLoadingProgress(bool isShow)
        {
            _loading.Show(isShow);
        }

        /// <summary>
        /// Displays a temporary toast notification with the specified message.
        /// </summary>
        /// <param name="message">The notification text to display.</param>
        /// <param name="isSuccess"><c>true</c> for a green success notification; <c>false</c> for a red error notification.</param>
        /// <param name="durationMs">Duration in milliseconds before the notification auto-dismisses.</param>
        public void ShowGeneralNotification(string message, bool isSuccess = false, uint durationMs = 3000)
        {
            _notification.Show(message, isSuccess, durationMs);
        }
        
        /// <summary>
        /// Displays a success notification with the specified message.
        /// </summary>
        /// <param name="message">The info message to display.</param>
        public void ShowInfo(string message)
        {
            _notification.Show(message, true);
        }

        /// <summary>
        /// Displays an error notification with the specified message.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        public void ShowError(string message)
        {
            _notification.Show(message, false);
        }

        /// <summary>
        /// Displays a success notification using a localized text key.
        /// </summary>
        /// <param name="textKey">The locale text key to translate and display.</param>
        public void ShowInfo(LocaleTextKey textKey)
        {
            _notification.Show(textKey, true);
        }

        /// <summary>
        /// Displays an error notification using a localized text key.
        /// </summary>
        /// <param name="textKey">The locale text key to translate and display.</param>
        public void ShowError(LocaleTextKey textKey)
        {
            _notification.Show(textKey, false);
        }
        
        /// <summary>
        /// Displays a fatal error dialog for game startup failures, then quits the application after acknowledgment.
        /// </summary>
        /// <param name="errorMessage">The error message describing the startup failure.</param>
        public async UniTask ShowStartGameErrorDialog(string errorMessage)
        {
            await _startGameErrorDialog.Show(errorMessage);
        }

        /// <summary>
        /// Displays an ad placeholder UI for the specified ad type while the real ad loads.
        /// </summary>
        /// <param name="adType">The type of ad placeholder to display (banner, interstitial, or rewarded).</param>
        public void ShowAdPlaceholder(AdPlaceholderType adType)
        {
            _adPlaceholder.Show(adType: adType);
        }

        /// <summary>
        /// Closes the currently displayed ad placeholder UI.
        /// </summary>
        public void CloseAdPlaceholder()
        {
            _adPlaceholder.CloseAdPlaceholder();
        }

        private void ApplyLocalization(VisualElement root, string uxmlName, Dictionary<string, string> localization)
        {
            UIUtility.ApplyTranslations(root, uxmlName, localization);
        }
    }
}
