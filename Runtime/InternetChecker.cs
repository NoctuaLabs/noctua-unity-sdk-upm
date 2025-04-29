using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace com.noctuagames.sdk
{
    public static class InternetChecker
    {
        private static readonly string pingUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/ping";
        private static bool _isQuitting = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterQuitHandler()
        {
            Application.quitting += () => _isQuitting = true;
        }

        public static async UniTask CheckInternetConnectionAsync(Action<bool> onResult, int timeoutSeconds = 5)
        {
            if (_isQuitting || !Application.isPlaying)
            {
                Debug.LogWarning("[InternetChecker] Skipped check: application is quitting or not playing.");
                // onResult?.Invoke(false);
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
                    Debug.LogWarning("[InternetChecker] App is quitting. Ignoring result.");
                    return;
                }

                bool isConnected = request.result == UnityWebRequest.Result.Success;
                onResult?.Invoke(isConnected);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InternetChecker] Exception: {e.Message}");
                onResult?.Invoke(false);
            }
        }
    }
}
