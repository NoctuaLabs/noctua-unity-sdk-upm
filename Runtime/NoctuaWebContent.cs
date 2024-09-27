#undef UNITY_EDITOR

using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    internal class WebContentDetails
    {
        [JsonProperty("redirectType")] public string RedirectType;
        [JsonProperty("url")] public string Url;
    }
    
    public class NoctuaWebContent
    {
        private readonly NoctuaWebContentConfig _config;
        private readonly AccessTokenProvider _accessTokenProvider;

        internal NoctuaWebContent(NoctuaWebContentConfig config, AccessTokenProvider accessTokenProvider)
        {
            _config = config ?? throw new System.ArgumentNullException(nameof(config));
            _accessTokenProvider = accessTokenProvider ?? throw new System.ArgumentNullException(nameof(accessTokenProvider));
        }
        
        public async UniTask ShowAnnouncement()
        {
            if (string.IsNullOrEmpty(_config.AnnouncementBaseUrl))
            {
                throw new System.ArgumentNullException(nameof(_config.AnnouncementBaseUrl));
            }
            
            var webContentDetails = await GetWebContentDetails(_config.AnnouncementBaseUrl);
            
            await OpenUrlInWebView(webContentDetails.Url);
        }

        public async UniTask ShowReward()
        {
            if (string.IsNullOrEmpty(_config.RewardBaseUrl))
            {
                throw new System.ArgumentNullException(nameof(_config.RewardBaseUrl));
            }
            
            var webContentDetails = await GetWebContentDetails(_config.RewardBaseUrl);
            
            await OpenUrlInWebView(webContentDetails.Url);
        }
        
        public async UniTask ShowCustomerService()
        {
            if (string.IsNullOrEmpty(_config.CustomerServiceBaseUrl))
            {
                throw new System.ArgumentNullException(nameof(_config.CustomerServiceBaseUrl));
            }
            
            var webContentDetails = await GetWebContentDetails(_config.CustomerServiceBaseUrl);
            
            await OpenUrlInWebView(webContentDetails.Url);
        }
        
        private async UniTask<WebContentDetails> GetWebContentDetails(string url)
        {
            var request = new HttpRequest(HttpMethod.Get, url)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken);
            
            return await request.Send<WebContentDetails>();
        }

        private async UniTask OpenUrlInWebView(string url)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR


            var gameObject = new GameObject("SocialLoginWebView");
            var uniWebView = gameObject.AddComponent<UniWebView>();

            if (Application.platform == RuntimePlatform.Android)
            {
                uniWebView.SetUserAgent(
                    "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Mobile Safari/537.3"
                );
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                uniWebView.SetUserAgent(
                    "Mozilla/5.0 (iPhone; CPU iPhone OS 14_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1"
                );
            }

            var tcs = new UniTaskCompletionSource();

            void PageClosed(UniWebView webView, string windowId)
            {
                Debug.Log("NoctuaWebContent: Page closed");
                tcs.TrySetResult();
            }

            bool ShouldClose(UniWebView webView)
            {
                Debug.Log("NoctuaWebContent: Should close");

                tcs.TrySetResult();

                return true;
            }

            void PageFinished(UniWebView webView, int statusCode, string url)
            {
                Debug.Log($"Page finished: {url}");
            }

            uniWebView.OnPageFinished += PageFinished;
            uniWebView.OnMultipleWindowClosed += PageClosed;
            uniWebView.OnShouldClose += ShouldClose;

            uniWebView.SetBackButtonEnabled(true);
            uniWebView.EmbeddedToolbar.Show();
            uniWebView.EmbeddedToolbar.SetDoneButtonText("Close");
            uniWebView.EmbeddedToolbar.SetPosition(UniWebViewToolbarPosition.Top);

            uniWebView.Frame = new Rect(
                Screen.width  * 0.15f,
                Screen.height * 0.15f,
                Screen.width  * 0.7f,
                Screen.height * 0.7f
            );

            Debug.Log("NoctuaWebPaymentService: Showing WebView");
            uniWebView.Show();
            uniWebView.Load(url);

            try
            {
                await tcs.Task;
            }
            finally
            {
                Debug.Log("NoctuaWebPaymentService: Closing WebView");
                uniWebView.Hide();
                uniWebView.OnPageFinished -= PageFinished;
                uniWebView.OnMultipleWindowClosed -= PageClosed;
                uniWebView.OnShouldClose -= ShouldClose;

                Object.Destroy(gameObject);
            }
#else
            throw new NoctuaException(NoctuaErrorCode.Application, "Web payment is not supported in this platform");
#endif
        }
    }

    internal class NoctuaWebContentConfig
    {
        public string AnnouncementBaseUrl;

        public string RewardBaseUrl;

        public string CustomerServiceBaseUrl;
    }
}