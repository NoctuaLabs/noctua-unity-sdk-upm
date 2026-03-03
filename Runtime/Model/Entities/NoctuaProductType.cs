using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>Mirrors com.noctuagames.sdk.models.ProductType (Android) / ProductType (iOS)</summary>
    [Preserve]
    public enum NoctuaProductType
    {
        /// <summary>An in-app product (consumable or non-consumable).</summary>
        InApp = 0,
        /// <summary>A subscription product.</summary>
        Subs = 1
    }
}
