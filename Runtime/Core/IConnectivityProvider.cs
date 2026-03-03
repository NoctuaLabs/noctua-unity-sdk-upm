using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides connectivity status and SDK lifecycle checks without
    /// depending on the Noctua static singleton.
    /// </summary>
    public interface IConnectivityProvider
    {
        UniTask<bool> IsOfflineAsync();
        bool IsInitialized();
        UniTask InitAsync();
    }
}
