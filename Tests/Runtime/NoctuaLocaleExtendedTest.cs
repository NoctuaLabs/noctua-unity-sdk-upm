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

        [Test]
        [Timeout(5000)]
        public IEnumerator SetCountry_GetCountry_UpperCase()
        {
            var locale = new NoctuaLocale("");
            locale.SetCountry("id");
            Assert.AreEqual("ID", locale.GetCountry());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetCountry_Default_ReturnsEmpty()
        {
            var locale = new NoctuaLocale("");
            Assert.AreEqual("", locale.GetCountry());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator SetCurrency_GetCurrency_UpperCase()
        {
            var locale = new NoctuaLocale("");
            locale.SetCurrency("idr");
            Assert.AreEqual("IDR", locale.GetCurrency());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetCurrency_Default_ReturnsUSD()
        {
            var locale = new NoctuaLocale("");
            Assert.AreEqual("USD", locale.GetCurrency());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator OnLanguageChanged_FiresWhenLanguageChanges()
        {
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("");

            string receivedLanguage = null;
            locale.OnLanguageChanged += lang => receivedLanguage = lang;

            locale.SetUserPrefsLanguage("vi");
            Assert.AreEqual("vi", receivedLanguage);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator OnLanguageChanged_DoesNotFire_WhenSameLanguage()
        {
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("id");

            bool fired = false;
            locale.OnLanguageChanged += lang => fired = true;

            locale.SetUserPrefsLanguage("id");
            Assert.IsFalse(fired);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetLanguage_RegionTh_ReturnsTh()
        {
            var locale = new NoctuaLocale("th");
            locale.SetUserPrefsLanguage("");
            Assert.AreEqual("th", locale.GetLanguage());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator SetUserPrefsLanguage_ClearWithEmpty_FallsBackToRegion()
        {
            var locale = new NoctuaLocale("vn");
            locale.SetUserPrefsLanguage("id");
            Assert.AreEqual("id", locale.GetLanguage());

            locale.SetUserPrefsLanguage("");
            Assert.AreEqual("vi", locale.GetLanguage());
            yield return null;
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

        [Test]
        [Timeout(5000)]
        public IEnumerator GetLanguage_NoRegionNoPrefs_ReturnsFallbackString()
        {
            var locale = new NoctuaLocale("");
            var lang = locale.GetLanguage();
            // In Editor, system language maps to something — just confirm it is non-null and non-empty
            Assert.IsNotNull(lang);
            Assert.IsNotEmpty(lang);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator SetUserPrefsLanguage_Null_ClearsOverrideAndFallsBackToRegion()
        {
            var locale = new NoctuaLocale("th");
            locale.SetUserPrefsLanguage("id");
            Assert.AreEqual("id", locale.GetLanguage());

            // null behaves like empty — should clear the user pref
            locale.SetUserPrefsLanguage(null);
            Assert.AreEqual("th", locale.GetLanguage());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetLanguage_UnknownRegion_FallsBackToSystemLanguage()
        {
            var locale = new NoctuaLocale("xx");
            locale.SetUserPrefsLanguage("");
            var lang = locale.GetLanguage();
            // Unknown region is not matched by the switch, so it falls through to system language
            Assert.IsNotNull(lang);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator SetCountry_AlreadyUppercase_RoundTripsCorrectly()
        {
            var locale = new NoctuaLocale("");
            locale.SetCountry("US");
            Assert.AreEqual("US", locale.GetCountry());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator SetCurrency_AlreadyUppercase_RoundTripsCorrectly()
        {
            var locale = new NoctuaLocale("");
            locale.SetCurrency("USD");
            Assert.AreEqual("USD", locale.GetCurrency());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator SetCountry_MultipleTimes_LastOneWins()
        {
            var locale = new NoctuaLocale("");
            locale.SetCountry("id");
            locale.SetCountry("sg");
            Assert.AreEqual("SG", locale.GetCountry());
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetTranslation_StringKey_UnknownKey_ReturnsKey()
        {
            // NoctuaLocale.GetTranslation(string) returns the key itself when no translation is found
            var locale = new NoctuaLocale("");
            var result = locale.GetTranslation("this_key_does_not_exist_anywhere");
            Assert.AreEqual("this_key_does_not_exist_anywhere", result);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetTranslation_LocaleTextKey_DelegatesToStringOverload()
        {
            // GetTranslation(LocaleTextKey) must call GetTranslation(key.ToString())
            var locale = new NoctuaLocale("");
            var fromEnum = locale.GetTranslation(LocaleTextKey.IAPCanceled);
            var fromString = locale.GetTranslation(LocaleTextKey.IAPCanceled.ToString());
            Assert.AreEqual(fromString, fromEnum);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator GetTranslations_ReturnsNonNullDictionary()
        {
            var locale = new NoctuaLocale("");
            var dict = locale.GetTranslations();
            Assert.IsNotNull(dict);
            yield return null;
        }

        [Test]
        [Timeout(5000)]
        public IEnumerator OnLanguageChanged_Unsubscribe_NoFireAfterRemoval()
        {
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("");

            bool fired = false;
            System.Action<string> handler = _ => fired = true;
            locale.OnLanguageChanged += handler;
            locale.OnLanguageChanged -= handler;

            locale.SetUserPrefsLanguage("vi");
            Assert.IsFalse(fired);
            yield return null;
        }
    }
}
