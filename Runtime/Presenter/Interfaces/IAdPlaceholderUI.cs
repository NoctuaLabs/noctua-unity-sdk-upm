using com.noctuagames.sdk.AdPlaceholder;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Abstraction for ad placeholder UI operations.
    /// Used by MediationManager (Presenter) so it doesn't depend
    /// on the concrete UIFactory (UI layer).
    /// </summary>
    public interface IAdPlaceholderUI
    {
        /// <summary>
        /// Displays the <b>full-screen</b> cross-promotion placeholder (interstitial / rewarded /
        /// rewarded interstitial). The <b>banner</b> surface is separate — see
        /// <see cref="ShowBannerPlaceholder"/> — and the two coexist.
        /// </summary>
        /// <param name="adType">The full-screen placeholder type to display.</param>
        /// <param name="entry">
        /// The resolved cross-promotion asset for this format (CDN URL, CTA, min watch). Must be non-null
        /// with an asset URL — the caller (MediationManager) is responsible for the master gate and will
        /// not call this when cross-promotion is disabled for the format.
        /// </param>
        void ShowAdPlaceholder(AdPlaceholderType adType, CrossPromotionEntry entry);

        /// <summary>
        /// Displays the <b>banner</b> cross-promotion placeholder. Non-modal (only the box is
        /// tappable) and independent from the full-screen placeholder — a full-screen placeholder or
        /// a real ad simply draws over it; when that closes, the banner is still shown.
        /// </summary>
        /// <param name="entry">The resolved banner cross-promotion asset (CDN URL, CTA, min watch). Non-null with an asset URL.</param>
        void ShowBannerPlaceholder(CrossPromotionEntry entry);

        /// <summary>Closes and hides the banner cross-promotion placeholder (no-op when not shown).</summary>
        void CloseBannerPlaceholder();

        /// <summary>
        /// Configures the size and screen anchor of the banner placeholder box, mirroring the
        /// flexible size/position of a real network banner (<c>CreateBannerViewAdAdmob</c> /
        /// <c>CreateBannerViewAdAppLovin</c>).
        /// </summary>
        /// <param name="size">Placeholder box size in pixels (presets or a custom size).</param>
        /// <param name="position">Screen anchor (edge, corner, or center).</param>
        void SetBannerLayout(AdPlaceholderSize size, AdPlaceholderPosition position);

        /// <summary>
        /// Preloads cross-promotion assets for all configured formats into the cache so a later
        /// <see cref="ShowAdPlaceholder"/> renders instantly (load-then-show, like mediation ads).
        /// No-op when <paramref name="config"/> is null.
        /// </summary>
        /// <param name="config">The cross-promotion config whose per-format assets should be cached.</param>
        void PreloadAdPlaceholder(CrossPromotionConfig config);

        /// <summary>
        /// Closes and hides the currently displayed <b>full-screen</b> ad placeholder.
        /// </summary>
        void CloseAdPlaceholder();

        /// <summary>
        /// Returns true when the asset at <paramref name="assetUrl"/> is cached locally and can be
        /// shown without a network fetch. Lets the caller (MediationManager) report a cross-promotion
        /// as "ready" only when its creative is actually available, not merely configured.
        /// </summary>
        /// <param name="assetUrl">The CDN URL of the cross-promotion asset to check.</param>
        bool IsAssetCached(string assetUrl);

        /// <summary>
        /// Registers a callback invoked whenever the full-screen placeholder is dismissed (by the user,
        /// auto-close, or programmatically), so the caller can keep its shown/hidden state in sync.
        /// </summary>
        /// <param name="onClosed">Callback invoked after the placeholder is hidden.</param>
        void SetPlaceholderClosedCallback(System.Action onClosed);

        /// <summary>
        /// Registers a callback invoked when the user taps the full-screen placeholder asset (its
        /// click-through), so the caller can fire the ad-clicked event.
        /// </summary>
        /// <param name="onClicked">Callback invoked when the placeholder asset is tapped.</param>
        void SetPlaceholderClickedCallback(System.Action onClicked);

        /// <summary>
        /// Registers a callback invoked once the full-screen placeholder asset has actually rendered,
        /// so the caller can fire the ad-displayed event only when something is really on screen.
        /// </summary>
        /// <param name="onShown">Callback invoked after the asset renders.</param>
        void SetPlaceholderShownCallback(System.Action onShown);

        /// <summary>
        /// Registers a callback invoked when the full-screen placeholder asset could not be loaded/shown
        /// (not ready, offline with no cache, etc.), so the caller can report it as no-ad-available.
        /// </summary>
        /// <param name="onFailed">Callback invoked when the asset fails to load.</param>
        void SetPlaceholderFailedCallback(System.Action onFailed);

        /// <summary>Registers the banner-surface equivalent of <see cref="SetPlaceholderClosedCallback"/>.</summary>
        void SetBannerPlaceholderClosedCallback(System.Action onClosed);

        /// <summary>Registers the banner-surface equivalent of <see cref="SetPlaceholderClickedCallback"/>.</summary>
        void SetBannerPlaceholderClickedCallback(System.Action onClicked);

        /// <summary>Registers the banner-surface equivalent of <see cref="SetPlaceholderShownCallback"/>.</summary>
        void SetBannerPlaceholderShownCallback(System.Action onShown);

        /// <summary>Registers the banner-surface equivalent of <see cref="SetPlaceholderFailedCallback"/>.</summary>
        void SetBannerPlaceholderFailedCallback(System.Action onFailed);
    }
}
