using System;
using com.noctuagames.sdk.Events;
using com.noctuagames.sdk.UI;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
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
        private readonly NoctuaLogger _log = new(typeof(NoctuaWebContent));
        private readonly NoctuaWebContentConfig _config;
        private readonly AccessTokenProvider _accessTokenProvider;
        private readonly WebContentModel _webContent = new();

        private readonly UIFactory _uiFactory;
        private readonly WebContentPresenter _webView;
        private readonly EventSender _eventSender;

        internal NoctuaWebContent(
            NoctuaWebContentConfig config,
            AccessTokenProvider accessTokenProvider,
            UIFactory uiFactory,
            EventSender eventSender = null
        )
        {
            _uiFactory = uiFactory;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
            _webView = uiFactory.Create<WebContentPresenter, WebContentModel>(_webContent);
            _eventSender = eventSender;
        }
        
        public async UniTask<bool> ShowAnnouncement()
        {
            await OfflineModeHandler(async () => await ShowAnnouncement());

            _log.Debug("calling API");
            
            if (string.IsNullOrEmpty(_config.AnnouncementBaseUrl))
            {
                throw new ArgumentNullException(nameof(_config.AnnouncementBaseUrl));
            }
            
            var baseUrl = "";
            _uiFactory.ShowLoadingProgress(true);

            try
            {
                var details = await GetWebContentDetails(_config.AnnouncementBaseUrl);
                baseUrl = details.Url;
            }
            catch (Exception e)
            {
                _uiFactory.ShowLoadingProgress(false);

                if (e.Message.Contains("Networking"))
                {
                    _uiFactory.ShowError("Failed to load the contents. Please kindly check your connection and try again.");
                } else {
                    _uiFactory.ShowError(e.Message);
                }

                throw e;
            }
            finally
            {
                _uiFactory.ShowLoadingProgress(false);
            }

            if(string.IsNullOrEmpty(baseUrl))
            {
                _log.Warning("Url is Empty");
                return false;
            }
            
            _webContent.Url = baseUrl;
            _webContent.ScreenMode = ScreenMode.Windowed;
            _webContent.Title = "Announcement";
            
            var strLastShown = PlayerPrefs.GetString("NoctuaWebContent.Announcement.LastShown", "");
            _webContent.LastShown = DateTime.TryParse(strLastShown, out var lastShown) ? lastShown : default;
            
            if (DateTime.Now.ToUniversalTime() < _webContent.LastShown.Value.Add(TimeSpan.FromDays(1)))
            {
                _log.Info($"Web content already shown today on {_webContent.LastShown.Value.ToUniversalTime():O}");
                return false;
            }
            
            _eventSender?.Send("platform_content_announcement_opened");

            await _webView.OpenAsync();

            if (_webContent.LastShown != default)
            {
                PlayerPrefs.SetString("NoctuaWebContent.Announcement.LastShown", DateTime.Now.ToUniversalTime().ToString("O"));
            }
            
            return true;
        }

        public async UniTask ShowReward()
        {
            await OfflineModeHandler(async () => await ShowReward());

            _log.Debug("calling API");

            if (string.IsNullOrEmpty(_config.RewardBaseUrl))
            {
                throw new ArgumentNullException(nameof(_config.RewardBaseUrl));
            }
            
            var baseUrl = "";
            _uiFactory.ShowLoadingProgress(true);
            
            try
            {
                var details = await GetWebContentDetails(_config.RewardBaseUrl);
                baseUrl = details.Url;
            }
            catch (Exception e)
            {
                _uiFactory.ShowLoadingProgress(false);

                if (e.Message.Contains("Networking"))
                {
                    _uiFactory.ShowError("Failed to load the contents. Please kindly check your connection and try again.");
                } else {
                    _uiFactory.ShowError(e.Message);
                }

                throw e;
            }
            finally
            {
                _uiFactory.ShowLoadingProgress(false);
            }


            if(string.IsNullOrEmpty(baseUrl))
            {
                _log.Warning("Url is Empty");
                return;
            }
            
            _eventSender?.Send("platform_content_reward_opened");

            _webContent.Url = baseUrl;
            _webContent.ScreenMode = ScreenMode.FullScreen;
            _webContent.Title = "Reward";
            _webContent.LastShown = null;
            
            await _webView.OpenAsync();
        }
        
        public async UniTask ShowCustomerService(string reason = "general", string context = "")
        {
            await OfflineModeHandler(async () => await ShowCustomerService(reason, context));

            _log.Debug("calling API");

            if (string.IsNullOrEmpty(_config.CustomerServiceBaseUrl))
            {
                throw new ArgumentNullException(nameof(_config.CustomerServiceBaseUrl));
            }

            var baseUrl = "";
            _uiFactory.ShowLoadingProgress(true);
            
            try
            {
                var details = await GetWebContentDetails(_config.CustomerServiceBaseUrl);
                baseUrl = details.Url;
            }
            catch (Exception e)
            {
                _uiFactory.ShowLoadingProgress(false);

                if (e.Message.Contains("Networking"))
                {
                    _uiFactory.ShowError("Failed to load the contents. Please kindly check your connection and try again.");
                } else {
                    _uiFactory.ShowError(e.Message);
                }

                throw e;
            }
            finally
            {
                _uiFactory.ShowLoadingProgress(false);
            }

            if(string.IsNullOrEmpty(baseUrl))
            {
                _log.Warning("Url is Empty");
                return;
            }
            
            _eventSender?.Send("customer_service_opened");

            if (baseUrl.Contains("reason=general"))
            {
                // Replace existing
                baseUrl = baseUrl.Replace("reason=general", $"reason={reason}");
            } else {
                // Append new one
                baseUrl = baseUrl + $"&reason={reason}";
            }

            if (!string.IsNullOrEmpty(context))
            {
                baseUrl = baseUrl + $"&context={context}";
            }


            _webContent.Url = baseUrl;
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
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken);

            return await request.Send<WebContentUrl>();
        }

        private async UniTask OfflineModeHandler(Func<UniTask> retryFunction)
        {
            // Offline-first handler
            _uiFactory.ShowLoadingProgress(true);
            
            var offlineModeMessage = Noctua.Platform.Locale.GetTranslation(LocaleTextKey.IAPPurchaseOfflineModeMessage);
            var isOffline = await Noctua.IsOfflineModeAsync();

            if(!isOffline && !Noctua.IsInitialized())
            {
                try
                {
                    await Noctua.InitAsync();

                    await Noctua.Auth.AuthenticateAsync();

                } catch(Exception e)
                {
                    _uiFactory.ShowLoadingProgress(false);

                    await HandleRetryPopUpMessageAsync(offlineModeMessage, retryFunction);

                    throw new NoctuaException(NoctuaErrorCode.Authentication, $"{e.Message}");
                }
            }

            if (isOffline)
            {
                _uiFactory.ShowLoadingProgress(false);

                await HandleRetryPopUpMessageAsync(offlineModeMessage, retryFunction);

                throw new NoctuaException(NoctuaErrorCode.Authentication, offlineModeMessage);
            }

            _uiFactory.ShowLoadingProgress(false);
        }

        public async UniTask<bool> ShowSocialMedia()
        {
            await OfflineModeHandler(async () => await ShowSocialMedia());

            _log.Debug("calling API");

            if (string.IsNullOrEmpty(_config.SocialMediaBaseUrl))
            {
                throw new ArgumentNullException(nameof(_config.SocialMediaBaseUrl));
            }

            var baseUrl = "";
            _uiFactory.ShowLoadingProgress(true);

            try
            {
                var details = await GetWebContentDetails(_config.SocialMediaBaseUrl);
                baseUrl = details.Url;
            }
            catch (Exception e)
            {
                _uiFactory.ShowLoadingProgress(false);

                if (e.Message.Contains("Networking"))
                {
                    _uiFactory.ShowError("Failed to load the contents. Please kindly check your connection and try again.");
                } else {
                    _uiFactory.ShowError(e.Message);
                }

                throw e;
            }
            finally
            {
                _uiFactory.ShowLoadingProgress(false);
            }

            if(string.IsNullOrEmpty(baseUrl))
            {
                _log.Warning("Url is Empty");
                return false;
            }

            _webContent.Url = baseUrl;
            _webContent.ScreenMode = ScreenMode.FullScreen;
            _webContent.Title = "Social Media";

            _eventSender?.Send("platform_content_social_media_opened");

            await _webView.OpenAsync();

            return true;
        }


        private async UniTask HandleRetryPopUpMessageAsync(string offlineModeMessage, Func<UniTask> retryFunction) {
            bool isRetry = await _uiFactory.ShowRetryDialog(offlineModeMessage, "offlineMode");
            if(isRetry)
            {
                await retryFunction();                
            }
        }
    }

    internal class NoctuaWebContentConfig
    {
        public string AnnouncementBaseUrl;

        public string RewardBaseUrl;

        public string CustomerServiceBaseUrl;

        public string SocialMediaBaseUrl;
    }
}
