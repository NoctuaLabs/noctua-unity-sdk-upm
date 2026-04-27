using com.noctuagames.sdk.Events;
using com.noctuagames.sdk.UI;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Facade providing access to platform-level features: locale and web content.
    /// Accessed via <c>Noctua.Platform</c>.
    /// </summary>
    public class NoctuaPlatform
    {
        /// <summary>
        /// Locale service providing language, country, currency, and translation support.
        /// </summary>
        public readonly NoctuaLocale Locale;

        /// <summary>
        /// Web content service for announcements, rewards, customer service, and social media.
        /// </summary>
        public readonly NoctuaWebContent Content;

        internal NoctuaPlatform(
            NoctuaConfig config,
            AccessTokenProvider accessTokenProvider,
            UIFactory uiFactory,
            IEventSender eventSender = null
        )
        {
            Locale = new NoctuaLocale(config.Region);
            Content = new NoctuaWebContent(
                new NoctuaWebContentConfig
                {
                    AnnouncementBaseUrl = config.AnnouncementBaseUrl,
                    RewardBaseUrl = config.RewardBaseUrl,
                    CustomerServiceBaseUrl = config.CustomerServiceBaseUrl,
                    SocialMediaBaseUrl = config.SocialMediaBaseUrl,
                },
                accessTokenProvider,
                uiFactory,
                eventSender
            );
        }
    }
}
