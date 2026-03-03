using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>Mirrors com.noctuagames.sdk.models.ConsumableType (Android) / ConsumableType (iOS)</summary>
    [Preserve]
    public enum NoctuaConsumableType
    {
        /// <summary>A consumable product that can be purchased multiple times.</summary>
        Consumable = 0,
        /// <summary>A non-consumable product that is purchased once and owned permanently.</summary>
        NonConsumable = 1,
        /// <summary>A subscription product with recurring billing.</summary>
        Subscription = 2
    }
}
