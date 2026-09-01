using System;
using UnityEngine.TextCore.Text;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Narrow custom-font contract the renderer depends on. Implemented by
    /// <see cref="CampaignFontSource"/>; the renderer passes a <c>style.fontFamily</c> key and
    /// the source owns the registry → URL → download → <see cref="FontAsset"/> pipeline. Tests
    /// can pass <c>null</c> (text then keeps the panel's default font) or a fake.
    /// </summary>
    public interface ICampaignFontSource
    {
        /// <summary>
        /// Resolves the registry key <paramref name="family"/> and invokes
        /// <paramref name="onLoaded"/> on the main thread with the built font, or <c>null</c>
        /// when the name is unknown or the load fails / is skipped offline. Never throws.
        /// </summary>
        void GetFont(string family, Action<FontAsset> onLoaded);
    }
}
