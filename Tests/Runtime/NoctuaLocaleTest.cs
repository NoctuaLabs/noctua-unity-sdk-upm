using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class NoctuaLocaleTest
    {
        [Test]
        public void GetLanguageByPriority_NoPrefsNoRegion()
        {
            // No user preferences and no region
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("");
            var language = locale.GetLanguage();

            Assert.AreEqual(language, "en");

        }

        [Test]
        public void GetLanguageByPriority_NoPrefsRegionVietnam()
        {
            // No user preferences and region set to Vietnam
            var locale = new NoctuaLocale("vn");
            locale.SetUserPrefsLanguage("");
            var language = locale.GetLanguage();

            Assert.AreEqual(language, "vi");

        }

        [Test]
        public void GetLanguageByPriority_PrefsId()
        {
            // User preferences set to id
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("id");
            var language = locale.GetLanguage();

            Assert.AreEqual(language, "id");

        }
    }

    [TestFixture]
    public class NoctuaLocaleAdditionalTest
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaLocaleUserPrefsLanguage");
            PlayerPrefs.DeleteKey("NoctuaLocaleCountry");
            PlayerPrefs.DeleteKey("NoctuaLocaleCurrency");
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaLocaleUserPrefsLanguage");
            PlayerPrefs.DeleteKey("NoctuaLocaleCountry");
            PlayerPrefs.DeleteKey("NoctuaLocaleCurrency");
        }

        // 1. GetLanguage() — returns non-null, non-empty string in edit mode
        [Test]
        public void GetLanguage_EditMode_ReturnsNonNullNonEmptyString()
        {
            var locale = new NoctuaLocale("");
            var lang = locale.GetLanguage();
            Assert.IsNotNull(lang);
            Assert.IsNotEmpty(lang);
        }

        // 2. GetCountry() — returns non-null string (may be empty if not set)
        [Test]
        public void GetCountry_WhenNotSet_ReturnsNonNullString()
        {
            var locale = new NoctuaLocale("");
            var country = locale.GetCountry();
            Assert.IsNotNull(country);
        }

        // 3. GetCurrency() — returns non-null string; defaults to "USD"
        [Test]
        public void GetCurrency_WhenNotSet_ReturnsNonNullStringDefaultUsd()
        {
            var locale = new NoctuaLocale("");
            var currency = locale.GetCurrency();
            Assert.IsNotNull(currency);
            Assert.AreEqual("USD", currency);
        }

        // 4. SetUserPrefsLanguage("id") then GetLanguage() — returns "id"
        [Test]
        public void SetUserPrefsLanguage_Id_GetLanguage_ReturnsId()
        {
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("id");
            Assert.AreEqual("id", locale.GetLanguage());
        }

        // 5. SetUserPrefsLanguage(null) — does not throw, clears override (falls back to region/system)
        [Test]
        public void SetUserPrefsLanguage_Null_DoesNotThrow()
        {
            var locale = new NoctuaLocale("vn");
            locale.SetUserPrefsLanguage("id");
            Assert.DoesNotThrow(() => locale.SetUserPrefsLanguage(null));
            // After clearing, region "vn" kicks in
            Assert.AreEqual("vi", locale.GetLanguage());
        }

        // 6. GetTranslation(key) for a known LocaleTextKey — returns a non-null, non-empty string
        [Test]
        public void GetTranslation_KnownLocaleTextKey_ReturnsNonEmptyString()
        {
            var locale = new NoctuaLocale("");
            var result = locale.GetTranslation(LocaleTextKey.IAPCanceled);
            Assert.IsNotNull(result);
            // Should return the translation or fallback to the key name — either way non-empty
            Assert.IsNotEmpty(result);
        }

        // 7. GetTranslation(unknownKey) — returns the key itself as fallback
        [Test]
        public void GetTranslation_UnknownStringKey_ReturnsKeyAsFallback()
        {
            var locale = new NoctuaLocale("");
            const string unknownKey = "this_key_does_not_exist_at_all_12345";
            var result = locale.GetTranslation(unknownKey);
            Assert.AreEqual(unknownKey, result);
        }

        // 8. OnLanguageChanged event — fires when language changes from one code to another
        [Test]
        public void OnLanguageChanged_FiresWithNewLanguage_WhenLanguageChanges()
        {
            var locale = new NoctuaLocale("");
            // Start with no user pref (falls back to system)
            locale.SetUserPrefsLanguage("");

            string capturedLanguage = null;
            locale.OnLanguageChanged += lang => capturedLanguage = lang;

            locale.SetUserPrefsLanguage("th");

            Assert.AreEqual("th", capturedLanguage);
        }

        // 9. GetTranslations() — returns non-null dictionary
        [Test]
        public void GetTranslations_ReturnsNonNullDictionary()
        {
            var locale = new NoctuaLocale("");
            var dict = locale.GetTranslations();
            Assert.IsNotNull(dict);
        }

        // 10. Country/currency consistency for a known locale (ID)
        [Test]
        public void SetCountryAndCurrency_ForIndonesia_GetBothMatchExpected()
        {
            var locale = new NoctuaLocale("");
            locale.SetCountry("id");
            locale.SetCurrency("idr");

            Assert.AreEqual("ID", locale.GetCountry());
            Assert.AreEqual("IDR", locale.GetCurrency());
        }

        // 11. SetUserPrefsLanguage with a custom code — GetLanguage honours it
        [Test]
        public void SetUserPrefsLanguage_CustomCode_GetLanguage_ReturnsCustomCode()
        {
            var locale = new NoctuaLocale("vn"); // region would give "vi"
            locale.SetUserPrefsLanguage("zh-CN"); // user pref overrides region
            Assert.AreEqual("zh-CN", locale.GetLanguage());
        }

        // 12. GetTranslation(LocaleTextKey) delegates to string overload — results are equal
        [Test]
        public void GetTranslation_LocaleTextKey_EqualsStringOverload()
        {
            var locale = new NoctuaLocale("en");
            var fromEnum = locale.GetTranslation(LocaleTextKey.OfflineModeMessage);
            var fromString = locale.GetTranslation(LocaleTextKey.OfflineModeMessage.ToString());
            Assert.AreEqual(fromString, fromEnum);
        }
    }
}
