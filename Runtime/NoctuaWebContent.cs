using System;
using com.noctuagames.sdk.UI;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    internal class WebContentUrl
    {
        [JsonProperty("url")] public string Url;
    }
    
    internal class WebContentModel
    {
        public string Url;
        public ScreenMode ScreenMode;
        public string Title;
        public DateTime? LastShown;
    }
    
    public class NoctuaWebContent
    {
        private readonly NoctuaUnityDebugLogger _log = new();
        private readonly NoctuaWebContentConfig _config;
        private readonly AccessTokenProvider _accessTokenProvider;
        private readonly WebContentModel _webContent = new();
        private readonly WebContentPresenter _webView;

        internal NoctuaWebContent(NoctuaWebContentConfig config, AccessTokenProvider accessTokenProvider, UIFactory uiFactory)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
            
            _webView = uiFactory.Create<WebContentPresenter, WebContentModel>(_webContent);
        }
        
        public async UniTask<bool> ShowAnnouncement()
        {
            if (string.IsNullOrEmpty(_config.AnnouncementBaseUrl))
            {
                throw new ArgumentNullException(nameof(_config.AnnouncementBaseUrl));
            }
            
            var details = await GetWebContentDetails(_config.AnnouncementBaseUrl);
            
            _webContent.Url = details.Url;
            _webContent.ScreenMode = ScreenMode.Windowed;
            _webContent.Title = "Announcement";
            
            var strLastShown = PlayerPrefs.GetString("NoctuaWebContent.Announcement.LastShown", "");
            _webContent.LastShown = DateTime.TryParse(strLastShown, out var lastShown) ? lastShown : default;
            
            if (DateTime.Now.ToUniversalTime() < _webContent.LastShown.Value.Add(TimeSpan.FromDays(1)))
            {
                _log.Log($"Web content already shown today on {_webContent.LastShown.Value.ToUniversalTime():O}");
                return false;
            }

            
            await _webView.OpenAsync();

            if (_webContent.LastShown != default)
            {
                PlayerPrefs.SetString("NoctuaWebContent.Announcement.LastShown", DateTime.Now.ToUniversalTime().ToString("O"));
            }
            
            return true;
        }

        public async UniTask ShowReward()
        {
            if (string.IsNullOrEmpty(_config.RewardBaseUrl))
            {
                throw new ArgumentNullException(nameof(_config.RewardBaseUrl));
            }
            
            var details = await GetWebContentDetails(_config.RewardBaseUrl);
            
            _webContent.Url = details.Url;
            _webContent.ScreenMode = ScreenMode.FullScreen;
            _webContent.Title = "Reward";
            _webContent.LastShown = null;
            
            await _webView.OpenAsync();
        }
        
        public async UniTask ShowCustomerService()
        {
            if (string.IsNullOrEmpty(_config.CustomerServiceBaseUrl))
            {
                throw new ArgumentNullException(nameof(_config.CustomerServiceBaseUrl));
            }
            
            var details = await GetWebContentDetails(_config.CustomerServiceBaseUrl);
            
            _webContent.Url = details.Url;
            _webContent.ScreenMode = ScreenMode.FullScreen;
            _webContent.Title = "Customer Service";
            _webContent.LastShown = null;
            
            await _webView.OpenAsync();
        }
        
        private async UniTask<WebContentUrl> GetWebContentDetails(string url)
        {
            var request = new HttpRequest(HttpMethod.Get, url)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")


            try
            {
                request.WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken);
            }
            catch (Exception e)
            {
                // Do nothing. Backend will handle unauthenticated requests.
                // It either returns empty URL (if the request is not allowed)
                // or the URL if authentication is not required.
            }
            
            return await request.Send<WebContentUrl>();
        }
    }

    internal class NoctuaWebContentConfig
    {
        public string AnnouncementBaseUrl;

        public string RewardBaseUrl;

        public string CustomerServiceBaseUrl;
    }
}