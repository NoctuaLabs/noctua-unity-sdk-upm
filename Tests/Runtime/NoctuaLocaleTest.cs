using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace Tests.Runtime
{
    public class NoctuaLocaleTest
    {
        [UnityTest]
        public IEnumerator GetLanguageByPriority_NoPrefsNoRegion()
        {
            // No user preferences and no region
            var locale = new NoctuaLocale("");
            locale.SetUserPrefsLanguage("");
            var language = locale.GetLanguage();

            Assert.AreEqual(language, "en");

            yield return null;
        }

        [UnityTest]
        public IEnumerator GetLanguageByPriority_NoPrefsRegionVietnam()
        {
            // No user preferences and region set to Vietnam
            var locale = new NoctuaLocale("vn");
            locale.SetUserPrefsLanguage("");
            var language = locale.GetLanguage();

            Assert.AreEqual(language, "vi");

            yield return null;
        }

        [UnityTest]
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
}
