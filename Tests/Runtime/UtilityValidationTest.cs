using System.Collections;
using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class UtilityValidationTest
    {
        // ValidateEmail tests

        [Test]
        public void ValidateEmail_Empty_ReturnsError()
        {
            var result = Utility.ValidateEmail("");
            Assert.AreEqual(Utility.errorEmailEmpty, result);
        }

        [Test]
        public void ValidateEmail_Whitespace_ReturnsError()
        {
            var result = Utility.ValidateEmail("   ");
            Assert.AreEqual(Utility.errorEmailEmpty, result);
        }

        [Test]
        public void ValidateEmail_Null_ReturnsError()
        {
            var result = Utility.ValidateEmail(null);
            Assert.AreEqual(Utility.errorEmailEmpty, result);
        }

        [Test]
        public void ValidateEmail_Valid_ReturnsEmpty()
        {
            var result = Utility.ValidateEmail("user@example.com");
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void ValidateEmail_InvalidFormat_ReturnsError()
        {
            var result = Utility.ValidateEmail("notanemail");
            Assert.AreEqual(Utility.errorEmailNotValid, result);
        }

        [Test]
        public void ValidateEmail_MissingAtSign_ReturnsError()
        {
            var result = Utility.ValidateEmail("user.example.com");
            Assert.AreEqual(Utility.errorEmailNotValid, result);
        }

        [Test]
        public void ValidateEmail_MissingDomain_ReturnsError()
        {
            var result = Utility.ValidateEmail("user@");
            Assert.AreEqual(Utility.errorEmailNotValid, result);
        }

        // ValidatePassword tests

        [Test]
        public void ValidatePassword_Empty_ReturnsError()
        {
            var result = Utility.ValidatePassword("");
            Assert.AreEqual(Utility.errorPasswordEmpty, result);
        }

        [Test]
        public void ValidatePassword_Null_ReturnsError()
        {
            var result = Utility.ValidatePassword(null);
            Assert.AreEqual(Utility.errorPasswordEmpty, result);
        }

        [Test]
        public void ValidatePassword_TooShort_ReturnsError()
        {
            var result = Utility.ValidatePassword("abc");
            Assert.AreEqual(Utility.errorPasswordShort, result);
        }

        [Test]
        public void ValidatePassword_ExactlyMinLength_ReturnsEmpty()
        {
            var result = Utility.ValidatePassword("abcdef");
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void ValidatePassword_LongPassword_ReturnsEmpty()
        {
            var result = Utility.ValidatePassword("a_very_long_password_123");
            Assert.AreEqual(string.Empty, result);
        }

        // ValidateReenterPassword tests

        [Test]
        public void ValidateReenterPassword_Empty_ReturnsError()
        {
            var result = Utility.ValidateReenterPassword("password", "");
            Assert.AreEqual(Utility.errorRePasswordEmpty, result);
        }

        [Test]
        public void ValidateReenterPassword_Null_ReturnsError()
        {
            var result = Utility.ValidateReenterPassword("password", null);
            Assert.AreEqual(Utility.errorRePasswordEmpty, result);
        }

        [Test]
        public void ValidateReenterPassword_Matching_ReturnsEmpty()
        {
            var result = Utility.ValidateReenterPassword("password", "password");
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void ValidateReenterPassword_NotMatching_ReturnsError()
        {
            var result = Utility.ValidateReenterPassword("password", "different");
            Assert.AreEqual(Utility.errorRePasswordNotMatch, result);
        }

        // ParseQueryString tests

        [Test]
        public void ParseQueryString_SimpleParams()
        {
            var result = Utility.ParseQueryString("?key=val&key2=val2");
            Assert.AreEqual("val", result["key"]);
            Assert.AreEqual("val2", result["key2"]);
        }

        [Test]
        public void ParseQueryString_WithHashFragment()
        {
            var result = Utility.ParseQueryString("?key=val#fragment");
            Assert.AreEqual("val", result["key"]);
            Assert.IsFalse(result.ContainsKey("fragment"));
        }

        [Test]
        public void ParseQueryString_EncodedChars()
        {
            var result = Utility.ParseQueryString("?name=hello%20world");
            Assert.AreEqual("hello world", result["name"]);
        }

        [Test]
        public void ParseQueryString_MissingValue_Skipped()
        {
            var result = Utility.ParseQueryString("?key=&valid=yes");
            Assert.IsFalse(result.ContainsKey("key"));
            Assert.AreEqual("yes", result["valid"]);
        }

        [Test]
        public void ParseQueryString_MissingEquals_Skipped()
        {
            var result = Utility.ParseQueryString("?noequals&valid=yes");
            Assert.IsFalse(result.ContainsKey("noequals"));
            Assert.AreEqual("yes", result["valid"]);
        }

        [Test]
        public void ParseQueryString_FullUrl()
        {
            var result = Utility.ParseQueryString("https://example.com/path?a=1&b=2");
            Assert.AreEqual("1", result["a"]);
            Assert.AreEqual("2", result["b"]);
        }

        // ContainsFlag tests

        [Test]
        public void ContainsFlag_Present_ReturnsTrue()
        {
            Assert.IsTrue(Utility.ContainsFlag("alpha,beta,gamma", "beta"));
        }

        [Test]
        public void ContainsFlag_Absent_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ContainsFlag("alpha,beta", "gamma"));
        }

        [Test]
        public void ContainsFlag_CaseInsensitive()
        {
            Assert.IsTrue(Utility.ContainsFlag("Alpha,Beta", "alpha"));
        }

        [Test]
        public void ContainsFlag_NullInput_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ContainsFlag(null, "flag"));
        }

        [Test]
        public void ContainsFlag_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ContainsFlag("", "flag"));
        }

        [Test]
        public void ContainsFlag_WithSpaces_ReturnsTrue()
        {
            Assert.IsTrue(Utility.ContainsFlag("alpha, beta , gamma", "beta"));
        }

        // ParseBooleanFeatureFlag tests

        [Test]
        public void ParseBooleanFeatureFlag_True_ReturnsTrue()
        {
            var flags = new Dictionary<string, string> { { "feature", "true" } };
            Assert.IsTrue(Utility.ParseBooleanFeatureFlag(flags, "feature"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_One_ReturnsTrue()
        {
            var flags = new Dictionary<string, string> { { "feature", "1" } };
            Assert.IsTrue(Utility.ParseBooleanFeatureFlag(flags, "feature"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_On_ReturnsTrue()
        {
            var flags = new Dictionary<string, string> { { "feature", "on" } };
            Assert.IsTrue(Utility.ParseBooleanFeatureFlag(flags, "feature"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_False_ReturnsFalse()
        {
            var flags = new Dictionary<string, string> { { "feature", "false" } };
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "feature"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_Zero_ReturnsFalse()
        {
            var flags = new Dictionary<string, string> { { "feature", "0" } };
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "feature"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_MissingKey_ReturnsFalse()
        {
            var flags = new Dictionary<string, string> { { "other", "true" } };
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "feature"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_NullDict_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(null, "feature"));
        }

        // GetCoPublisherLogo tests

        [Test]
        public void GetCoPublisherLogo_KnownCompany_ReturnsLogo()
        {
            var result = Utility.GetCoPublisherLogo("OEG JSC");
            Assert.AreEqual("OegWhiteLogo", result);
        }

        [Test]
        public void GetCoPublisherLogo_UnknownCompany_ReturnsDefault()
        {
            var result = Utility.GetCoPublisherLogo("Unknown Corp");
            Assert.AreEqual("NoctuaLogoWithText", result);
        }

        // GetTranslation (static Utility method) tests

        [Test]
        public void GetTranslation_KeyFound_ReturnsTranslation()
        {
            var translations = new Dictionary<string, string> { { "hello", "hola" } };
            var result = Utility.GetTranslation("hello", translations);
            Assert.AreEqual("hola", result);
        }

        [Test]
        public void GetTranslation_KeyNotFound_ReturnsKey()
        {
            var translations = new Dictionary<string, string> { { "hello", "hola" } };
            var result = Utility.GetTranslation("missing", translations);
            Assert.AreEqual("missing", result);
        }

        [Test]
        public void GetTranslation_NullTranslations_ReturnsKey()
        {
            var result = Utility.GetTranslation("some_key", null);
            Assert.AreEqual("some_key", result);
        }

        // PrintFields tests

        [Test]
        public void PrintFields_Null_ReturnsEmpty()
        {
            var result = Utility.PrintFields<object>(null);
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void PrintFields_PrimitiveString_ReturnsEmpty()
        {
            var result = "hello".PrintFields();
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void PrintFields_SimpleObject_ContainsFieldNames()
        {
            var obj = new SimpleTestClass { Name = "test", Value = 42 };
            var result = obj.PrintFields();
            Assert.IsTrue(result.Contains("Name: test"));
            Assert.IsTrue(result.Contains("Value: 42"));
        }

        [Test]
        public void PrintFields_Array_ContainsItems()
        {
            var arr = new SimpleTestClass[]
            {
                new SimpleTestClass { Name = "a", Value = 1 },
                new SimpleTestClass { Name = "b", Value = 2 }
            };
            var result = arr.PrintFields();
            Assert.IsTrue(result.Contains("Name: a"));
            Assert.IsTrue(result.Contains("Name: b"));
        }

        [Test]
        public void PrintFields_List_ContainsItems()
        {
            var list = new List<SimpleTestClass>
            {
                new SimpleTestClass { Name = "x", Value = 10 }
            };
            var result = list.PrintFields();
            Assert.IsTrue(result.Contains("Name: x"));
            Assert.IsTrue(result.Contains("Value: 10"));
        }

        [Test]
        public void PrintFields_Dictionary_ContainsKeyValues()
        {
            var dict = new Dictionary<string, string> { { "k1", "v1" }, { "k2", "v2" } };
            var result = dict.PrintFields();
            Assert.IsTrue(result.Contains("k1: v1"));
            Assert.IsTrue(result.Contains("k2: v2"));
        }

        public class SimpleTestClass
        {
            public string Name;
            public int Value;
        }
    }
}
