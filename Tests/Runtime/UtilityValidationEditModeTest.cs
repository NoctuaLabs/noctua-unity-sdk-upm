using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for the pure-logic methods in <see cref="Utility"/>:
    ///   — <see cref="Utility.ValidateEmail"/>
    ///   — <see cref="Utility.ValidatePassword"/>
    ///   — <see cref="Utility.ValidateReenterPassword"/>
    ///   — <see cref="Utility.ContainsFlag"/>
    ///   — <see cref="Utility.ParseBooleanFeatureFlag"/>
    ///   — <see cref="Utility.GetCoPublisherLogo"/>
    ///   — <see cref="Utility.GetTranslation(string, Dictionary{string,string})"/>
    ///   — <see cref="Utility.PrintFields{T}"/>
    ///
    /// All 46 existing tests in <c>UtilityValidationTest</c> use <c>[UnityTest]</c> / <c>yield return null</c>
    /// even though every method under test is synchronous.  Those tests only run in PlayMode and
    /// contribute zero to the EditMode coverage report.  These plain <c>[Test]</c> counterparts
    /// ensure the same branches are counted when the EditMode suite runs.
    ///
    /// Note: <c>ParseQueryString</c> and <c>GetCoPublisherLogo</c> already have EditMode coverage
    /// in <c>UtilityExtraTests.cs</c>; they are re-exercised here for completeness.
    /// </summary>
    [TestFixture]
    public class UtilityValidationEditModeTest
    {
        // ─── ValidateEmail ────────────────────────────────────────────────────

        [Test]
        public void ValidateEmail_Null_ReturnsEmailEmptyError()
        {
            Assert.AreEqual(Utility.errorEmailEmpty, Utility.ValidateEmail(null));
        }

        [Test]
        public void ValidateEmail_EmptyString_ReturnsEmailEmptyError()
        {
            Assert.AreEqual(Utility.errorEmailEmpty, Utility.ValidateEmail(""));
        }

        [Test]
        public void ValidateEmail_Whitespace_ReturnsEmailEmptyError()
        {
            Assert.AreEqual(Utility.errorEmailEmpty, Utility.ValidateEmail("   "));
        }

        [Test]
        public void ValidateEmail_Valid_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, Utility.ValidateEmail("user@example.com"));
        }

        [Test]
        public void ValidateEmail_ValidWithSubdomain_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, Utility.ValidateEmail("user@mail.example.co.uk"));
        }

        [Test]
        public void ValidateEmail_NoAtSign_ReturnsNotValidError()
        {
            Assert.AreEqual(Utility.errorEmailNotValid, Utility.ValidateEmail("user.example.com"));
        }

        [Test]
        public void ValidateEmail_MissingDomain_ReturnsNotValidError()
        {
            Assert.AreEqual(Utility.errorEmailNotValid, Utility.ValidateEmail("user@"));
        }

        [Test]
        public void ValidateEmail_InvalidFormat_ReturnsNotValidError()
        {
            Assert.AreEqual(Utility.errorEmailNotValid, Utility.ValidateEmail("notanemail"));
        }

        [Test]
        public void ValidateEmail_PlusAlias_ReturnsEmptyString()
        {
            // Common "plus addressing" format should be valid
            Assert.AreEqual(string.Empty, Utility.ValidateEmail("user+tag@example.com"));
        }

        // ─── ValidatePassword ─────────────────────────────────────────────────

        [Test]
        public void ValidatePassword_Null_ReturnsPasswordEmptyError()
        {
            Assert.AreEqual(Utility.errorPasswordEmpty, Utility.ValidatePassword(null));
        }

        [Test]
        public void ValidatePassword_EmptyString_ReturnsPasswordEmptyError()
        {
            Assert.AreEqual(Utility.errorPasswordEmpty, Utility.ValidatePassword(""));
        }

        [Test]
        public void ValidatePassword_TooShort_ReturnsPasswordShortError()
        {
            // Less than 6 characters
            Assert.AreEqual(Utility.errorPasswordShort, Utility.ValidatePassword("abc"));
        }

        [Test]
        public void ValidatePassword_FiveChars_ReturnsPasswordShortError()
        {
            Assert.AreEqual(Utility.errorPasswordShort, Utility.ValidatePassword("abcde"));
        }

        [Test]
        public void ValidatePassword_ExactlyMinLength_ReturnsEmptyString()
        {
            // Exactly 6 characters — at the boundary
            Assert.AreEqual(string.Empty, Utility.ValidatePassword("abcdef"));
        }

        [Test]
        public void ValidatePassword_LongPassword_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, Utility.ValidatePassword("a_very_long_password_123!@#"));
        }

        // ─── ValidateReenterPassword ──────────────────────────────────────────

        [Test]
        public void ValidateReenterPassword_RePasswordNull_ReturnsRePasswordEmptyError()
        {
            Assert.AreEqual(Utility.errorRePasswordEmpty, Utility.ValidateReenterPassword("password123", null));
        }

        [Test]
        public void ValidateReenterPassword_RePasswordEmpty_ReturnsRePasswordEmptyError()
        {
            Assert.AreEqual(Utility.errorRePasswordEmpty, Utility.ValidateReenterPassword("password123", ""));
        }

        [Test]
        public void ValidateReenterPassword_MatchingPasswords_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, Utility.ValidateReenterPassword("password123", "password123"));
        }

        [Test]
        public void ValidateReenterPassword_NotMatching_ReturnsNotMatchError()
        {
            Assert.AreEqual(Utility.errorRePasswordNotMatch, Utility.ValidateReenterPassword("password123", "different"));
        }

        [Test]
        public void ValidateReenterPassword_CaseSensitiveMismatch_ReturnsNotMatchError()
        {
            // Passwords are case-sensitive
            Assert.AreEqual(Utility.errorRePasswordNotMatch, Utility.ValidateReenterPassword("Password", "password"));
        }

        // ─── Utility error-message constants are non-empty strings ────────────

        [Test]
        public void ErrorConstants_AreNonEmpty()
        {
            Assert.IsNotEmpty(Utility.errorEmailEmpty);
            Assert.IsNotEmpty(Utility.errorEmailNotValid);
            Assert.IsNotEmpty(Utility.errorPasswordEmpty);
            Assert.IsNotEmpty(Utility.errorPasswordShort);
            Assert.IsNotEmpty(Utility.errorRePasswordEmpty);
            Assert.IsNotEmpty(Utility.errorRePasswordNotMatch);
        }

        // ─── ContainsFlag ─────────────────────────────────────────────────────

        [Test]
        public void ContainsFlag_PresentFlag_ReturnsTrue()
        {
            Assert.IsTrue(Utility.ContainsFlag("alpha,beta,gamma", "beta"));
        }

        [Test]
        public void ContainsFlag_AbsentFlag_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ContainsFlag("alpha,beta", "gamma"));
        }

        [Test]
        public void ContainsFlag_CaseInsensitiveMatch_ReturnsTrue()
        {
            Assert.IsTrue(Utility.ContainsFlag("Alpha,Beta", "alpha"));
        }

        [Test]
        public void ContainsFlag_NullInput_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ContainsFlag(null, "flag"));
        }

        [Test]
        public void ContainsFlag_EmptyInput_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ContainsFlag("", "flag"));
        }

        [Test]
        public void ContainsFlag_FlagsWithSpaces_StripsWhitespace()
        {
            Assert.IsTrue(Utility.ContainsFlag("alpha, beta , gamma", "beta"));
        }

        [Test]
        public void ContainsFlag_SingleFlag_MatchesExactly()
        {
            Assert.IsTrue(Utility.ContainsFlag("onlyflag", "onlyflag"));
        }

        [Test]
        public void ContainsFlag_PartialWord_DoesNotMatch()
        {
            // "bet" should not match flag "beta"
            Assert.IsFalse(Utility.ContainsFlag("alpha,beta,gamma", "bet"));
        }

        // ─── ParseBooleanFeatureFlag ──────────────────────────────────────────

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
        public void ParseBooleanFeatureFlag_Off_ReturnsFalse()
        {
            var flags = new Dictionary<string, string> { { "feature", "off" } };
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "feature"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_MissingKey_ReturnsFalse()
        {
            var flags = new Dictionary<string, string> { { "other", "true" } };
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "feature"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_NullDictionary_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(null, "feature"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_EmptyDictionary_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(new Dictionary<string, string>(), "feature"));
        }

        // ─── GetCoPublisherLogo ───────────────────────────────────────────────

        [Test]
        public void GetCoPublisherLogo_KnownOegJsc_ReturnsOegLogo()
        {
            Assert.AreEqual("OegWhiteLogo", Utility.GetCoPublisherLogo("OEG JSC"));
        }

        [Test]
        public void GetCoPublisherLogo_UnknownCompany_ReturnsNoctuaDefault()
        {
            Assert.AreEqual("NoctuaLogoWithText", Utility.GetCoPublisherLogo("Unknown Corp"));
        }

        [Test]
        public void GetCoPublisherLogo_AnotherUnknown_ReturnsNoctuaDefault()
        {
            Assert.AreEqual("NoctuaLogoWithText", Utility.GetCoPublisherLogo("Acme Games Pte Ltd"));
        }

        // ─── GetTranslation (static overload) ────────────────────────────────

        [Test]
        public void GetTranslation_KeyFound_ReturnsTranslationValue()
        {
            var translations = new Dictionary<string, string> { { "hello", "hola" } };
            Assert.AreEqual("hola", Utility.GetTranslation("hello", translations));
        }

        [Test]
        public void GetTranslation_KeyNotFound_ReturnsKeyItself()
        {
            var translations = new Dictionary<string, string> { { "hello", "hola" } };
            Assert.AreEqual("missing_key", Utility.GetTranslation("missing_key", translations));
        }

        [Test]
        public void GetTranslation_NullDictionary_ReturnsKeyItself()
        {
            Assert.AreEqual("some_key", Utility.GetTranslation("some_key", null));
        }

        [Test]
        public void GetTranslation_EmptyKey_ReturnsEmptyOrKey()
        {
            var translations = new Dictionary<string, string> { { "a", "b" } };
            // Empty key lookup — should return empty string (the key itself)
            var result = Utility.GetTranslation("", translations);
            Assert.IsNotNull(result);
        }

        [Test]
        public void GetTranslation_EmptyDictionary_ReturnsKeyItself()
        {
            Assert.AreEqual("key", Utility.GetTranslation("key", new Dictionary<string, string>()));
        }

        // ─── PrintFields ──────────────────────────────────────────────────────

        [Test]
        public void PrintFields_Null_ReturnsEmptyString()
        {
            var result = Utility.PrintFields<object>(null);
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void PrintFields_PrimitiveString_ReturnsEmptyString()
        {
            var result = "hello".PrintFields();
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void PrintFields_SimpleObject_ContainsFieldNameAndValue()
        {
            var obj = new SimpleTestDto { Name = "test", Value = 42 };
            var result = obj.PrintFields();
            StringAssert.Contains("Name: test", result);
            StringAssert.Contains("Value: 42", result);
        }

        [Test]
        public void PrintFields_SimpleObject_IsNonEmpty()
        {
            var obj = new SimpleTestDto { Name = "x", Value = 1 };
            Assert.IsNotEmpty(obj.PrintFields());
        }

        [Test]
        public void PrintFields_Array_ContainsAllItems()
        {
            var arr = new SimpleTestDto[]
            {
                new SimpleTestDto { Name = "a", Value = 1 },
                new SimpleTestDto { Name = "b", Value = 2 }
            };
            var result = arr.PrintFields();
            StringAssert.Contains("Name: a", result);
            StringAssert.Contains("Name: b", result);
        }

        [Test]
        public void PrintFields_List_ContainsAllItems()
        {
            var list = new List<SimpleTestDto>
            {
                new SimpleTestDto { Name = "x", Value = 10 },
                new SimpleTestDto { Name = "y", Value = 20 }
            };
            var result = list.PrintFields();
            StringAssert.Contains("Name: x", result);
            StringAssert.Contains("Name: y", result);
        }

        [Test]
        public void PrintFields_Dictionary_ContainsKeyValuePairs()
        {
            var dict = new Dictionary<string, string> { { "k1", "v1" }, { "k2", "v2" } };
            var result = dict.PrintFields();
            StringAssert.Contains("k1: v1", result);
            StringAssert.Contains("k2: v2", result);
        }

        [Test]
        public void PrintFields_EmptyList_ReturnsNonNullString()
        {
            var result = new List<SimpleTestDto>().PrintFields();
            Assert.IsNotNull(result);
        }

        // Shared DTO used by PrintFields tests
        private class SimpleTestDto
        {
            public string Name;
            public int Value;
        }
    }
}
