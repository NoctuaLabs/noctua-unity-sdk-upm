using System;
using UnityEngine.TextCore.Text;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Narrow custom-font contract the renderer depends on. Implemented by
    /// <see cref="CampaignFontSource"/>; the renderer resolves <c>style.fontFamily</c> to a
    /// <c>Resources</c> path (against the campaign item's registry, the shared payload registry,
    /// or the value itself) and this source owns the path → <see cref="FontAsset"/> pipeline.
    /// Tests can pass <c>null</c> (text then keeps the panel's default font) or a fake.
    /// </summary>
    public interface ICampaignFontSource
    {
        /// <summary>
        /// Loads the Font Asset at <paramref name="resourcesPath"/> and invokes
        /// <paramref name="onLoaded"/> on the main thread with it, or <c>null</c> when the path
        /// holds no usable font. Results are cached by path. Never throws.
        /// </summary>
        void GetFont(string resourcesPath, Action<FontAsset> onLoaded);
    }
}
