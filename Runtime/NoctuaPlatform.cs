﻿using com.noctuagames.sdk.UI;

namespace com.noctuagames.sdk
{
    public class NoctuaPlatform
    {
        public readonly NoctuaLocale Locale;

        public readonly NoctuaWebContent Content;
        
        internal NoctuaPlatform(NoctuaConfig config, AccessTokenProvider accessTokenProvider, UIFactory uiFactory)
        {
            Locale = new NoctuaLocale();
            Content = new NoctuaWebContent(
                new NoctuaWebContentConfig
                {
                    AnnouncementBaseUrl = config.AnnouncementBaseUrl,
                    RewardBaseUrl = config.RewardBaseUrl,
                    CustomerServiceBaseUrl = config.CustomerServiceBaseUrl
                },
                accessTokenProvider,
                uiFactory
            );
        }
    }
}