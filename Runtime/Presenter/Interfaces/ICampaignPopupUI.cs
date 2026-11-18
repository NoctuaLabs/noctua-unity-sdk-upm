using System;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// The campaign popup as the live ops logic sees it, so <see cref="NoctuaLiveOpsCampaign"/>
    /// (Presenter) does not reference UI types. Implemented by <c>CampaignUIHost</c>.
    /// </summary>
    public interface ICampaignPopupUI
    {
        /// <summary>True while a campaign is on screen.</summary>
        bool IsShowing { get; }

        /// <summary>Sets what runs when the next campaign is shown, closed, or fails to render.</summary>
        void SetCallbacks(Action<CampaignItem> onShown, Action onClosed, Action onFailed);

        /// <summary>Shows <paramref name="item"/>, replacing any campaign on screen.</summary>
        void Show(CampaignItem item, int configSchemaVersion);

        /// <summary>Re-renders the campaign on screen in place. False when nothing is showing or the render failed.</summary>
        bool Refresh(CampaignItem item, int configSchemaVersion);

        /// <summary>Veils the popup with a spinner while a purchase or claim runs.</summary>
        void SetBusy(bool busy);

        /// <summary>Closes the popup if one is showing.</summary>
        void Close();
    }
}
