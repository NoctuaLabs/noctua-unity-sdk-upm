using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Application = UnityEngine.Device.Application;
using SystemInfo = UnityEngine.Device.SystemInfo;
using Newtonsoft.Json;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    public class NoctuaLocale
    {
        private readonly string _region;
        private const string PlayerPrefsKeyUserPrefsLanguage = "NoctuaLocaleUserPrefsLanguage";
        private string PlayerPrefsKeyLocaleCountry = "NoctuaLocaleCountry";
        private string PlayerPrefsKeyLocaleCurrency = "NoctuaLocaleCurrency";

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaLocale));

        public NoctuaLocale(string region = "")
        {
            if (!string.IsNullOrEmpty(region))
            {
                _region = region;
            }
        }

        public string GetLanguage()
        {
            var language = "en";

            // 2. Get from region
            if (!string.IsNullOrEmpty(_region))
            {
                // Region to language mapping
                // Region code is using Alpha-2,
                // meanwhile the language code is using ISO 639-1.
                //
                // Use if else to support early return
                var _regionToLower = _region.ToLower();
                if (_regionToLower == "th")
                {
                    _log.Debug("GetLanguage: using language by region: " + _region);
                    language = "th";
                    return language;
                } else if (_regionToLower == "vn")
                {
                    _log.Debug("GetLanguage: using language by region: " + _region);
                    language = "vi";
                    return language;
                } else {
                    _log.Debug("GetLanguage: no language mapping for this region: " + _region);
                }
            }

            // 3. Fallback to system language
            _log.Debug("GetLanguage: using language by system language");
            var languageMapping = new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.Afrikaans, "af" },
                { SystemLanguage.Arabic, "ar" },
                { SystemLanguage.Basque, "eu" },
                { SystemLanguage.Belarusian, "be" },
                { SystemLanguage.Bulgarian, "bg" },
                { SystemLanguage.Catalan, "ca" },
                { SystemLanguage.Chinese, "zh" },
                { SystemLanguage.Czech, "cs" },
                { SystemLanguage.Danish, "da" },
                { SystemLanguage.Dutch, "nl" },
                { SystemLanguage.English, "en" },
                { SystemLanguage.Estonian, "et" },
                { SystemLanguage.Faroese, "fo" },
                { SystemLanguage.Finnish, "fi" },
                { SystemLanguage.French, "fr" },
                { SystemLanguage.German, "de" },
                { SystemLanguage.Greek, "el" },
                { SystemLanguage.Hebrew, "he" },
                { SystemLanguage.Hungarian, "hu" },
                { SystemLanguage.Icelandic, "is" },
                { SystemLanguage.Indonesian, "id" },
                { SystemLanguage.Italian, "it" },
                { SystemLanguage.Japanese, "ja" },
                { SystemLanguage.Korean, "ko" },
                { SystemLanguage.Latvian, "lv" },
                { SystemLanguage.Lithuanian, "lt" },
                { SystemLanguage.Norwegian, "no" },
                { SystemLanguage.Polish, "pl" },
                { SystemLanguage.Portuguese, "pt" },
                { SystemLanguage.Romanian, "ro" },
                { SystemLanguage.Russian, "ru" },
                { SystemLanguage.SerboCroatian, "sh" },
                { SystemLanguage.Slovak, "sk" },
                { SystemLanguage.Slovenian, "sl" },
                { SystemLanguage.Spanish, "es" },
                { SystemLanguage.Swedish, "sv" },
                { SystemLanguage.Thai, "th" },
                { SystemLanguage.Turkish, "tr" },
                { SystemLanguage.Ukrainian, "uk" },
                { SystemLanguage.Vietnamese, "vi" },
                { SystemLanguage.ChineseSimplified, "zh-CN" },
                { SystemLanguage.ChineseTraditional, "zh-TW" },
                { SystemLanguage.Unknown, "en" }
            };

            return languageMapping.TryGetValue(Application.systemLanguage, out var code) ? code : "en";
        }

        public void SetCountry(string country)
        {
            country = country.ToUpper();
            PlayerPrefs.SetString(PlayerPrefsKeyLocaleCountry, country);
        }

        public void SetUserPrefsLanguage(string language)
        {
	        // Update user preference language
	        if (!string.IsNullOrEmpty(language))
	        {
		        PlayerPrefs.SetString(PlayerPrefsKeyUserPrefsLanguage, language);
	        } else {
		        PlayerPrefs.DeleteKey(PlayerPrefsKeyUserPrefsLanguage);
	        }
        }

        public void SetCurrency(string currency)
        {
            currency = currency.ToUpper();
            PlayerPrefs.SetString(PlayerPrefsKeyLocaleCurrency, currency);
        }

        public string GetCountry()
        {
            return PlayerPrefs.GetString(PlayerPrefsKeyLocaleCountry, "ID"); // Default to Indonesia
        }

        public string GetCurrency()
        {
            return PlayerPrefs.GetString(PlayerPrefsKeyLocaleCurrency, "USD"); // Default to USD
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}
