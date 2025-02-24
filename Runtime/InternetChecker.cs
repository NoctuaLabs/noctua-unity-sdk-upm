using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

namespace com.noctuagames.sdk
{
    public static class InternetChecker
    {
        private static string pingUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/ping";
        /// <summary>
        /// Checks if the internet is available by sending a web request.
        /// </summary>
        /// <param name="onResult">Callback with a boolean result (true if internet is available, false otherwise).</param>
        public static void CheckInternetConnection(System.Action<bool> onResult)
        {
            GameObject tempObj = new GameObject("InternetChecker");
            InternetCheckHelper helper = tempObj.AddComponent<InternetCheckHelper>();
            helper.StartCoroutine(helper.CheckConnectionCoroutine(onResult));
        }

        private class InternetCheckHelper : MonoBehaviour
        {
            public IEnumerator CheckConnectionCoroutine(System.Action<bool> onResult)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(pingUrl))
                {
                    request.timeout = 5;
                    yield return request.SendWebRequest();

                    bool isConnected = request.result == UnityWebRequest.Result.Success;
                    onResult?.Invoke(isConnected);
                }
                Destroy(gameObject); // Clean up after checking
            }
        }
    }
}