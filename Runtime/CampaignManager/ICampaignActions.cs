namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Narrow contract the renderer uses to fire a node's action without referencing the
    /// concrete dispatcher. Implemented by <see cref="CampaignActionDispatcher"/>.
    /// </summary>
    public interface ICampaignActions
    {
        /// <summary>
        /// Executes <paramref name="action"/> in the context of <paramref name="campaign"/>.
        /// Never throws; an unrecognised or malformed action is logged and ignored.
        /// </summary>
        void Dispatch(CampaignAction action, CampaignItem campaign);
    }
}
