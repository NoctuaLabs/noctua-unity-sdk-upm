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
        /// Displays a cross-promotion placeholder of the specified type (banner, interstitial, or rewarded).
        /// </summary>
        /// <param name="adType">The type of ad placeholder to display.</param>
        /// <param name="entry">
        /// The resolved cross-promotion asset for this format (CDN URL, CTA, min watch). Must be non-null
        /// with an asset URL — the caller (MediationManager) is responsible for the master gate and will
        /// not call this when cross-promotion is disabled for the format.
        /// </param>
        void ShowAdPlaceholder(AdPlaceholderType adType, CrossPromotionEntry entry);

        /// <summary>
        /// Preloads cross-promotion assets for all configured formats into the cache so a later
        /// <see cref="ShowAdPlaceholder"/> renders instantly (load-then-show, like mediation ads).
        /// No-op when <paramref name="config"/> is null.
        /// </summary>
        /// <param name="config">The cross-promotion config whose per-format assets should be cached.</param>
        void PreloadAdPlaceholder(CrossPromotionConfig config);

        /// <summary>
        /// Closes and hides the currently displayed ad placeholder.
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
        /// Registers a callback invoked whenever the placeholder is dismissed (by the user, auto-close,
        /// or programmatically), so the caller can keep its shown/hidden state in sync.
        /// </summary>
        /// <param name="onClosed">Callback invoked after the placeholder is hidden.</param>
        void SetPlaceholderClosedCallback(System.Action onClosed);

        /// <summary>
        /// Registers a callback invoked when the user taps the placeholder asset (its click-through),
        /// so the caller can fire the ad-clicked event.
        /// </summary>
        /// <param name="onClicked">Callback invoked when the placeholder asset is tapped.</param>
        void SetPlaceholderClickedCallback(System.Action onClicked);

        /// <summary>
        /// Registers a callback invoked once the placeholder asset has actually rendered, so the caller
        /// can fire the ad-displayed event only when something is really on screen.
        /// </summary>
        /// <param name="onShown">Callback invoked after the asset renders.</param>
        void SetPlaceholderShownCallback(System.Action onShown);

        /// <summary>
        /// Registers a callback invoked when the placeholder asset could not be loaded/shown (not ready,
        /// offline with no cache, etc.), so the caller can report it as no-ad-available.
        /// </summary>
        /// <param name="onFailed">Callback invoked when the asset fails to load.</param>
        void SetPlaceholderFailedCallback(System.Action onFailed);
    }
}
