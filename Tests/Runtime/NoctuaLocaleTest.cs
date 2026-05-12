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
        [Timeout(5000)]
        public IEnumerator GetLanguageByPriority_NoPrefsNoRegion()
        {
            // No user preferences and no region
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("");
            var language = locale.GetLanguage();

            Assert.AreEqual(language, "en");

            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetLanguageByPriority_NoPrefsRegionVietnam()
        {
            // No user preferences and region set to Vietnam
            var locale = new NoctuaLocale("vn");
            locale.SetUserPrefsLanguage("");
            var language = locale.GetLanguage();

            Assert.AreEqual(language, "vi");

            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetLanguageByPriority_PrefsId()
        {
            // User preferences set to id
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("id");
            var language = locale.GetLanguage();

            Assert.AreEqual(language, "id");

            yield return null;
        }
    }

    [TestFixture]
    public class NoctuaLocaleAdditionalTest
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaLocaleUserPrefsLanguage");
            PlayerPrefs.DeleteKey("NoctuaLocaleCountry");
            PlayerPrefs.DeleteKey("NoctuaLocaleCurrency");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaLocaleUserPrefsLanguage");
            PlayerPrefs.DeleteKey("NoctuaLocaleCountry");
            PlayerPrefs.DeleteKey("NoctuaLocaleCurrency");
            yield return null;
        }

        // 1. GetLanguage() — returns non-null, non-empty string in edit mode
        [Test]
        [Timeout(5000)]
        public IEnumerator GetLanguage_EditMode_ReturnsNonNullNonEmptyString()
        {
            var locale = new NoctuaLocale("");
            var lang = locale.GetLanguage();
            Assert.IsNotNull(lang);
            Assert.IsNotEmpty(lang);
            yield return null;
        }

        // 2. GetCountry() — returns non-null string (may be empty if not set)
        [Test]
        [Timeout(5000)]
        public IEnumerator GetCountry_WhenNotSet_ReturnsNonNullString()
        {
            var locale = new NoctuaLocale("");
            var country = locale.GetCountry();
            Assert.IsNotNull(country);
            yield return null;
        }

        // 3. GetCurrency() — returns non-null string; defaults to "USD"
        [Test]
        [Timeout(5000)]
        public IEnumerator GetCurrency_WhenNotSet_ReturnsNonNullStringDefaultUsd()
        {
            var locale = new NoctuaLocale("");
            var currency = locale.GetCurrency();
            Assert.IsNotNull(currency);
            Assert.AreEqual("USD", currency);
            yield return null;
        }

        // 4. SetUserPrefsLanguage("id") then GetLanguage() — returns "id"
        [Test]
        [Timeout(5000)]
        public IEnumerator SetUserPrefsLanguage_Id_GetLanguage_ReturnsId()
        {
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("id");
            Assert.AreEqual("id", locale.GetLanguage());
            yield return null;
        }

        // 5. SetUserPrefsLanguage(null) — does not throw, clears override (falls back to region/system)
        [Test]
        [Timeout(5000)]
        public IEnumerator SetUserPrefsLanguage_Null_DoesNotThrow()
        {
            var locale = new NoctuaLocale("vn");
            locale.SetUserPrefsLanguage("id");
            Assert.DoesNotThrow(() => locale.SetUserPrefsLanguage(null));
            // After clearing, region "vn" kicks in
            Assert.AreEqual("vi", locale.GetLanguage());
            yield return null;
        }

        // 6. GetTranslation(key) for a known LocaleTextKey — returns a non-null, non-empty string
        [Test]
        [Timeout(5000)]
        public IEnumerator GetTranslation_KnownLocaleTextKey_ReturnsNonEmptyString()
        {
            var locale = new NoctuaLocale("");
            var result = locale.GetTranslation(LocaleTextKey.IAPCanceled);
            Assert.IsNotNull(result);
            // Should return the translation or fallback to the key name — either way non-empty
            Assert.IsNotEmpty(result);
            yield return null;
        }

        // 7. GetTranslation(unknownKey) — returns the key itself as fallback
        [Test]
        [Timeout(5000)]
        public IEnumerator GetTranslation_UnknownStringKey_ReturnsKeyAsFallback()
        {
            var locale = new NoctuaLocale("");
            const string unknownKey = "this_key_does_not_exist_at_all_12345";
            var result = locale.GetTranslation(unknownKey);
            Assert.AreEqual(unknownKey, result);
            yield return null;
        }

        // 8. OnLanguageChanged event — fires when language changes from one code to another
        [Test]
        [Timeout(5000)]
        public IEnumerator OnLanguageChanged_FiresWithNewLanguage_WhenLanguageChanges()
        {
            var locale = new NoctuaLocale("");
            // Start with no user pref (falls back to system)
            locale.SetUserPrefsLanguage("");

            string capturedLanguage = null;
            locale.OnLanguageChanged += lang => capturedLanguage = lang;

            locale.SetUserPrefsLanguage("th");

            Assert.AreEqual("th", capturedLanguage);
            yield return null;
        }

        // 9. GetTranslations() — returns non-null dictionary
        [Test]
        [Timeout(5000)]
        public IEnumerator GetTranslations_ReturnsNonNullDictionary()
        {
            var locale = new NoctuaLocale("");
            var dict = locale.GetTranslations();
            Assert.IsNotNull(dict);
            yield return null;
        }

        // 10. Country/currency consistency for a known locale (ID)
        [Test]
        [Timeout(5000)]
        public IEnumerator SetCountryAndCurrency_ForIndonesia_GetBothMatchExpected()
        {
            var locale = new NoctuaLocale("");
            locale.SetCountry("id");
            locale.SetCurrency("idr");

            Assert.AreEqual("ID", locale.GetCountry());
            Assert.AreEqual("IDR", locale.GetCurrency());
            yield return null;
        }

        // 11. SetUserPrefsLanguage with a custom code — GetLanguage honours it
        [Test]
        [Timeout(5000)]
        public IEnumerator SetUserPrefsLanguage_CustomCode_GetLanguage_ReturnsCustomCode()
        {
            var locale = new NoctuaLocale("vn"); // region would give "vi"
            locale.SetUserPrefsLanguage("zh-CN"); // user pref overrides region
            Assert.AreEqual("zh-CN", locale.GetLanguage());
            yield return null;
        }

        // 12. GetTranslation(LocaleTextKey) delegates to string overload — results are equal
        [Test]
        [Timeout(5000)]
        public IEnumerator GetTranslation_LocaleTextKey_EqualsStringOverload()
        {
            var locale = new NoctuaLocale("en");
            var fromEnum = locale.GetTranslation(LocaleTextKey.OfflineModeMessage);
            var fromString = locale.GetTranslation(LocaleTextKey.OfflineModeMessage.ToString());
            Assert.AreEqual(fromString, fromEnum);
            yield return null;
        }
    }
}
