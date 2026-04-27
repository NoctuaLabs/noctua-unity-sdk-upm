using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.AdPlaceholder;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter that displays a placeholder image while an ad is loading, with auto-close timeout and close button support.
    /// </summary>
    internal class NoctuaAdPlaceholder : Presenter<object>
    {
        private VisualElement _closeBtn;
        private VisualElement _adPlaceholder;
        private VisualElement _bannerPlaceholder;

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaAdPlaceholder));

        private CancellationTokenSource _timeoutCts;
        private const int PLACEHOLDER_TIMEOUT_MS = 10000; // 10 seconds

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

        /// <summary>
        /// Displays the ad placeholder for the specified ad type, loading the appropriate placeholder image and starting an auto-close timeout.
        /// </summary>
        /// <param name="adType">The type of ad placeholder to display (banner, interstitial, or rewarded).</param>
        public void Show(AdPlaceholderType adType)
        {
            // Cancel any previous timeout
            CancelTimeout();

            Visible = true;

            _log.Info($"Ad placeholder shown for type: {adType}");

            // Start timeout to auto-close if no ad callback arrives
            _timeoutCts = new CancellationTokenSource();
            StartTimeoutAsync(_timeoutCts.Token).Forget();

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

        private async UniTaskVoid StartTimeoutAsync(CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(PLACEHOLDER_TIMEOUT_MS, cancellationToken: cancellationToken);

                // Timeout reached — auto-close
                await UniTask.SwitchToMainThread();

                if (Visible)
                {
                    _log.Warning($"Ad placeholder timed out after {PLACEHOLDER_TIMEOUT_MS}ms, auto-closing.");
                    Visible = false;
                }
            }
            catch (System.OperationCanceledException)
            {
                // Timeout was cancelled because placeholder was closed normally — ignore
            }
        }

        private void CancelTimeout()
        {
            if (_timeoutCts != null)
            {
                _timeoutCts.Cancel();
                _timeoutCts.Dispose();
                _timeoutCts = null;
            }
        }

        private void CloseDialog(ClickEvent evt)
        {
            CancelTimeout();
            Visible = false;
            _log.Info("Ad placeholder closed");
        }

        /// <summary>
        /// Programmatically closes the ad placeholder from an external caller, cancelling any pending timeout.
        /// </summary>
        public void CloseAdPlaceholder()
        {
            CancelTimeout();

            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();

                Visible = false;
            });

            _log.Info("Ad placeholder closed by external call");
        }

        private void OnDestroy()
        {
            CancelTimeout();

            if (_closeBtn != null)
            {
                _closeBtn.UnregisterCallback<ClickEvent>(CloseDialog);
            }
        }
    }
}
