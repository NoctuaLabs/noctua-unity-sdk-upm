using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.AdPlaceholder;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.Video;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter for the <b>banner</b> cross-promotion placeholder. Deliberately independent from
    /// <see cref="NoctuaAdPlaceholder"/> (the full-screen placeholder): it lives on its own
    /// <c>UIDocument</c>, is non-modal (only the banner box itself is tappable — the rest of the
    /// screen passes straight through to the game), and persists on its own lifecycle. A full-screen
    /// placeholder or a real ad simply draws over it; when that closes, the banner is still here.
    /// </summary>
    internal class NoctuaAdBannerPlaceholder : Presenter<object>
    {
        private VisualElement _root;
        private VisualElement _box;
        private VisualElement _closeBtn;

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaAdBannerPlaceholder));
        private const string LogTag = "[cross_promo_banner_ui]";

        // Box size + screen anchor — defaults mirror BannerAdmob's own defaults (AdSize.Banner /
        // AdPosition.Bottom). Configurable via SetLayout so games can match wherever their real
        // banner sits.
        private AdPlaceholderSize _size = AdPlaceholderSize.Banner;
        private AdPlaceholderPosition _position = AdPlaceholderPosition.Bottom;
        private const int EdgeOffsetPx = 10;

        // Safety net: auto-close if the asset never loads.
        private CancellationTokenSource _loadTimeoutCts;
        private const int LOAD_TIMEOUT_MS = 10000;

        private string _clickUrl;
        private VideoPlayer _activePlayer;
        private EventCallback<ClickEvent> _ctaHandler;
        private bool _assetHandled;

        private System.Action _onClosed;
        private System.Action _onClicked;
        private System.Action _onShown;
        private System.Action _onFailed;

        protected override void Attach() { }
        protected override void Detach() { }

        private void Start()
        {
            _root = View.Q<VisualElement>("Root");
            _box = View.Q<VisualElement>("BannerBox");
            _closeBtn = View.Q<VisualElement>("BannerCloseButton");

            _closeBtn.RegisterCallback<ClickEvent>(OnCloseClicked);
            ApplyLayout();
            RefreshPicking();
        }

        /// <summary>Configures the banner box size and screen anchor. Applied immediately if the view is ready.</summary>
        public void SetLayout(AdPlaceholderSize size, AdPlaceholderPosition position)
        {
            _size = size;
            _position = position;
            ApplyLayout();
        }

        /// <summary>
        /// Loads the creative referenced by <paramref name="entry"/> (image or video) into the banner
        /// box and shows it. The banner has NO skip gate — the close (X) is shown as soon as the
        /// creative renders (<c>MinWatchSeconds</c> is ignored for the banner surface).
        /// </summary>
        public void Show(CrossPromotionEntry entry)
        {
            CancelTimers();
            StopActiveAsset();

            if (entry == null || string.IsNullOrEmpty(entry.AssetUrl))
            {
                _log.Warning($"{LogTag} show - banner cross-promotion entry missing asset, not showing");
                return;
            }

            Visible = true;
            RefreshPicking();
            _clickUrl = entry.ClickUrl;
            _assetHandled = false;
            SetCloseButtonVisible(false);
            ApplyLayout();

            _log.Info($"{LogTag} show - banner placeholder shown");

            _loadTimeoutCts = new CancellationTokenSource();
            StartLoadTimeoutAsync(_loadTimeoutCts.Token).Forget();

            PlaceholderAssetSource.Instance.GetAdAsset(entry.AssetUrl, asset =>
            {
                if (_assetHandled) return;
                _assetHandled = true;
                CancelLoadTimeout();

                if (asset == null || _box == null)
                {
                    _log.Warning($"{LogTag} show - banner asset failed to load, reporting not-ready");
                    FailPlaceholder();
                    return;
                }

                _box.RemoveFromClassList("hide");

                if (asset.IsVideo)
                {
                    _box.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(asset.Video));
                    _activePlayer = asset.Player;
                    asset.Player.Play();
                }
                else
                {
                    if (asset.Image == null)
                    {
                        _log.Warning($"{LogTag} show - banner image texture null, reporting not-ready");
                        FailPlaceholder();
                        return;
                    }
                    _box.style.backgroundImage = new StyleBackground(asset.Image);
                }

                // No skip gate on the banner — close is available immediately.
                SetCloseButtonVisible(true);
                RegisterClickThrough();
                _log.Info($"{LogTag} show - banner asset rendered");
                _onShown?.Invoke();
            });
        }

        /// <summary>
        /// Applies <see cref="_size"/> / <see cref="_position"/> to the banner box via inline styles,
        /// anchoring it the way a real AdMob/AppLovin banner would sit.
        /// </summary>
        private void ApplyLayout()
        {
            if (_box == null) return;

            var s = _box.style;
            s.width = _size.Width;
            s.height = _size.Height;

            s.left = StyleKeyword.Auto;
            s.right = StyleKeyword.Auto;
            s.top = StyleKeyword.Auto;
            s.bottom = StyleKeyword.Auto;
            s.marginLeft = 0;
            s.marginTop = 0;

            switch (_position)
            {
                case AdPlaceholderPosition.Top:
                    s.left = new Length(50, LengthUnit.Percent);
                    s.marginLeft = -_size.Width / 2f;
                    s.top = EdgeOffsetPx;
                    break;
                case AdPlaceholderPosition.Bottom:
                    s.left = new Length(50, LengthUnit.Percent);
                    s.marginLeft = -_size.Width / 2f;
                    s.bottom = EdgeOffsetPx;
                    break;
                case AdPlaceholderPosition.TopLeft:
                    s.left = EdgeOffsetPx;
                    s.top = EdgeOffsetPx;
                    break;
                case AdPlaceholderPosition.TopRight:
                    s.right = EdgeOffsetPx;
                    s.top = EdgeOffsetPx;
                    break;
                case AdPlaceholderPosition.BottomLeft:
                    s.left = EdgeOffsetPx;
                    s.bottom = EdgeOffsetPx;
                    break;
                case AdPlaceholderPosition.BottomRight:
                    s.right = EdgeOffsetPx;
                    s.bottom = EdgeOffsetPx;
                    break;
                case AdPlaceholderPosition.Center:
                    s.left = new Length(50, LengthUnit.Percent);
                    s.marginLeft = -_size.Width / 2f;
                    s.top = new Length(50, LengthUnit.Percent);
                    s.marginTop = -_size.Height / 2f;
                    break;
            }
        }

        /// <summary>
        /// The banner never blocks the game: the panel root ignores pointer events so only the banner
        /// box itself is tappable, everything else passes through to the game.
        /// </summary>
        private void RefreshPicking()
        {
            if (View != null) View.pickingMode = PickingMode.Ignore;
            if (_root != null) _root.pickingMode = PickingMode.Ignore;
            if (_box != null) _box.pickingMode = PickingMode.Position;
        }

        private void RegisterClickThrough()
        {
            UnregisterClickThrough();
            if (string.IsNullOrEmpty(_clickUrl)) return;
            _ctaHandler = _ => OnCtaClicked();
            _box.RegisterCallback(_ctaHandler);
        }

        private void UnregisterClickThrough()
        {
            if (_box != null && _ctaHandler != null) _box.UnregisterCallback(_ctaHandler);
            _ctaHandler = null;
        }

        private void OnCtaClicked()
        {
            if (string.IsNullOrEmpty(_clickUrl)) return;
            _log.Debug($"{LogTag} cta_click - opening click-through URL: {_clickUrl}");
            _onClicked?.Invoke();
            Application.OpenURL(_clickUrl);
        }

        private async UniTaskVoid StartLoadTimeoutAsync(CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(LOAD_TIMEOUT_MS, cancellationToken: cancellationToken);
                await UniTask.SwitchToMainThread();
                if (!_assetHandled && Visible)
                {
                    _assetHandled = true;
                    _log.Warning($"{LogTag} load_timeout - banner asset did not load within {LOAD_TIMEOUT_MS}ms");
                    FailPlaceholder();
                }
            }
            catch (System.OperationCanceledException) { }
        }

        private void SetCloseButtonVisible(bool visible)
        {
            if (_closeBtn == null) return;
            if (visible) _closeBtn.RemoveFromClassList("hide");
            else _closeBtn.AddToClassList("hide");
        }

        private void CancelLoadTimeout()
        {
            if (_loadTimeoutCts == null) return;
            _loadTimeoutCts.Cancel();
            _loadTimeoutCts.Dispose();
            _loadTimeoutCts = null;
        }

        private void CancelTimers()
        {
            CancelLoadTimeout();
        }

        private void StopActiveAsset()
        {
            _activePlayer = null;
            PlaceholderAssetSource.Instance.StopVideo();
            UnregisterClickThrough();
            _clickUrl = null;
            _box?.AddToClassList("hide");
        }

        /// <summary>Registers a callback invoked whenever the banner is dismissed.</summary>
        public void SetClosedCallback(System.Action onClosed) => _onClosed = onClosed;

        /// <summary>Registers a callback invoked when the banner asset is tapped (click-through).</summary>
        public void SetClickedCallback(System.Action onClicked) => _onClicked = onClicked;

        /// <summary>Registers a callback invoked once the banner asset has rendered.</summary>
        public void SetShownCallback(System.Action onShown) => _onShown = onShown;

        /// <summary>Registers a callback invoked when the banner asset can't be loaded/shown.</summary>
        public void SetFailedCallback(System.Action onFailed) => _onFailed = onFailed;

        private void FailPlaceholder()
        {
            CancelTimers();
            StopActiveAsset();
            Visible = false;
            _onFailed?.Invoke();
        }

        private void OnCloseClicked(ClickEvent evt)
        {
            CancelTimers();
            StopActiveAsset();
            Visible = false;
            _log.Info($"{LogTag} close - banner closed by user");
            _onClosed?.Invoke();
        }

        /// <summary>Programmatically closes the banner (e.g. a real ad / "remove ads" IAP took over).</summary>
        public void Close()
        {
            CancelTimers();
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                StopActiveAsset();
                Visible = false;
                _onClosed?.Invoke();
            });
            _log.Info($"{LogTag} close - banner closed by external call");
        }

        private void OnDestroy()
        {
            CancelTimers();
            StopActiveAsset();
            if (_closeBtn != null) _closeBtn.UnregisterCallback<ClickEvent>(OnCloseClicked);
        }
    }
}
