using System;
using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.UI;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Owns the campaign popup presenter <c>GameObject</c> under a dedicated
    /// <c>NoctuaLiveOpsCampaignUI</c> root. Replicates the parts of <c>UIFactory.Create</c> the
    /// module needs (Init → localization → activate) without going through <c>UIFactory</c>,
    /// so nothing here references <c>IAdPlaceholderUI</c> or any IAA type.
    /// </summary>
    public sealed class CampaignUIHost : ICampaignPopupUI
    {
        // Above ad placeholders, below SDK modals (retry / error / payment dialogs) so a purchase
        // started from the popup can still show them.
        private const int SortingOrder = UIUtility.CampaignPopupSortingOrder;

        private readonly PanelSettings _panelSettings;
        private readonly NoctuaLocale _locale;
        private readonly CampaignRenderer _renderer;
        private readonly ICampaignImageSource _images;
        private readonly GameObject _root;
        private readonly ILogger _log = new NoctuaLogger(typeof(CampaignUIHost));

        private CampaignPopupPresenter _popup;

        public CampaignUIHost(PanelSettings panelSettings, NoctuaLocale locale,
            Action<CampaignAction, CampaignItem> dispatch, ICampaignImageSource images,
            Action<string, Action<UnityEngine.TextCore.Text.FontAsset>> getFont = null)
        {
            _panelSettings = panelSettings;
            _locale = locale;
            _renderer = new CampaignRenderer(dispatch, images, getFont);
            _images = images;

            _root = new GameObject("NoctuaLiveOpsCampaignUI");
            UnityEngine.Object.DontDestroyOnLoad(_root);
        }

        /// <summary>The lazily-created popup presenter.</summary>
        private CampaignPopupPresenter Popup
        {
            get
            {
                if (_popup == null)
                {
                    _popup = Create<CampaignPopupPresenter>("CampaignPopupPresenter");
                    _popup.Configure(_renderer, _images);
                }
                return _popup;
            }
        }

        // Only SetCallbacks and Show create the popup; the rest act on one that already exists.
        public bool IsShowing => _popup != null && _popup.IsShowing;

        public void SetCallbacks(Action<CampaignItem> onShown, Action onClosed, Action onFailed) =>
            Popup.SetCallbacks(onShown, onClosed, onFailed);

        public void Show(CampaignItem item, int configSchemaVersion) => Popup.Show(item, configSchemaVersion);

        public bool Refresh(CampaignItem item, int configSchemaVersion) =>
            _popup != null && _popup.Refresh(item, configSchemaVersion);

        public void SetBusy(bool busy) => _popup?.SetBusy(busy);

        public void Close() => _popup?.Close();

        private T Create<T>(string presenterName) where T : Presenter<object>
        {
            var go = new GameObject(presenterName);
            go.transform.SetParent(_root.transform);
            go.SetActive(false);

            var presenter = go.AddComponent<T>();
            presenter.Init(new object(), _panelSettings, _locale);

            var doc = go.GetComponent<UIDocument>();
            if (doc != null)
            {
                doc.sortingOrder = SortingOrder;
                if (doc.rootVisualElement != null)
                {
                    UIUtility.ApplyTranslations(doc.rootVisualElement, presenterName, _locale.GetTranslations());
                }
            }
            else
            {
                _log.Warning($"{presenterName} has no UIDocument after Init");
            }

            go.SetActive(true);
            return presenter;
        }
    }
}
