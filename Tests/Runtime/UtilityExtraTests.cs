using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    public class UtilityExtraTests
    {
        [Test]
        public void ParseQueryString_DecodesPairs()
        {
            var result = Utility.ParseQueryString("https://x.com/cb?a=1&b=hello%20world&c=%26x");
            Assert.AreEqual("1", result["a"]);
            Assert.AreEqual("hello world", result["b"]);
            Assert.AreEqual("&x", result["c"]);
        }

        [Test]
        public void ParseQueryString_SkipsEmptyAndMalformed()
        {
            var result = Utility.ParseQueryString("?=novalue&onlykey&validkey=val");
            Assert.IsFalse(result.ContainsKey(""));
            Assert.IsFalse(result.ContainsKey("onlykey"));
            Assert.AreEqual("val", result["validkey"]);
        }

        [Test]
        public void ParseQueryString_StripsFragment()
        {
            var result = Utility.ParseQueryString("?a=1#section");
            Assert.AreEqual("1", result["a"]);
            Assert.IsFalse(result.ContainsKey("section"));
        }

        [Test]
        public void ParseQueryString_NoLeadingQuestionMark()
        {
            var result = Utility.ParseQueryString("a=1&b=2");
            Assert.AreEqual("1", result["a"]);
            Assert.AreEqual("2", result["b"]);
        }

        [Test]
        public void GetCoPublisherLogo_KnownCompany()
        {
            Assert.AreEqual("OegWhiteLogo", Utility.GetCoPublisherLogo("OEG JSC"));
        }

        [Test]
        public void GetCoPublisherLogo_UnknownCompany_FallsBack()
        {
            Assert.AreEqual("NoctuaLogoWithText", Utility.GetCoPublisherLogo("Acme Corp"));
            Assert.AreEqual("NoctuaLogoWithText", Utility.GetCoPublisherLogo(null));
        }

        [Test]
        public void ContainsFlag_FindsFlagCaseInsensitive()
        {
            Assert.IsTrue(Utility.ContainsFlag("alpha,Beta,GAMMA", "beta"));
            Assert.IsTrue(Utility.ContainsFlag("alpha, beta , gamma", "GAMMA"));
        }

        [Test]
        public void ContainsFlag_MissingReturnsFalse()
        {
            Assert.IsFalse(Utility.ContainsFlag("alpha,beta", "delta"));
            Assert.IsFalse(Utility.ContainsFlag("", "alpha"));
            Assert.IsFalse(Utility.ContainsFlag(null, "alpha"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_TrueValues()
        {
            var flags = new Dictionary<string, string>
            {
                { "a", "true" },
                { "b", "1" },
                { "c", "on" },
            };
            Assert.IsTrue(Utility.ParseBooleanFeatureFlag(flags, "a"));
            Assert.IsTrue(Utility.ParseBooleanFeatureFlag(flags, "b"));
            Assert.IsTrue(Utility.ParseBooleanFeatureFlag(flags, "c"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_FalseAndMissing()
        {
            var flags = new Dictionary<string, string>
            {
                { "x", "false" },
                { "y", "0" },
                { "z", "TRUE" },
            };
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "x"));
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "y"));
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "z"));
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "missing"));
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(null, "anything"));
        }

        [Test]
        public void GetTranslation_KeyFoundReturnsValue()
        {
            var dict = new Dictionary<string, string> { { "hello", "halo" } };
            Assert.AreEqual("halo", Utility.GetTranslation("hello", dict));
        }

        [Test]
        public void GetTranslation_KeyMissingReturnsKey()
        {
            var dict = new Dictionary<string, string> { { "hello", "halo" } };
            Assert.AreEqual("missing", Utility.GetTranslation("missing", dict));
        }

        [Test]
        public void GetTranslation_NullDictionaryReturnsKey()
        {
            Assert.AreEqual("foo", Utility.GetTranslation("foo", null));
        }

        [Test]
        public void LoadTranslations_MissingResource_ReturnsNull()
        {
            // No noctua-translation.* TextAsset exists in the test runtime Resources;
            // method should swallow the NullReferenceException and return null.
            Assert.IsNull(Utility.LoadTranslations("xx-not-a-language"));
        }

        [Test]
        public void GetPlatformType_ReturnsKnownValue()
        {
            var result = Utility.GetPlatformType();
            // The installer name is host-dependent; just confirm we get one of the known values.
            CollectionAssert.Contains(
                new[]
                {
                    PaymentType.playstore.ToString(),
                    PaymentType.appstore.ToString(),
                    PaymentType.direct.ToString(),
                },
                result);
        }

        [Test]
        public void PrintFields_PrimitiveAndNull_NoThrow()
        {
            Assert.AreEqual(string.Empty, ((object)null).PrintFields());
            Assert.AreEqual(string.Empty, 42.PrintFields());
            Assert.AreEqual(string.Empty, "hello".PrintFields());
        }

        [Test]
        public void PrintFields_ObjectWithFields_IncludesFieldNames()
        {
            var obj = new Sample { Id = 7, Name = "noctua" };
            var output = obj.PrintFields();
            StringAssert.Contains("Id", output);
            StringAssert.Contains("Name", output);
        }

        [Test]
        public void PrintFields_ListAndDictionary_NoThrow()
        {
            var list = new List<Sample> { new Sample { Id = 1, Name = "a" } };
            var dict = new Dictionary<string, Sample> { { "k", new Sample { Id = 2, Name = "b" } } };
            Assert.DoesNotThrow(() => list.PrintFields());
            Assert.DoesNotThrow(() => dict.PrintFields());
        }

        [Test]
        public void PrintFields_Array_NoThrow()
        {
            var arr = new[] { new Sample { Id = 1, Name = "a" }, new Sample { Id = 2, Name = "b" } };
            Assert.DoesNotThrow(() => arr.PrintFields());
        }

        private class Sample
        {
            public int Id;
            public string Name;
        }
    }

    public class UtilityEdgeCaseTests
    {
        // ParseQueryString edge cases

        [Test]
        public void ParseQueryString_ValueContainsEquals_SplitsOnFirstEqualsOnly()
        {
            // token=abc=def — only the first '=' is the delimiter
            var result = Utility.ParseQueryString("?token=abc=def");
            Assert.IsTrue(result.ContainsKey("token"));
            Assert.AreEqual("abc=def", result["token"]);
        }

        [Test]
        public void ParseQueryString_UnicodeEncodedValue_DecodesCorrectly()
        {
            // %E4%B8%AD%E6%96%87 = "中文" in UTF-8 percent-encoding
            var result = Utility.ParseQueryString("?lang=%E4%B8%AD%E6%96%87");
            Assert.IsTrue(result.ContainsKey("lang"));
            Assert.AreEqual("中文", result["lang"]);
        }

        [Test]
        public void ParseQueryString_MultipleEquals_NoKeyBeforeFirstEquals_Skipped()
        {
            // "=val" — splitIndex is 0, which is < 1, so it is skipped
            var result = Utility.ParseQueryString("?=val&valid=yes");
            Assert.IsFalse(result.ContainsKey(""));
            Assert.AreEqual("yes", result["valid"]);
        }

        // GetCoPublisherLogo edge cases

        [Test]
        public void GetCoPublisherLogo_EmptyString_ReturnsFallback()
        {
            Assert.AreEqual("NoctuaLogoWithText", Utility.GetCoPublisherLogo(""));
        }

        // ContainsFlag edge cases

        [Test]
        public void ContainsFlag_SingleFlagNoComma_ExactMatch_ReturnsTrue()
        {
            Assert.IsTrue(Utility.ContainsFlag("alpha", "alpha"));
        }

        [Test]
        public void ContainsFlag_FlagIsPrefixOfAnother_ReturnsFalse()
        {
            // "ab" must NOT match "abc" — flags are whole tokens
            Assert.IsFalse(Utility.ContainsFlag("abc,abcd", "ab"));
        }

        [Test]
        public void ContainsFlag_FlagWithLeadingTrailingSpaces_StillMatches()
        {
            Assert.IsTrue(Utility.ContainsFlag("  alpha  ,  beta  ", "beta"));
        }

        // ValidateEmail edge cases

        [Test]
        public void ValidateEmail_SubdomainAddress_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, Utility.ValidateEmail("user@sub.example.com"));
        }

        [Test]
        public void ValidateEmail_PlusAddressing_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, Utility.ValidateEmail("user+tag@example.com"));
        }

        // ValidatePassword boundary values

        [Test]
        public void ValidatePassword_FiveChars_ReturnsTooShortError()
        {
            // Exactly 5 chars — one below the 6-char minimum
            Assert.AreEqual(Utility.errorPasswordShort, Utility.ValidatePassword("abcde"));
        }

        [Test]
        public void ValidatePassword_SixChars_ReturnsEmpty()
        {
            // Exactly 6 chars — at the minimum boundary
            Assert.AreEqual(string.Empty, Utility.ValidatePassword("abcdef"));
        }

        // ValidateReenterPassword edge cases

        [Test]
        public void ValidateReenterPassword_BothEmpty_ReturnsRePasswordEmptyError()
        {
            Assert.AreEqual(Utility.errorRePasswordEmpty, Utility.ValidateReenterPassword("", ""));
        }

        // GetTranslation edge cases

        [Test]
        public void GetTranslation_EmptyKey_NullDict_ReturnsEmptyKey()
        {
            Assert.AreEqual("", Utility.GetTranslation("", null));
        }

        [Test]
        public void GetTranslation_EmptyKey_EmptyDict_ReturnsEmptyKey()
        {
            Assert.AreEqual("", Utility.GetTranslation("", new System.Collections.Generic.Dictionary<string, string>()));
        }

        // LoadTranslations edge cases

        [Test]
        public void LoadTranslations_NullLanguage_DoesNotThrow()
        {
            // A null language is passed to GetTranslationByLanguage; switch falls through to default "en".
            // Resources.Load will succeed or fail gracefully — either way no unhandled exception.
            Assert.DoesNotThrow(() => Utility.LoadTranslations(null));
        }

        // PrintFields edge cases

        [Test]
        public void PrintFields_ObjectWithNullField_DoesNotThrow()
        {
            var obj = new SampleWithNullable { Id = 1, Name = null };
            Assert.DoesNotThrow(() => obj.PrintFields());
        }

        [Test]
        public void PrintFields_EmptyList_DoesNotThrow()
        {
            var list = new System.Collections.Generic.List<SampleWithNullable>();
            Assert.DoesNotThrow(() => list.PrintFields());
        }

        private class SampleWithNullable
        {
            public int Id;
            public string Name;
        }
    }
}
