using System;
using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.UI;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Modal presenter for a single <c>popup</c> campaign. Loads the <c>CampaignPopup.uxml</c>
    /// shell, has <see cref="CampaignRenderer"/> build the tree detached, and attaches it to
    /// <c>#Mount</c> in one <c>Add</c>. Enter/exit is a USS <c>opacity</c>/<c>translate</c>
    /// transition (<c>.campaign--shown</c>); the runtime controller's timers stop on close.
    /// </summary>
    public class CampaignPopupPresenter : Presenter<object>
    {
        private const string ShownClass = "campaign--shown";
        private const string FullscreenClass = "campaign-card--fullscreen";
        private const string BorderlessClass = "campaign-card--borderless";
        private const int ExitTransitionMs = 240;

        private readonly ILogger _log = new NoctuaLogger(typeof(CampaignPopupPresenter));

        private VisualElement _root;
        private VisualElement _card;
        private ScrollView _mountScroll;
        private VisualElement _mount;
        private Button _closeBtn;
        private bool _safeAreaActive;

        private CampaignRenderer _renderer;
        private CampaignRuntimeController _controller;
        private Action<CampaignItem> _onShown;
        private Action _onClosed;
        private Action _onFailed;
        private bool _closing;
        private bool _bound;

        /// <summary>True between a successful <see cref="Show"/> and the following <see cref="Close"/>.</summary>
        public bool IsShowing { get; private set; }

        protected override void Attach() { }
        protected override void Detach() { }

        // No text input in v1 — skip the inherited per-frame TouchScreenKeyboard poll.
        protected override void Update() { }

        private void Start() => EnsureBound();

        // Element binding must be synchronous: the UI host calls Show() on the same frame
        // it activates this GameObject, before Unity's Start() message fires.
        private void EnsureBound()
        {
            if (_bound || View == null) return;
            _bound = true;

            _root = View.Q<VisualElement>("Root");
            _card = View.Q<VisualElement>("Card");
            _mountScroll = View.Q<ScrollView>("MountScroll");
            _mount = View.Q<VisualElement>("Mount");
            _closeBtn = View.Q<Button>("CloseButton");

            if (_root != null)
            {
                _root.usageHints = UsageHints.DynamicTransform;
                // Screen size / orientation changes → recompute the safe-area inset.
                _root.RegisterCallback<GeometryChangedEvent>(_ => { if (IsShowing) ApplySafeArea(); });
            }
            if (_closeBtn != null) _closeBtn.clicked += Close;

            Visible = false;
        }

        /// <summary>Injects the shared renderer. Call once, before the first <see cref="Show"/>.</summary>
        public void Configure(CampaignRenderer renderer) => _renderer = renderer;

        /// <summary>Wires lifecycle callbacks (all optional).</summary>
        public void SetCallbacks(Action<CampaignItem> onShown, Action onClosed, Action onFailed)
        {
            _onShown = onShown;
            _onClosed = onClosed;
            _onFailed = onFailed;
        }

        /// <summary>Renders and shows <paramref name="item"/>. Fires <c>onFailed</c> if it can't render.</summary>
        public void Show(CampaignItem item, int configSchemaVersion)
        {
            EnsureBound();

            if (_renderer == null || _mount == null)
            {
                _log.Error("Show() called before Configure()/Start()");
                _onFailed?.Invoke();
                return;
            }

            TeardownController();
            _mount.Clear();
            if (_mountScroll != null) _mountScroll.scrollOffset = Vector2.zero;
            _closing = false;

            _controller = new CampaignRuntimeController();
            var built = _renderer.RenderCampaign(item, _controller, configSchemaVersion);
            if (built == null)
            {
                TeardownController();
                _onFailed?.Invoke();
                return;
            }

            _mount.Add(built);

            if (_card != null)
            {
                _card.EnableInClassList(FullscreenClass, item.Fullscreen);
                _card.EnableInClassList(BorderlessClass, item.Borderless);
            }

            // Edge-to-edge creatives must stay clear of the notch / home indicator.
            _safeAreaActive = item.Fullscreen || item.Borderless;
            ApplySafeArea();

            // Keep the close button on top of whatever the renderer built (a
            // full-bleed image would otherwise paint over it).
            _closeBtn?.BringToFront();

            Visible = true;
            IsShowing = true;
            _root?.RemoveFromClassList(ShownClass);
            // Guard: a Close() before this fires would otherwise re-add the class mid-exit.
            _root?.schedule.Execute(() => { if (IsShowing && !_closing) _root.AddToClassList(ShownClass); });

            _onShown?.Invoke(item);
        }

        /// <summary>Plays the exit transition, disposes timers, then hides. Idempotent.</summary>
        public void Close()
        {
            if (!IsShowing && !_closing)
            {
                HideNow();
                return;
            }
            if (_closing) return;
            _closing = true;

            _root?.RemoveFromClassList(ShownClass);
            TeardownController();

            if (_root != null) _root.schedule.Execute(HideNow).StartingIn(ExitTransitionMs);
            else HideNow();

            _onClosed?.Invoke();
        }

        private void HideNow()
        {
            // The exit transition schedules this ~240ms out; the presenter may be destroyed by then.
            if (this == null) return;

            Visible = false;
            IsShowing = false;
            _closing = false;
            _mount?.Clear();
            _safeAreaActive = false;
            ApplySafeArea();
        }

        /// <summary>
        /// Insets <c>#Root</c>'s content by the device safe area when a fullscreen / borderless
        /// creative is showing, so it clears the notch and home indicator. The dim background
        /// still paints to the physical edges. No-op (and zeroes the padding) otherwise, or
        /// until the panel has a usable layout — the <c>#Root</c> geometry callback retries.
        /// </summary>
        private void ApplySafeArea()
        {
            if (_root == null) return;

            if (!_safeAreaActive)
            {
                _root.style.paddingTop = 0f;
                _root.style.paddingRight = 0f;
                _root.style.paddingBottom = 0f;
                _root.style.paddingLeft = 0f;
                return;
            }

            var refW = _root.panel?.visualTree?.layout.width ?? 0f;
            if (float.IsNaN(refW) || refW < 1f || Screen.width < 1 || Screen.height < 1) return;

            var scale = refW / Screen.width; // physical px → panel points
            var safe = Screen.safeArea;      // origin bottom-left, physical px

            _root.style.paddingLeft = safe.xMin * scale;
            _root.style.paddingRight = (Screen.width - safe.xMax) * scale;
            _root.style.paddingTop = (Screen.height - safe.yMax) * scale;
            _root.style.paddingBottom = safe.yMin * scale;
        }

        private void TeardownController()
        {
            _controller?.Dispose();
            _controller = null;
        }

        protected override void OnDestroy()
        {
            TeardownController();
            if (_closeBtn != null) _closeBtn.clicked -= Close;
            base.OnDestroy();
        }
    }
}
