using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>Mirrors com.noctuagames.sdk.models.ConsumableType (Android) / ConsumableType (iOS)</summary>
    [Preserve]
    public enum NoctuaConsumableType
    {
        Consumable = 0,
        NonConsumable = 1,
        Subscription = 2
    }
}
