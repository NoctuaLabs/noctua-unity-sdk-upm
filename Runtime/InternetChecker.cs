using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

namespace com.noctuagames.sdk
{
    public static class InternetChecker
    {
        private static readonly string pingUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/ping";

        public static void CheckInternetConnection(Action<bool> onResult)
        {
            GameObject tempObj = new GameObject("InternetChecker");
            InternetCheckHelper helper = tempObj.AddComponent<InternetCheckHelper>();
            helper.StartCheck(onResult);
        }

        private class InternetCheckHelper : MonoBehaviour
        {
            private Action<bool> _onResult;

            public void StartCheck(Action<bool> onResult)
            {
                _onResult = onResult;
                StartCoroutine(CheckConnectionCoroutine());
            }

            private IEnumerator CheckConnectionCoroutine()
            {
                UnityWebRequest request = UnityWebRequest.Get(pingUrl);
                request.timeout = 5;

                yield return request.SendWebRequest(); // <-- OUTSIDE try

                bool isConnected = false;

                try
                {
                    isConnected = request.result == UnityWebRequest.Result.Success;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[InternetChecker] Exception checking request result: {ex}");
                    isConnected = false;
                }
                finally
                {
                    request.Dispose();
                }

                _onResult?.Invoke(isConnected);

                if (this != null && gameObject != null)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
