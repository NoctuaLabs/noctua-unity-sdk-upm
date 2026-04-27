using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Delegates connectivity/lifecycle queries to the <see cref="Noctua"/> static facade.
    /// Lives in the View layer (composition root). Only called at runtime (PurchaseItemAsync),
    /// never during construction — safe from Lazy re-entry.
    /// </summary>
    internal class NoctuaConnectivityProvider : IConnectivityProvider
    {
        public UniTask<bool> IsOfflineAsync() => Noctua.IsOfflineAsync();
        public bool IsInitialized() => Noctua.IsInitialized();
        public UniTask InitAsync() => Noctua.InitAsync();
    }
}
