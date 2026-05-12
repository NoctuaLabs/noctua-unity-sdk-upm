using System.Collections;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class NoctuaLocaleExtendedTest
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaLocaleUserPrefsLanguage");
            PlayerPrefs.DeleteKey("NoctuaLocaleCountry");
            PlayerPrefs.DeleteKey("NoctuaLocaleCurrency");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaLocaleUserPrefsLanguage");
            PlayerPrefs.DeleteKey("NoctuaLocaleCountry");
            PlayerPrefs.DeleteKey("NoctuaLocaleCurrency");
        }

        [Test]
        public void SetCountry_GetCountry_UpperCase()
        {
            var locale = new NoctuaLocale("");
            locale.SetCountry("id");
            Assert.AreEqual("ID", locale.GetCountry());
        }

        [Test]
        public void GetCountry_Default_ReturnsEmpty()
        {
            var locale = new NoctuaLocale("");
            Assert.AreEqual("", locale.GetCountry());
        }

        [Test]
        public void SetCurrency_GetCurrency_UpperCase()
        {
            var locale = new NoctuaLocale("");
            locale.SetCurrency("idr");
            Assert.AreEqual("IDR", locale.GetCurrency());
        }

        [Test]
        public void GetCurrency_Default_ReturnsUSD()
        {
            var locale = new NoctuaLocale("");
            Assert.AreEqual("USD", locale.GetCurrency());
        }

        [Test]
        public void OnLanguageChanged_FiresWhenLanguageChanges()
        {
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("");

            string receivedLanguage = null;
            locale.OnLanguageChanged += lang => receivedLanguage = lang;

            locale.SetUserPrefsLanguage("vi");
            Assert.AreEqual("vi", receivedLanguage);
        }

        [Test]
        public void OnLanguageChanged_DoesNotFire_WhenSameLanguage()
        {
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("id");

            bool fired = false;
            locale.OnLanguageChanged += lang => fired = true;

            locale.SetUserPrefsLanguage("id");
            Assert.IsFalse(fired);
        }

        [Test]
        public void GetLanguage_RegionTh_ReturnsTh()
        {
            var locale = new NoctuaLocale("th");
            locale.SetUserPrefsLanguage("");
            Assert.AreEqual("th", locale.GetLanguage());
        }

        [Test]
        public void SetUserPrefsLanguage_ClearWithEmpty_FallsBackToRegion()
        {
            var locale = new NoctuaLocale("vn");
            locale.SetUserPrefsLanguage("id");
            Assert.AreEqual("id", locale.GetLanguage());

            locale.SetUserPrefsLanguage("");
            Assert.AreEqual("vi", locale.GetLanguage());
        }
    }

    public class NoctuaLocaleEdgeCaseTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaLocaleUserPrefsLanguage");
            PlayerPrefs.DeleteKey("NoctuaLocaleCountry");
            PlayerPrefs.DeleteKey("NoctuaLocaleCurrency");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaLocaleUserPrefsLanguage");
            PlayerPrefs.DeleteKey("NoctuaLocaleCountry");
            PlayerPrefs.DeleteKey("NoctuaLocaleCurrency");
        }

        [Test]
        public void GetLanguage_NoRegionNoPrefs_ReturnsFallbackString()
        {
            var locale = new NoctuaLocale("");
            var lang = locale.GetLanguage();
            // In Editor, system language maps to something — just confirm it is non-null and non-empty
            Assert.IsNotNull(lang);
            Assert.IsNotEmpty(lang);
        }

        [Test]
        public void SetUserPrefsLanguage_Null_ClearsOverrideAndFallsBackToRegion()
        {
            var locale = new NoctuaLocale("th");
            locale.SetUserPrefsLanguage("id");
            Assert.AreEqual("id", locale.GetLanguage());

            // null behaves like empty — should clear the user pref
            locale.SetUserPrefsLanguage(null);
            Assert.AreEqual("th", locale.GetLanguage());
        }

        [Test]
        public void GetLanguage_UnknownRegion_FallsBackToSystemLanguage()
        {
            var locale = new NoctuaLocale("xx");
            locale.SetUserPrefsLanguage("");
            var lang = locale.GetLanguage();
            // Unknown region is not matched by the switch, so it falls through to system language
            Assert.IsNotNull(lang);
        }

        [Test]
        public void SetCountry_AlreadyUppercase_RoundTripsCorrectly()
        {
            var locale = new NoctuaLocale("");
            locale.SetCountry("US");
            Assert.AreEqual("US", locale.GetCountry());
        }

        [Test]
        public void SetCurrency_AlreadyUppercase_RoundTripsCorrectly()
        {
            var locale = new NoctuaLocale("");
            locale.SetCurrency("USD");
            Assert.AreEqual("USD", locale.GetCurrency());
        }

        [Test]
        public void SetCountry_MultipleTimes_LastOneWins()
        {
            var locale = new NoctuaLocale("");
            locale.SetCountry("id");
            locale.SetCountry("sg");
            Assert.AreEqual("SG", locale.GetCountry());
        }

        [Test]
        public void GetTranslation_StringKey_UnknownKey_ReturnsKey()
        {
            // NoctuaLocale.GetTranslation(string) returns the key itself when no translation is found
            var locale = new NoctuaLocale("");
            var result = locale.GetTranslation("this_key_does_not_exist_anywhere");
            Assert.AreEqual("this_key_does_not_exist_anywhere", result);
        }

        [Test]
        public void GetTranslation_LocaleTextKey_DelegatesToStringOverload()
        {
            // GetTranslation(LocaleTextKey) must call GetTranslation(key.ToString())
            var locale = new NoctuaLocale("");
            var fromEnum = locale.GetTranslation(LocaleTextKey.IAPCanceled);
            var fromString = locale.GetTranslation(LocaleTextKey.IAPCanceled.ToString());
            Assert.AreEqual(fromString, fromEnum);
        }

        [Test]
        public void GetTranslations_ReturnsNonNullDictionary()
        {
            var locale = new NoctuaLocale("");
            var dict = locale.GetTranslations();
            Assert.IsNotNull(dict);
        }

        [Test]
        public void OnLanguageChanged_Unsubscribe_NoFireAfterRemoval()
        {
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("");

            bool fired = false;
            System.Action<string> handler = _ => fired = true;
            locale.OnLanguageChanged += handler;
            locale.OnLanguageChanged -= handler;

            locale.SetUserPrefsLanguage("vi");
            Assert.IsFalse(fired);
        }
    }
}
