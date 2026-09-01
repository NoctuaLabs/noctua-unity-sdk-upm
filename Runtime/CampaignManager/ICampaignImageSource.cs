using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Narrow image-fetch contract the renderer depends on. Implemented by
    /// <see cref="CampaignAssetSource"/>; tests can pass <c>null</c> (image nodes then
    /// render empty) or a fake.
    /// </summary>
    public interface ICampaignImageSource
    {
        /// <summary>
        /// Resolves the texture for <paramref name="url"/> and invokes
        /// <paramref name="onLoaded"/> on the main thread with the texture, or <c>null</c>
        /// on failure. Never throws.
        /// </summary>
        void GetImage(string url, Action<Texture2D> onLoaded);

        /// <summary>Marks textures as in-use so the RAM cache won't evict them while a surface shows them.</summary>
        void Pin(IReadOnlyCollection<string> urls);

        /// <summary>Releases a previous <see cref="Pin"/> (called when the surface closes).</summary>
        void Unpin(IReadOnlyCollection<string> urls);
    }
}
