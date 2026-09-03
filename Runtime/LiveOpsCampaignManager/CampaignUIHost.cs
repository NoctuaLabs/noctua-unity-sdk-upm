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
    public sealed class CampaignUIHost
    {
        private const int SortingOrder = 3; // above dialogs / ad placeholders

        private readonly PanelSettings _panelSettings;
        private readonly NoctuaLocale _locale;
        private readonly CampaignRenderer _renderer;
        private readonly GameObject _root;
        private readonly ILogger _log = new NoctuaLogger(typeof(CampaignUIHost));

        private CampaignPopupPresenter _popup;

        public CampaignUIHost(PanelSettings panelSettings, NoctuaLocale locale, CampaignRenderer renderer)
        {
            _panelSettings = panelSettings;
            _locale = locale;
            _renderer = renderer;

            _root = new GameObject("NoctuaLiveOpsCampaignUI");
            UnityEngine.Object.DontDestroyOnLoad(_root);
        }

        /// <summary>The lazily-created popup presenter.</summary>
        public CampaignPopupPresenter Popup
        {
            get
            {
                if (_popup == null)
                {
                    _popup = Create<CampaignPopupPresenter>("CampaignPopupPresenter");
                    _popup.Configure(_renderer);
                }
                return _popup;
            }
        }

        /// <summary>The popup presenter if one has been created, else <c>null</c> — never creates one.</summary>
        public CampaignPopupPresenter PopupIfCreated => _popup;

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
