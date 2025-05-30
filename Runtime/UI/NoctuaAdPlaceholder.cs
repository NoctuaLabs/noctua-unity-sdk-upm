using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.AdPlaceholder;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.UI
{
    internal class NoctuaAdPlaceholder : Presenter<object>
    {
        private VisualElement _closeBtn;
        private VisualElement _adPlaceholder;
        private VisualElement _bannerPlaceholder;

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaAdPlaceholder));

        protected override void Attach()
        { }

        protected override void Detach()
        { }

        private void Start()
        {
            _closeBtn = View.Q<VisualElement>("CloseButton");
            _adPlaceholder = View.Q<VisualElement>("AdPlaceholder");
            _bannerPlaceholder = View.Q<VisualElement>("BannerPlaceholder");

            _closeBtn.RegisterCallback<ClickEvent>(CloseDialog);
        }

        public void Show(AdPlaceholderType adType)
        {
            Visible = true;

            _log.Info($"Ad placeholder shown for type: {adType}");

            // Load and apply image
            PlaceholderAssetSource.Instance.GetAdAssetResource(adType, texture =>
            {
                if (texture != null)
                {
                    if (adType == AdPlaceholderType.Banner)
                    {
                        _bannerPlaceholder.RemoveFromClassList("hide");
                        _adPlaceholder.AddToClassList("hide");

                        _bannerPlaceholder.style.backgroundImage = new StyleBackground(texture);
                        _log.Info($"Banner placeholder image set for type: {adType}");
                    }
                    else
                    {
                        _bannerPlaceholder.AddToClassList("hide");
                        _adPlaceholder.RemoveFromClassList("hide");

                        _adPlaceholder.style.backgroundImage = new StyleBackground(texture);
                        _log.Info($"Ad placeholder image set for type: {adType}");
                    }
                }
                else
                {
                    _log.Warning($"Failed to load ad placeholder image for type: {adType}");
                }
            });
        }

        private void CloseDialog(ClickEvent evt)
        {
            Visible = false;
            _log.Info("Ad placeholder closed");
        }

        public void CloseAdPlaceholder()
        {
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();

                Visible = false;
            });

            _log.Info("Ad placeholder closed by external call");
        }
    }
}
