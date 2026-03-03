using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides a lightweight asynchronous internet connectivity check by pinging the Noctua API.
    /// </summary>
    public static class InternetChecker
    {
        private static readonly ILogger _sLog = new NoctuaLogger(typeof(InternetChecker));
        private static readonly string pingUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/ping";
        private static bool _isQuitting = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterQuitHandler()
        {
            Application.quitting += () => _isQuitting = true;
        }

        /// <summary>
        /// Sends an HTTP GET request to the Noctua ping endpoint to determine internet connectivity.
        /// Must be called from the main thread. Skips the check if the application is quitting.
        /// </summary>
        /// <param name="onResult">Callback invoked with <c>true</c> if the ping succeeds, <c>false</c> otherwise.</param>
        /// <param name="timeoutSeconds">Maximum seconds to wait for a response before treating as offline. Defaults to 5.</param>
        public static async UniTask CheckInternetConnectionAsync(Action<bool> onResult, int timeoutSeconds = 5)
        {
            if (_isQuitting || !Application.isPlaying)
            {
                _sLog.Warning("[InternetChecker] Skipped check: application is quitting or not playing.");
                return;
            }

            try
            {
                await UniTask.SwitchToMainThread(); // UnityWebRequest must be on Unity thread

                using var request = UnityWebRequest.Get(pingUrl);
                request.timeout = timeoutSeconds;

                await request.SendWebRequest();

                if (_isQuitting)
                {
                    _sLog.Warning("[InternetChecker] App is quitting. Ignoring result.");
                    return;
                }

                bool isConnected = request.result == UnityWebRequest.Result.Success;
                onResult?.Invoke(isConnected);
            }
            catch (Exception e)
            {
                _sLog.Warning($"[InternetChecker] Exception: {e.Message}");
                onResult?.Invoke(false);
            }
        }
    }
}
