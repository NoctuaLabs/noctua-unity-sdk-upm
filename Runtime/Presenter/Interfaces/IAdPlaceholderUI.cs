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
        void ShowAdPlaceholder(AdPlaceholderType adType);
        void CloseAdPlaceholder();
    }
}
