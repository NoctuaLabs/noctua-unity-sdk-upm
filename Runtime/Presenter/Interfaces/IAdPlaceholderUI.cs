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
        /// Displays a placeholder ad of the specified type (banner, interstitial, or rewarded).
        /// </summary>
        /// <param name="adType">The type of ad placeholder to display.</param>
        void ShowAdPlaceholder(AdPlaceholderType adType);

        /// <summary>
        /// Closes and hides the currently displayed ad placeholder.
        /// </summary>
        void CloseAdPlaceholder();
    }
}
