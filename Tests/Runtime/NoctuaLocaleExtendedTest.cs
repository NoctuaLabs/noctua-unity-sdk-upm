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

        [UnityTest]
        public IEnumerator SetCountry_GetCountry_UpperCase()
        {
            var locale = new NoctuaLocale("");
            locale.SetCountry("id");
            Assert.AreEqual("ID", locale.GetCountry());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetCountry_Default_ReturnsEmpty()
        {
            var locale = new NoctuaLocale("");
            Assert.AreEqual("", locale.GetCountry());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetCurrency_GetCurrency_UpperCase()
        {
            var locale = new NoctuaLocale("");
            locale.SetCurrency("idr");
            Assert.AreEqual("IDR", locale.GetCurrency());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetCurrency_Default_ReturnsUSD()
        {
            var locale = new NoctuaLocale("");
            Assert.AreEqual("USD", locale.GetCurrency());
            yield return null;
        }

        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
        public IEnumerator GetLanguage_RegionTh_ReturnsTh()
        {
            var locale = new NoctuaLocale("th");
            locale.SetUserPrefsLanguage("");
            Assert.AreEqual("th", locale.GetLanguage());
            yield return null;
        }

        [UnityTest]
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
}
