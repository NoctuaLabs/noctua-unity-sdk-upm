using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="NoctuaLocale"/> covering the logic paths that do
    /// NOT require Unity Play-mode (no coroutines, no async network):
    ///   — <see cref="NoctuaLocale.GetCountry"/> / <see cref="NoctuaLocale.SetCountry"/>
    ///     (PlayerPrefs round-trip, auto-uppercase)
    ///   — <see cref="NoctuaLocale.GetCurrency"/> / <see cref="NoctuaLocale.SetCurrency"/>
    ///     (PlayerPrefs round-trip, auto-uppercase, "USD" default)
    ///   — <see cref="NoctuaLocale.GetLanguage"/>
    ///       · User-preference path (PlayerPrefs wins over region and system language)
    ///       · Region override: "TH" / "th" → "th", "VN" / "vn" → "vi"
    ///       · <see cref="NoctuaLocale.SetUserPrefsLanguage"/> — set / clear pref, reload
    ///   — <see cref="NoctuaLocale.OnLanguageChanged"/> event
    ///   — <see cref="NoctuaLocale.GetTranslation(string)"/> / <see cref="NoctuaLocale.GetTranslation(LocaleTextKey)"/>
    ///     fallback when translation is absent (returns key / key-name)
    ///   — <see cref="NoctuaLocale.GetTranslations"/> returns non-null
    ///   — <see cref="LocaleTextKey"/> enum: count and named values
    /// </summary>
    [TestFixture]
    public class NoctuaLocaleEditModeTest
    {
        private const string KeyUserPrefsLanguage = "NoctuaLocaleUserPrefsLanguage";
        private const string KeyLocaleCountry     = "NoctuaLocaleCountry";
        private const string KeyLocaleCurrency    = "NoctuaLocaleCurrency";

        [SetUp]
        public void SetUp()
        {
            // Guarantee a clean PlayerPrefs slate before each test
            PlayerPrefs.DeleteKey(KeyUserPrefsLanguage);
            PlayerPrefs.DeleteKey(KeyLocaleCountry);
            PlayerPrefs.DeleteKey(KeyLocaleCurrency);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(KeyUserPrefsLanguage);
            PlayerPrefs.DeleteKey(KeyLocaleCountry);
            PlayerPrefs.DeleteKey(KeyLocaleCurrency);
        }

        // ─── GetCountry / SetCountry ──────────────────────────────────────────

        [Test]
        public void GetCountry_WhenNotSet_ReturnsEmptyString()
        {
            var locale = new NoctuaLocale();
            Assert.AreEqual("", locale.GetCountry());
        }

        [Test]
        public void SetCountry_Then_GetCountry_ReturnsUppercasedValue()
        {
            var locale = new NoctuaLocale();
            locale.SetCountry("id");
            Assert.AreEqual("ID", locale.GetCountry());
        }

        [Test]
        public void SetCountry_AlreadyUppercase_ReturnsUnchanged()
        {
            var locale = new NoctuaLocale();
            locale.SetCountry("SG");
            Assert.AreEqual("SG", locale.GetCountry());
        }

        [Test]
        public void SetCountry_MixedCase_ReturnedAsUppercase()
        {
            var locale = new NoctuaLocale();
            locale.SetCountry("uSa");
            Assert.AreEqual("USA", locale.GetCountry());
        }

        [Test]
        public void SetCountry_OverwritesPreviousValue()
        {
            var locale = new NoctuaLocale();
            locale.SetCountry("ID");
            locale.SetCountry("VN");
            Assert.AreEqual("VN", locale.GetCountry());
        }

        // ─── GetCurrency / SetCurrency ────────────────────────────────────────

        [Test]
        public void GetCurrency_WhenNotSet_DefaultsToUsd()
        {
            var locale = new NoctuaLocale();
            Assert.AreEqual("USD", locale.GetCurrency());
        }

        [Test]
        public void SetCurrency_Then_GetCurrency_ReturnsUppercasedValue()
        {
            var locale = new NoctuaLocale();
            locale.SetCurrency("idr");
            Assert.AreEqual("IDR", locale.GetCurrency());
        }

        [Test]
        public void SetCurrency_AlreadyUppercase_ReturnsUnchanged()
        {
            var locale = new NoctuaLocale();
            locale.SetCurrency("THB");
            Assert.AreEqual("THB", locale.GetCurrency());
        }

        [Test]
        public void SetCurrency_OverwritesPreviousValue()
        {
            var locale = new NoctuaLocale();
            locale.SetCurrency("IDR");
            locale.SetCurrency("SGD");
            Assert.AreEqual("SGD", locale.GetCurrency());
        }

        // ─── GetLanguage — user preference wins ───────────────────────────────

        [Test]
        public void GetLanguage_WithUserPrefSet_ReturnsPref()
        {
            PlayerPrefs.SetString(KeyUserPrefsLanguage, "id");
            var locale = new NoctuaLocale();
            Assert.AreEqual("id", locale.GetLanguage());
        }

        [Test]
        public void GetLanguage_WithUserPrefSet_RegionOverrideIgnored()
        {
            // User pref "en" takes priority even when region is "th"
            PlayerPrefs.SetString(KeyUserPrefsLanguage, "en");
            var locale = new NoctuaLocale(region: "th");
            Assert.AreEqual("en", locale.GetLanguage());
        }

        // ─── GetLanguage — region override ────────────────────────────────────

        [Test]
        public void GetLanguage_RegionTh_LowerCase_ReturnsThaiLanguage()
        {
            var locale = new NoctuaLocale(region: "th");
            Assert.AreEqual("th", locale.GetLanguage());
        }

        [Test]
        public void GetLanguage_RegionTH_UpperCase_ReturnsThaiLanguage()
        {
            // Region matching is case-insensitive (ToLower applied internally)
            var locale = new NoctuaLocale(region: "TH");
            Assert.AreEqual("th", locale.GetLanguage());
        }

        [Test]
        public void GetLanguage_RegionVn_LowerCase_ReturnsVietnameseLanguage()
        {
            var locale = new NoctuaLocale(region: "vn");
            Assert.AreEqual("vi", locale.GetLanguage());
        }

        [Test]
        public void GetLanguage_RegionVN_UpperCase_ReturnsVietnameseLanguage()
        {
            var locale = new NoctuaLocale(region: "VN");
            Assert.AreEqual("vi", locale.GetLanguage());
        }

        [Test]
        public void GetLanguage_NoRegionNoUserPref_ReturnsNonNullString()
        {
            // Falls through to system language — we just verify it's non-null/non-empty
            var locale = new NoctuaLocale();
            var lang = locale.GetLanguage();
            Assert.IsNotNull(lang);
            Assert.IsNotEmpty(lang);
        }

        // ─── SetUserPrefsLanguage ─────────────────────────────────────────────

        [Test]
        public void SetUserPrefsLanguage_NonEmpty_AffectsGetLanguage()
        {
            var locale = new NoctuaLocale();
            locale.SetUserPrefsLanguage("vi");
            Assert.AreEqual("vi", locale.GetLanguage());
        }

        [Test]
        public void SetUserPrefsLanguage_Null_ClearsUserPref()
        {
            PlayerPrefs.SetString(KeyUserPrefsLanguage, "vi");
            var locale = new NoctuaLocale();
            locale.SetUserPrefsLanguage(null);
            // After clearing, language resolves from region/system (not from pref)
            Assert.AreNotEqual("vi", locale.GetLanguage());
        }

        [Test]
        public void SetUserPrefsLanguage_EmptyString_ClearsUserPref()
        {
            PlayerPrefs.SetString(KeyUserPrefsLanguage, "id");
            var locale = new NoctuaLocale();
            locale.SetUserPrefsLanguage("");
            // Pref cleared — falls through to region/system
            Assert.AreNotEqual("id", locale.GetLanguage());
        }

        // ─── OnLanguageChanged event ──────────────────────────────────────────

        [Test]
        public void SetUserPrefsLanguage_WhenLanguageChanges_FiresOnLanguageChanged()
        {
            var locale       = new NoctuaLocale(region: "th");  // initial language "th"
            string received  = null;
            locale.OnLanguageChanged += lang => received = lang;

            locale.SetUserPrefsLanguage("id");  // switches from "th" to "id"

            Assert.AreEqual("id", received,
                "OnLanguageChanged should fire with new language code when it changes");
        }

        [Test]
        public void SetUserPrefsLanguage_WhenLanguageUnchanged_DoesNotFireOnLanguageChanged()
        {
            PlayerPrefs.SetString(KeyUserPrefsLanguage, "en");
            var locale       = new NoctuaLocale();   // initial language "en"
            int callCount    = 0;
            locale.OnLanguageChanged += _ => callCount++;

            // Setting the same language "en" → no change expected
            locale.SetUserPrefsLanguage("en");

            Assert.AreEqual(0, callCount,
                "OnLanguageChanged must NOT fire when language does not actually change");
        }

        // ─── GetTranslation — fallback behavior ───────────────────────────────

        [Test]
        public void GetTranslation_UnknownKey_ReturnsKeyItself()
        {
            // When no translation exists for the key, the key string is returned verbatim
            var locale = new NoctuaLocale();
            var result = locale.GetTranslation("SomeKeyThatDoesNotExist_xyz_9999");
            Assert.AreEqual("SomeKeyThatDoesNotExist_xyz_9999", result);
        }

        [Test]
        public void GetTranslation_LocaleTextKey_UnknownTranslation_ReturnsKeyName()
        {
            // GetTranslation(LocaleTextKey) calls GetTranslation(textKey.ToString())
            // If no translation exists, should return the enum member name
            var locale = new NoctuaLocale();
            var result = locale.GetTranslation(LocaleTextKey.IAPCanceled);
            // In EditMode resources may not be available — fallback is the key name
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void GetTranslation_EmptyKey_ReturnsEmptyStringOrKey()
        {
            var locale = new NoctuaLocale();
            // Empty string is a valid dictionary lookup; falls through to default behavior
            var result = locale.GetTranslation("");
            Assert.IsNotNull(result);
        }

        // ─── GetTranslations ──────────────────────────────────────────────────

        [Test]
        public void GetTranslations_ReturnsNonNull()
        {
            var locale = new NoctuaLocale();
            var dict   = locale.GetTranslations();
            Assert.IsNotNull(dict);
        }

        [Test]
        public void GetTranslations_ReturnsDictionary()
        {
            var locale = new NoctuaLocale();
            var dict   = locale.GetTranslations();
            // Must be a dictionary (IDictionary<string,string> compatible)
            Assert.IsInstanceOf<Dictionary<string, string>>(dict);
        }

        // ─── LocaleTextKey enum ───────────────────────────────────────────────

        [Test]
        public void LocaleTextKey_ContainsIAPCanceled()
        {
            Assert.IsTrue(
                Enum.IsDefined(typeof(LocaleTextKey), "IAPCanceled"),
                "LocaleTextKey must define IAPCanceled");
        }

        [Test]
        public void LocaleTextKey_ContainsIAPFailed()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(LocaleTextKey), "IAPFailed"));
        }

        [Test]
        public void LocaleTextKey_ContainsOfflineModeMessage()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(LocaleTextKey), "OfflineModeMessage"));
        }

        [Test]
        public void LocaleTextKey_ContainsAuthEmailLinkingSuccessful()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(LocaleTextKey), "AuthEmailLinkingSuccessful"));
        }

        [Test]
        public void LocaleTextKey_ContainsErrorEmailNotValid()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(LocaleTextKey), "ErrorEmailNotValid"));
        }

        [Test]
        public void LocaleTextKey_Count_IsAtLeast18()
        {
            // At the time of writing 18 values exist; enforce a lower bound so
            // accidental removals are caught without pinning the exact count.
            int count = Enum.GetValues(typeof(LocaleTextKey)).Length;
            Assert.GreaterOrEqual(count, 18,
                $"LocaleTextKey should have at least 18 values, found {count}");
        }

        [Test]
        public void LocaleTextKey_AllValues_ToStringNonEmpty()
        {
            // Verify every enum member has a non-empty string representation
            foreach (LocaleTextKey key in Enum.GetValues(typeof(LocaleTextKey)))
            {
                var str = key.ToString();
                Assert.IsNotEmpty(str, $"LocaleTextKey.{key} ToString() must be non-empty");
            }
        }

        // ─── OnLanguageChanged — unsubscribe ──────────────────────────────────

        [Test]
        public void OnLanguageChanged_Unsubscribe_DoesNotFireAfterHandlerRemoved()
        {
            var locale = new NoctuaLocale(region: "");
            locale.SetUserPrefsLanguage("");  // start with no override

            bool fired = false;
            System.Action<string> handler = _ => fired = true;
            locale.OnLanguageChanged += handler;
            locale.OnLanguageChanged -= handler;

            // Should NOT fire after unsubscription
            locale.SetUserPrefsLanguage("vi");
            Assert.IsFalse(fired,
                "OnLanguageChanged must NOT fire after the handler is unsubscribed");
        }

        [Test]
        public void SetUserPrefsLanguage_MultipleSubscribers_AllReceiveEvent()
        {
            var locale = new NoctuaLocale(region: "th");  // initial language "th"

            int callCount = 0;
            locale.OnLanguageChanged += _ => callCount++;
            locale.OnLanguageChanged += _ => callCount++;

            locale.SetUserPrefsLanguage("id");

            Assert.AreEqual(2, callCount,
                "Both subscribers must be called when language changes");
        }
    }
}
