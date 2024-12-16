using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        private const string PlayerPrefsKeyLocaleCountry = "NoctuaLocaleCountry";
        private const string PlayerPrefsKeyLocaleCurrency = "NoctuaLocaleCurrency";
        private readonly ImmutableDictionary<SystemLanguage, string> _languageMapping =
            new Dictionary<SystemLanguage, string>
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
            }.ToImmutableDictionary();

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaLocale));
        private readonly Dictionary<string,string> _translations;
        private readonly Dictionary<string,string> _defaultTranslations;

        public NoctuaLocale(string region = "")
        {
            if (!string.IsNullOrEmpty(region))
            {
                _region = region;
            }
            
            _translations = Utility.LoadTranslations(GetLanguage());
            _defaultTranslations = Utility.LoadTranslations("en");
        }

        public string GetLanguage()
        {
            var language = "en";

            // Determine by this priority: user pref, region, system

            // 1. Get from user profiles first.
            if (PlayerPrefs.HasKey(PlayerPrefsKeyUserPrefsLanguage))
            {
                var userPrefsLanguage = PlayerPrefs.GetString(PlayerPrefsKeyUserPrefsLanguage, "");

                if (!string.IsNullOrEmpty(userPrefsLanguage))
                {
                    _log.Debug("GetLanguage: using language from user preferences");
                    language = userPrefsLanguage;
                    return language;
                }
            } else {
                _log.Debug("GetLanguage: PlayerPrefsKeyUserPrefsLanguage is empty");
            }

            // 2. Get from region
            if (!string.IsNullOrEmpty(_region))
            {
                // Region to language mapping
                // Region code is using Alpha-2,
                // meanwhile the language code is using ISO 639-1.
                //
                // Use if else to support early return
                switch (_region.ToLower())
                {
                    case "th":
                        _log.Debug("GetLanguage: using language by region: " + _region);
                        language = "th";
                        return language;

                    case "vn":
                        _log.Debug("GetLanguage: using language by region: " + _region);
                        language = "vi";
                        return language;

                    default: _log.Debug("GetLanguage: no language mapping for this region: " + _region);

                        break;
                }
            }

            // 3. Fallback to system language
            _log.Debug("GetLanguage: using language by system language");

            return _languageMapping.GetValueOrDefault(Application.systemLanguage, "en");
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
        
        public string GetTranslation(string key)
        {
            _translations.TryGetValue(key, out var translation);

            if (translation is not null) return translation;

            _log.Warning($"Translation for key '{key}' not found");
                
            return _defaultTranslations.GetValueOrDefault(key, key);
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}
