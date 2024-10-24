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
        private readonly Config _config;
        private string PlayerPrefsKeyLocaleCountry = "NoctuaLocaleCountry";
        private string PlayerPrefsKeyLocaleCurrency = "NoctuaLocaleCurrency";

        public NoctuaLocale()
        {
        }

        public string GetLanguage()
        {

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
            return PlayerPrefs.GetString(PlayerPrefsKeyLocaleCurrency, "IDR"); // Default to Indonesia's Rupiah
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}
