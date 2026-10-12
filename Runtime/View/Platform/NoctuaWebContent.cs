using System;
using System.Globalization;
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

    /// <summary>
    /// Model representing the state of web content to be displayed in the web view presenter.
    /// </summary>
    internal class WebContentModel
    {
        /// <summary>The resolved URL of the web content to display.</summary>
        public string Url;
        /// <summary>The display mode (windowed or full-screen) for the web content.</summary>
        public ScreenMode ScreenMode;
        /// <summary>The title shown in the web view header.</summary>
        public string Title;
        /// <summary>The timestamp when this content was last shown, used for cooldown logic.</summary>
        public DateTime? LastShown;
    }
    
    /// <summary>
    /// Provides access to various web-based content within the Noctua platform,
    /// including announcements, rewards, customer service, and social media.
    /// </summary>
    /// <remarks>
    /// This class handles fetching URLs, authentication checks, and displaying content in
    /// a native web view through the <see cref="WebContentPresenter"/>.
    /// </remarks>
    /// <example>
    /// Example usage:
    /// <code>
    /// // Show latest announcement
    /// await Noctua.Platform.Content.ShowAnnouncement();
    ///
    /// // Open the reward center
    /// await Noctua.Platform.Content.ShowReward();
    ///
    /// // Contact customer support
    /// await Noctua.Platform.Content.ShowCustomerService();
    /// </code>
    /// </example>
    public class NoctuaWebContent
    {
        private readonly NoctuaLogger _log = new(typeof(NoctuaWebContent));
        private readonly NoctuaWebContentConfig _config;
        private readonly AccessTokenProvider _accessTokenProvider;
        private readonly WebContentModel _webContent = new();

        private readonly UIFactory _uiFactory;
        private readonly WebContentPresenter _webView;
        private readonly IEventSender _eventSender;

        internal NoctuaWebContent(
            NoctuaWebContentConfig config,
            AccessTokenProvider accessTokenProvider,
            UIFactory uiFactory,
            IEventSender eventSender = null
        )
        {
            _uiFactory = uiFactory;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
            _webView = uiFactory.Create<WebContentPresenter, WebContentModel>(_webContent);
            _eventSender = eventSender;
        }

        /// <summary>
        /// Displays the in-game announcements section in a windowed web view.
        /// </summary>
        /// <returns>True if the announcement was displayed, false if skipped due to cooldown or missing data.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the announcement base URL is not configured.</exception>
        public async UniTask<bool> ShowAnnouncement()
        {
            await EnsureOnlineAsync();

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
                }
                else
                {
                    _uiFactory.ShowError(e.Message);
                }

                throw;
            }
            finally
            {
                _uiFactory.ShowLoadingProgress(false);
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                _log.Warning("Url is Empty");
                return false;
            }

            _webContent.Url = baseUrl;
            _webContent.ScreenMode = ScreenMode.Windowed;
            _webContent.Title = "Announcement";

            var strLastShown = PlayerPrefs.GetString("NoctuaWebContent.Announcement.LastShown", "");
            _webContent.LastShown = DateTime.TryParse(strLastShown, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastShown)
                ? lastShown.ToUniversalTime()
                : default;

            if (DateTime.UtcNow < _webContent.LastShown.Value.Add(TimeSpan.FromDays(1)))
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

        /// <summary>
        /// Displays the rewards page in a full-screen web view.
        /// </summary>
        /// <remarks>
        /// Typically used to show daily rewards, event gifts, or promotional bonuses for players.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if the reward base URL is not configured.</exception>
        public async UniTask ShowReward()
        {
            await EnsureOnlineAsync();

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
                }
                else
                {
                    _uiFactory.ShowError(e.Message);
                }

                throw;
            }
            finally
            {
                _uiFactory.ShowLoadingProgress(false);
            }


            if (string.IsNullOrEmpty(baseUrl))
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

        /// <summary>
        /// Opens the customer service web page in a full-screen view.
        /// </summary>
        /// <param name="reason">Optional reason string for contacting customer support.</param>
        /// <param name="context">Optional context (such as screen or event name) to pass to the support form.</param>
        /// <remarks>
        /// This feature allows players to reach customer support directly through the SDK UI.
        /// </remarks>
        public async UniTask ShowCustomerService(string reason = "general", string context = "")
        {
            await EnsureOnlineAsync();

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
                }
                else
                {
                    _uiFactory.ShowError(e.Message);
                }

                throw;
            }
            finally
            {
                _uiFactory.ShowLoadingProgress(false);
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                _log.Warning("Url is Empty");
                return;
            }

            _eventSender?.Send("customer_service_opened");

            var escapedReason = Uri.EscapeDataString(reason ?? "general");

            baseUrl = baseUrl.Contains("reason=general")
                ? baseUrl.Replace("reason=general", $"reason={escapedReason}")
                : AppendQueryParam(baseUrl, "reason", escapedReason);

            if (!string.IsNullOrEmpty(context))
            {
                baseUrl = AppendQueryParam(baseUrl, "context", Uri.EscapeDataString(context));
            }

            _webContent.Url = baseUrl;
            _webContent.ScreenMode = ScreenMode.FullScreen;
            _webContent.Title = "Customer Service";
            _webContent.LastShown = null;

            await _webView.OpenAsync();
        }

        /// <summary>
        /// Retrieves web content metadata from a given URL.
        /// </summary>
        /// <param name="url">The endpoint URL to request metadata from.</param>
        /// <returns>A <see cref="WebContentUrl"/> object containing the URL details.</returns>
        private async UniTask<WebContentUrl> GetWebContentDetails(string url)
        {
            var request = new HttpRequest(HttpMethod.Get, url)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken);

            return await request.Send<WebContentUrl>();
        }

        /// <summary>
        /// Appends <c>name=value</c> to a URL, using <c>?</c> or <c>&amp;</c> as appropriate.
        /// The value must already be escaped.
        /// </summary>
        private static string AppendQueryParam(string url, string name, string escapedValue)
        {
            var separator = url.Contains("?") ? "&" : "?";

            return $"{url}{separator}{name}={escapedValue}";
        }

        /// <summary>
        /// Makes sure the SDK is online and initialized before loading web content.
        /// While it is not, shows a retry dialog and checks again each time the player retries.
        /// </summary>
        /// <exception cref="NoctuaException">Thrown when the player dismisses the retry dialog.</exception>
        private async UniTask EnsureOnlineAsync()
        {
            var offlineModeMessage = Noctua.Platform.Locale.GetTranslation(LocaleTextKey.OfflineModeMessage) + "[PlatformContent]";

            while (true)
            {
                _uiFactory.ShowLoadingProgress(true);

                var isOffline = await Noctua.IsOfflineAsync();
                var failureMessage = isOffline ? offlineModeMessage : null;

                if (!isOffline && !Noctua.IsInitialized())
                {
                    try
                    {
                        await Noctua.InitAsync();
                        await Noctua.Auth.AuthenticateAsync();
                    }
                    catch (Exception e)
                    {
                        _log.Warning($"Init before showing web content failed: {e.Message}");
                        failureMessage = e.Message;
                    }
                }

                _uiFactory.ShowLoadingProgress(false);

                if (failureMessage == null)
                {
                    return;
                }

                var isRetry = await _uiFactory.ShowRetryDialog(offlineModeMessage, "offlineMode");

                if (!isRetry)
                {
                    throw new NoctuaException(NoctuaErrorCode.Authentication, failureMessage);
                }
            }
        }

        /// <summary>
        /// Displays the official social media page for the current game or region.
        /// </summary>
        /// <returns>True if the page was successfully displayed; false otherwise.</returns>
        public async UniTask<bool> ShowSocialMedia()
        {
            await EnsureOnlineAsync();

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
                }
                else
                {
                    _uiFactory.ShowError(e.Message);
                }

                throw;
            }
            finally
            {
                _uiFactory.ShowLoadingProgress(false);
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                _log.Warning("Url is Empty");
                return false;
            }

            _webContent.Url = baseUrl;
            _webContent.ScreenMode = ScreenMode.FullScreen;
            _webContent.Title = "Social Media";
            _webContent.LastShown = null;

            _eventSender?.Send("platform_content_social_media_opened");

            await _webView.OpenAsync();

            return true;
        }
    }

    /// <summary>
    /// Configuration holding base URLs for the various Noctua web content endpoints.
    /// </summary>
    internal class NoctuaWebContentConfig
    {
        /// <summary>Base URL for fetching in-game announcement content.</summary>
        public string AnnouncementBaseUrl;

        /// <summary>Base URL for fetching reward center content.</summary>
        public string RewardBaseUrl;

        /// <summary>Base URL for fetching customer service content.</summary>
        public string CustomerServiceBaseUrl;

        /// <summary>Base URL for fetching social media content.</summary>
        public string SocialMediaBaseUrl;
    }
}
