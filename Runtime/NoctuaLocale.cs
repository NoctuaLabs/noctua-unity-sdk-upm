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
        public event Action<string> OnLanguageChanged;
        
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
        private Dictionary<string,string> _translations;
        private readonly Dictionary<string,string> _defaultTranslations;

        public NoctuaLocale(string region = "")
        {
            if (!string.IsNullOrEmpty(region))
            {
                _region = region;
            }
            
            _translations = Utility.LoadTranslations(GetLanguage());
            _defaultTranslations = Utility.LoadTranslations("en") ?? new Dictionary<string, string>();
        }

        public string GetLanguage()
        {
            // Determine by this priority: user pref, region, system

            // 1. Get from user profiles first.
            var userPrefsLanguage = PlayerPrefs.GetString(PlayerPrefsKeyUserPrefsLanguage, null);
            
            if (!string.IsNullOrEmpty(userPrefsLanguage))
            {
                _log.Debug("Using language from user preferences: " + userPrefsLanguage);
                    
                return userPrefsLanguage;
            }

            _log.Debug("PlayerPrefsKeyUserPrefsLanguage is empty");

            // 2. Get from region
            // Region to language mapping
            // Region code is using Alpha-2,
            // meanwhile the language code is using ISO 639-1.
            var language = "en";
            switch (_region?.ToLower())
            {
                case "th":
                    _log.Debug("using language by region: " + _region);
                    language = "th";
                    return language;

                case "vn":
                    // 2. Get from region
                    _log.Debug("using language by region: " + _region);
                    language = "vi";
                    return language;
            }
            
            // 3. Fallback to system language
            _log.Debug("Using language by system language");

            return _languageMapping.GetValueOrDefault(Application.systemLanguage, "en");
        }

        public void SetCountry(string country)
        {
            country = country.ToUpper();
            PlayerPrefs.SetString(PlayerPrefsKeyLocaleCountry, country);
        }

        public void SetUserPrefsLanguage(string language)
        {
            var oldLanguage = GetLanguage();

            // Update user preference language
	        if (!string.IsNullOrEmpty(language))
	        {
		        PlayerPrefs.SetString(PlayerPrefsKeyUserPrefsLanguage, language);
	        } else {
		        PlayerPrefs.DeleteKey(PlayerPrefsKeyUserPrefsLanguage);
	        }
            
            var newLanguage = GetLanguage();

            if (oldLanguage == newLanguage) return;

            _translations = Utility.LoadTranslations(newLanguage);
            OnLanguageChanged?.Invoke(newLanguage);
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
            string translation = null;
            
            _translations?.TryGetValue(key, out translation);

            if (translation is not null) return translation;

            _log.Warning($"Translation for key '{key}' not found");
                
            return _defaultTranslations.GetValueOrDefault(key, key);
        }
        
        public string GetTranslation(LocaleTextKey textKey)
        {
            return GetTranslation(textKey.ToString());
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }

        public Dictionary<string,string> GetTranslations()
        {
            return _translations ?? _defaultTranslations;
        }
    }
    
    public enum LocaleTextKey
    {
        IAPCanceled,
        IAPFailed,
        IAPNotReady,
        IAPRequiresAuthentication,
        IAPPaymentDisabled,
        IAPPendingPurchaseTitle,
        IAPPendingPurchaseReceiptCopied,
        IAPPendingPurchaseCanceled,
        IAPPendingPurchaseRefunded,
        IAPPendingPurchaseVoided,
        IAPPendingPurchaseCompleted,
        IAPPendingPurchaseNotVerified,
        IAPPendingPurchaseItemCsButtonText,
        IAPPendingPurchaseItemCopyButtonText,
        IAPPendingPurchaseItemRetryButtonText,
        IAPPurchaseHistoryTitle,

        ErrorEmailEmpty,
        ErrorEmailNotValid,
        ErrorPasswordEmpty,
        ErrorPasswordShort,
        ErrorRePasswordEmpty,
        ErrorRePasswordNotMatch,

        AuthEmailLinkingSuccessful
    }
}
