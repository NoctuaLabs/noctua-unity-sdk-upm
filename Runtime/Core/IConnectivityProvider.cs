using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides connectivity status and SDK lifecycle checks without
    /// depending on the Noctua static singleton.
    /// </summary>
    public interface IConnectivityProvider
    {
        /// <summary>
        /// Checks whether the device is currently offline by pinging the server.
        /// </summary>
        /// <returns><c>true</c> if the device has no internet connectivity; otherwise <c>false</c>.</returns>
        UniTask<bool> IsOfflineAsync();

        /// <summary>
        /// Checks whether the SDK has completed initialization.
        /// </summary>
        /// <returns><c>true</c> if the SDK is fully initialized; otherwise <c>false</c>.</returns>
        bool IsInitialized();

        /// <summary>
        /// Waits for the SDK initialization to complete asynchronously.
        /// </summary>
        UniTask InitAsync();
    }
}
