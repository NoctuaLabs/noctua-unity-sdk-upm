using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.AdPlaceholder;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.Video;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter that displays a cross-promotion placeholder (CDN image or video) while an ad is
    /// loading. Supports a click-through CTA and gates the close (X) button behind a bottom-left
    /// skip countdown that ends when the video finishes or the minimum watch time elapses.
    /// Closes itself if the CDN asset fails to load (no static fallback).
    /// </summary>
    internal class NoctuaAdPlaceholder : Presenter<object>
    {
        private VisualElement _closeBtn;
        private VisualElement _fullScreenContainer;
        private VisualElement _adPlaceholder;
        private VisualElement _bannerPlaceholder;
        private Label _countdownLabel;

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaAdPlaceholder));

        /// <summary>Structured log tag for all cross-promotion placeholder UI logs.</summary>
        private const string LogTag = "[cross_promo_ui]";

        // Safety net: auto-close if the asset never loads (e.g. prepare hangs).
        private CancellationTokenSource _loadTimeoutCts;
        private const int LOAD_TIMEOUT_MS = 10000; // 10 seconds

        // Reveals the close button after a minimum watch time.
        private CancellationTokenSource _minWatchCts;

        private VisualElement _activeElement;
        private string _clickUrl;
        private VideoPlayer _activePlayer;
        private EventCallback<ClickEvent> _ctaHandler;

        // Invoked whenever the placeholder is dismissed, so the presenter (MediationManager) can keep
        // its shown/hidden state in sync (e.g. after the user taps close).
        private System.Action _onClosed;

        // Invoked when the user taps the asset (click-through), so the presenter can fire OnAdClicked.
        private System.Action _onClicked;

        // Invoked once the asset has actually rendered, so the presenter can fire OnAdDisplayed.
        private System.Action _onShown;

        // Invoked when the asset can't be loaded/shown, so the presenter can report no-ad-available.
        private System.Action _onFailed;

        // Remembered context + one-shot guard so the asset callback and the load-timeout can't both
        // render. Whichever fires first wins; the load-timeout closes the placeholder.
        private int _currentMinWatchMs;
        private bool _assetHandled;

        protected override void Attach()
        { }

        protected override void Detach()
        { }

        private void Start()
        {
            _closeBtn = View.Q<VisualElement>("CloseButton");
            _fullScreenContainer = View.Q<VisualElement>("NoctuaAdPlaceholder");
            _adPlaceholder = View.Q<VisualElement>("AdPlaceholder");
            _bannerPlaceholder = View.Q<VisualElement>("BannerPlaceholder");
            _countdownLabel = View.Q<Label>("Countdown");

            _closeBtn.RegisterCallback<ClickEvent>(CloseDialog);
        }

        /// <summary>
        /// Displays the cross-promotion placeholder for the specified ad type, loading the asset
        /// (image or video) referenced by <paramref name="entry"/>. The close button stays hidden
        /// until the video ends or <c>MinWatchSeconds</c> elapses.
        /// </summary>
        /// <param name="adType">The type of ad placeholder to display (banner, interstitial, or rewarded).</param>
        /// <param name="entry">The resolved cross-promotion asset for this format.</param>
        public void Show(AdPlaceholderType adType, CrossPromotionEntry entry)
        {
            CancelTimers();
            StopActiveAsset();

            if (entry == null || string.IsNullOrEmpty(entry.AssetUrl))
            {
                // Defensive: MediationManager already gates this, but never render an empty placeholder.
                _log.Warning($"{LogTag} show - cross-promotion entry missing asset, not showing placeholder");
                return;
            }

            Visible = true;
            _clickUrl = entry.ClickUrl;
            _currentMinWatchMs = Mathf.Max(0, (entry.MinWatchSeconds ?? 0) * 1000);
            _assetHandled = false;
            SetCloseButtonVisible(false);
            HideCountdown();

            _log.Info($"{LogTag} show - placeholder shown for type: {adType}");

            // Safety net: if the asset hangs (e.g. a video that never prepares while offline), close
            // the placeholder instead of waiting forever.
            _loadTimeoutCts = new CancellationTokenSource();
            StartLoadTimeoutAsync(_loadTimeoutCts.Token).Forget();

            PlaceholderAssetSource.Instance.GetAdAsset(entry.AssetUrl, asset =>
            {
                // The load-timeout may already have closed the placeholder — ignore a late result.
                if (_assetHandled) return;
                _assetHandled = true;

                // Asset resolved (or failed) — the load safety net is no longer needed.
                CancelLoadTimeout();

                if (asset == null)
                {
                    _log.Warning($"{LogTag} show - asset failed to load for {adType}, reporting not-ready");
                    FailPlaceholder();
                    return;
                }

                if (asset.IsVideo)
                {
                    ShowVideo(adType, asset, _currentMinWatchMs);
                }
                else
                {
                    ShowImage(adType, asset.Image, _currentMinWatchMs);
                }
            });
        }

        private void ShowVideo(AdPlaceholderType adType, CrossPromoAsset asset, int minWatchMs)
        {
            var element = SelectElement(adType);
            element.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(asset.Video));
            _activeElement = element;
            _activePlayer = asset.Player;

            RegisterClickThrough(element);

            // Reveal the close button when the video ends (safety) ...
            asset.Player.loopPointReached += OnVideoEnded;

            // ... or via a visible countdown that ends at min(min-watch, video length), whichever
            // comes first. With no min-watch, the countdown tracks the full video length.
            int lengthSec   = asset.Player.length > 0.5 ? Mathf.CeilToInt((float)asset.Player.length) : 0;
            int minWatchSec = minWatchMs / 1000;
            int countdownSec = minWatchSec > 0
                ? (lengthSec > 0 ? Mathf.Min(minWatchSec, lengthSec) : minWatchSec)
                : lengthSec;

            if (countdownSec > 0) StartCloseCountdownAsync(countdownSec).Forget();
            // else: length unknown and no min-watch — leave it to loopPointReached.

            asset.Player.Play();
            _log.Info($"{LogTag} show - video playing for type: {adType} (countdown: {countdownSec}s)");
            _onShown?.Invoke();
        }

        private void ShowImage(AdPlaceholderType adType, Texture2D texture, int minWatchMs)
        {
            if (texture == null)
            {
                _log.Warning($"{LogTag} show - image texture null for {adType}, reporting not-ready");
                FailPlaceholder();
                return;
            }

            var element = SelectElement(adType);
            element.style.backgroundImage = new StyleBackground(texture);
            _activeElement = element;

            RegisterClickThrough(element);

            // Images have no end — count down the minimum watch time, then reveal close
            // (immediately when there's no min-watch).
            int minWatchSec = minWatchMs / 1000;
            if (minWatchSec > 0) StartCloseCountdownAsync(minWatchSec).Forget();
            else SetCloseButtonVisible(true);

            _log.Info($"{LogTag} show - image shown for type: {adType} (countdown: {minWatchSec}s)");
            _onShown?.Invoke();
        }

        /// <summary>
        /// Picks the correct visual element for the ad type (banner box vs. full-screen) and toggles
        /// the other one off.
        /// </summary>
        private VisualElement SelectElement(AdPlaceholderType adType)
        {
            if (adType == AdPlaceholderType.Banner)
            {
                _fullScreenContainer?.AddToClassList("hide");
                _bannerPlaceholder.RemoveFromClassList("hide");
                _adPlaceholder.AddToClassList("hide");
                return _bannerPlaceholder;
            }

            _bannerPlaceholder.AddToClassList("hide");
            _fullScreenContainer?.RemoveFromClassList("hide");
            _adPlaceholder.RemoveFromClassList("hide");
            return _adPlaceholder;
        }

        private void RegisterClickThrough(VisualElement element)
        {
            UnregisterClickThrough();

            if (string.IsNullOrEmpty(_clickUrl)) return;

            _ctaHandler = _ => OnCtaClicked();
            element.RegisterCallback(_ctaHandler);
        }

        private void UnregisterClickThrough()
        {
            if (_activeElement != null && _ctaHandler != null)
            {
                _activeElement.UnregisterCallback(_ctaHandler);
            }
            _ctaHandler = null;
        }

        private void OnCtaClicked()
        {
            if (string.IsNullOrEmpty(_clickUrl)) return;

            _log.Debug($"{LogTag} cta_click - opening click-through URL: {_clickUrl}");
            _onClicked?.Invoke();
            Application.OpenURL(_clickUrl);
        }

        private void OnVideoEnded(VideoPlayer source)
        {
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                _minWatchCts?.Cancel();
                HideCountdown();
                SetCloseButtonVisible(true);
            });
        }

        /// <summary>
        /// Runs a once-per-second countdown shown bottom-left while the close button is hidden, then
        /// reveals the close button when it reaches zero — mirroring mediation's skip countdown.
        /// </summary>
        private async UniTaskVoid StartCloseCountdownAsync(int totalSeconds)
        {
            _minWatchCts?.Cancel();
            _minWatchCts?.Dispose();
            _minWatchCts = new CancellationTokenSource();
            var token = _minWatchCts.Token;

            try
            {
                SetCloseButtonVisible(false);

                for (int remaining = totalSeconds; remaining > 0; remaining--)
                {
                    ShowCountdown(remaining);
                    await UniTask.Delay(1000, cancellationToken: token);
                }

                await UniTask.SwitchToMainThread();
                HideCountdown();
                SetCloseButtonVisible(true);
            }
            catch (System.OperationCanceledException)
            {
                // Placeholder closed / superseded before the countdown elapsed — ignore.
            }
        }

        private void ShowCountdown(int seconds)
        {
            if (_countdownLabel == null) return;
            _countdownLabel.text = $"Ad · {seconds}";
            _countdownLabel.RemoveFromClassList("hide");
        }

        private void HideCountdown()
        {
            _countdownLabel?.AddToClassList("hide");
        }

        private async UniTaskVoid StartLoadTimeoutAsync(CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(LOAD_TIMEOUT_MS, cancellationToken: cancellationToken);
                await UniTask.SwitchToMainThread();

                // The asset never resolved in time — report not-ready.
                if (!_assetHandled && Visible)
                {
                    _assetHandled = true;
                    _log.Warning($"{LogTag} load_timeout - asset did not load within {LOAD_TIMEOUT_MS}ms, reporting not-ready");
                    FailPlaceholder();
                }
            }
            catch (System.OperationCanceledException)
            {
                // Asset loaded (or placeholder closed) before the timeout — ignore.
            }
        }

        private void SetCloseButtonVisible(bool visible)
        {
            if (_closeBtn == null) return;

            if (visible) _closeBtn.RemoveFromClassList("hide");
            else _closeBtn.AddToClassList("hide");
        }

        private void CancelLoadTimeout()
        {
            if (_loadTimeoutCts != null)
            {
                _loadTimeoutCts.Cancel();
                _loadTimeoutCts.Dispose();
                _loadTimeoutCts = null;
            }
        }

        private void CancelTimers()
        {
            CancelLoadTimeout();

            if (_minWatchCts != null)
            {
                _minWatchCts.Cancel();
                _minWatchCts.Dispose();
                _minWatchCts = null;
            }
        }

        private void StopActiveAsset()
        {
            if (_activePlayer != null)
            {
                _activePlayer.loopPointReached -= OnVideoEnded;
                _activePlayer = null;
            }

            PlaceholderAssetSource.Instance.StopVideo();

            UnregisterClickThrough();
            HideCountdown();
            _activeElement = null;
            _clickUrl = null;
        }

        /// <summary>Registers a callback invoked whenever the placeholder is dismissed.</summary>
        public void SetClosedCallback(System.Action onClosed)
        {
            _onClosed = onClosed;
        }

        /// <summary>Registers a callback invoked when the asset is tapped (click-through).</summary>
        public void SetClickedCallback(System.Action onClicked)
        {
            _onClicked = onClicked;
        }

        /// <summary>Registers a callback invoked once the asset has actually rendered.</summary>
        public void SetShownCallback(System.Action onShown)
        {
            _onShown = onShown;
        }

        /// <summary>Registers a callback invoked when the asset can't be loaded/shown.</summary>
        public void SetFailedCallback(System.Action onFailed)
        {
            _onFailed = onFailed;
        }

        /// <summary>
        /// Hides the placeholder because its asset could not be shown (not ready / offline / no cache)
        /// and reports the failure so the presenter can signal no-ad-available. Distinct from a normal
        /// dismiss: it never fires the closed/displayed callbacks.
        /// </summary>
        private void FailPlaceholder()
        {
            CancelTimers();
            StopActiveAsset();
            Visible = false;
            _onFailed?.Invoke();
        }

        private void CloseDialog(ClickEvent evt)
        {
            CancelTimers();
            StopActiveAsset();
            Visible = false;
            _log.Info($"{LogTag} close - placeholder closed by user");
            _onClosed?.Invoke();
        }

        /// <summary>
        /// Programmatically closes the placeholder from an external caller, cancelling timers and
        /// stopping any video playback.
        /// </summary>
        public void CloseAdPlaceholder()
        {
            CancelTimers();

            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();

                StopActiveAsset();
                Visible = false;
                _onClosed?.Invoke();
            });

            _log.Info($"{LogTag} close - placeholder closed by external call");
        }

        private void OnDestroy()
        {
            CancelTimers();
            StopActiveAsset();

            if (_closeBtn != null)
            {
                _closeBtn.UnregisterCallback<ClickEvent>(CloseDialog);
            }
        }
    }
}
