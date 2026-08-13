using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Application = UnityEngine.Device.Application;
using SystemInfo = UnityEngine.Device.SystemInfo;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides locale information (language, country, currency) and translation services.
    /// Implements <see cref="ILocaleProvider"/> for dependency injection into lower layers.
    /// Language is resolved by priority: user preference > region config > system language.
    /// </summary>
    public class NoctuaLocale : ILocaleProvider
    {
        /// <summary>
        /// Raised when the active language changes, passing the new language code.
        /// </summary>
        public event Action<string> OnLanguageChanged;
        
        private readonly string _region;
        private const string PlayerPrefsKeyUserPrefsLanguage = "NoctuaLocaleUserPrefsLanguage";
        private const string PlayerPrefsKeyLocaleCountry = "NoctuaLocaleCountry";
        private const string PlayerPrefsKeyLocaleCurrency = "NoctuaLocaleCurrency";
        private readonly Dictionary<SystemLanguage, string> _languageMapping =
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
            };

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaLocale));
        private Dictionary<string,string> _translations;
        private readonly Dictionary<string,string> _defaultTranslations;
        private string _cachedTimezone;

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string noctuaGetTimezone();
#endif

        /// <summary>
        /// Creates a new locale instance, loading translations for the resolved language.
        /// </summary>
        /// <param name="region">Optional region code override (e.g. "VN", "TH") for language resolution.</param>
        public NoctuaLocale(string region = "")
        {
            if (!string.IsNullOrEmpty(region))
            {
                _region = region;
            }
            
            _translations = Utility.LoadTranslations(GetLanguage());
            _defaultTranslations = Utility.LoadTranslations("en") ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets the current language code, resolved by priority:
        /// user preference > region config > system language.
        /// </summary>
        /// <returns>An ISO 639-1 language code (e.g. "en", "id", "vi", "th").</returns>
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

        /// <summary>
        /// Persists the country code to PlayerPrefs (uppercased).
        /// </summary>
        /// <param name="country">An ISO 3166-1 alpha-2 country code (e.g. "US", "ID").</param>
        public void SetCountry(string country)
        {
            country = country.ToUpper();
            PlayerPrefs.SetString(PlayerPrefsKeyLocaleCountry, country);
        }

        /// <summary>
        /// Sets or clears the user-preferred language override. If the language actually changes,
        /// reloads translations and raises <see cref="OnLanguageChanged"/>.
        /// </summary>
        /// <param name="language">The language code to set, or <c>null</c>/empty to clear the override.</param>
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

        /// <summary>
        /// Persists the currency code to PlayerPrefs (uppercased).
        /// </summary>
        /// <param name="currency">An ISO 4217 currency code (e.g. "USD", "IDR").</param>
        public void SetCurrency(string currency)
        {
            currency = currency.ToUpper();
            PlayerPrefs.SetString(PlayerPrefsKeyLocaleCurrency, currency);
        }

        /// <summary>
        /// Gets the persisted country code from PlayerPrefs.
        /// </summary>
        /// <returns>An ISO 3166-1 alpha-2 country code, or empty string if not set.</returns>
        public string GetCountry()
        {
            // Default to empty string, the consumer of this API
            // should handle the case when the country is not set
            return PlayerPrefs.GetString(PlayerPrefsKeyLocaleCountry, "");
        }

        /// <summary>
        /// Gets the persisted currency code from PlayerPrefs.
        /// </summary>
        /// <returns>An ISO 4217 currency code. Defaults to "USD" if not set.</returns>
        public string GetCurrency()
        {
            return PlayerPrefs.GetString(PlayerPrefsKeyLocaleCurrency, "USD"); // Default to USD
        }

        /// <summary>
        /// Gets the device's local IANA timezone identifier. The result is cached after the
        /// first call, since resolving it natively on Android/iOS is not free and the timezone
        /// is not expected to change mid-session.
        /// </summary>
        /// <remarks>
        /// <see cref="TimeZoneInfo.Local"/> unreliably resolves to a synthetic zone with Id
        /// "Local" instead of a real IANA identifier on both Android and iOS real devices,
        /// because IL2CPP/Mono's bundled timezone database doesn't reliably match the OS zone.
        /// We query the native platform APIs instead — Android's
        /// <c>java.util.TimeZone.getDefault().getID()</c> and iOS's
        /// <c>NSTimeZone.localTimeZone.name</c> via <c>NoctuaInterop.m</c> — both of which
        /// return a proper IANA identifier (e.g. "Asia/Jakarta").
        /// </remarks>
        /// <returns>An IANA timezone ID (e.g. "Asia/Jakarta"). Falls back to "UTC" if unavailable.</returns>
        public string GetTimezone()
        {
            if (!string.IsNullOrEmpty(_cachedTimezone))
            {
                return _cachedTimezone;
            }

            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var timeZoneClass = new AndroidJavaClass("java.util.TimeZone");
                using var defaultTimeZone = timeZoneClass.CallStatic<AndroidJavaObject>("getDefault");
                var androidId = defaultTimeZone.Call<string>("getID");

                if (!string.IsNullOrEmpty(androidId))
                {
                    _cachedTimezone = androidId;
                    return _cachedTimezone;
                }
#elif UNITY_IOS && !UNITY_EDITOR
                var iosId = noctuaGetTimezone();

                if (!string.IsNullOrEmpty(iosId))
                {
                    _cachedTimezone = iosId;
                    return _cachedTimezone;
                }
#endif
                var localId = TimeZoneInfo.Local.Id;

                if (string.IsNullOrEmpty(localId) || localId == "Local")
                {
                    _log.Warning("TimeZoneInfo.Local.Id could not be resolved to an IANA identifier (got: " + localId + ")");
                    _cachedTimezone = "UTC";
                    return _cachedTimezone;
                }

                _cachedTimezone = localId;
                return _cachedTimezone;
            }
            catch (Exception e)
            {
                _log.Warning("Failed to resolve local timezone: " + e.Message);
                _cachedTimezone = "UTC";
                return _cachedTimezone;
            }
        }

        /// <summary>
        /// Gets the localized translation for the specified string key.
        /// Falls back to English if the current language has no translation.
        /// </summary>
        /// <param name="key">The translation key to look up.</param>
        /// <returns>The translated string, or the key itself if no translation exists.</returns>
        public string GetTranslation(string key)
        {
            string translation = null;
            
            _translations?.TryGetValue(key, out translation);

            if (translation is not null) return translation;

            _log.Warning($"Translation for key '{key}' not found");
                
            return _defaultTranslations.GetValueOrDefault(key, key);
        }
        
        /// <summary>
        /// Gets the localized translation for the specified <see cref="LocaleTextKey"/>.
        /// </summary>
        /// <param name="textKey">The typed locale text key to look up.</param>
        /// <returns>The translated string, or the key name if no translation exists.</returns>
        public string GetTranslation(LocaleTextKey textKey)
        {
            return GetTranslation(textKey.ToString());
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }

        /// <summary>
        /// Gets the full translation dictionary for the current language.
        /// Falls back to the English dictionary if no translations are loaded.
        /// </summary>
        /// <returns>A dictionary mapping translation keys to localized strings.</returns>
        public Dictionary<string,string> GetTranslations()
        {
            return _translations ?? _defaultTranslations;
        }
    }
    
    /// <summary>
    /// Strongly-typed keys for SDK UI translation strings.
    /// Each value corresponds to a key in the translation JSON files.
    /// </summary>
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
        IAPDisabled,
        OfflineModeMessage,

        ErrorEmailEmpty,
        ErrorEmailNotValid,
        ErrorPasswordEmpty,
        ErrorPasswordShort,
        ErrorRePasswordEmpty,
        ErrorRePasswordNotMatch,

        AuthEmailLinkingSuccessful
    }
}
