using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine.TestTools;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode + PlayMode tests for <see cref="Utility"/>.
    ///
    /// EditMode (<c>[Test]</c>) covers the pure-sync helpers:
    ///   ParseQueryString, GetCoPublisherLogo, ContainsFlag,
    ///   ParseBooleanFeatureFlag, ValidateEmail, ValidatePassword,
    ///   ValidateReenterPassword, GetTranslation.
    ///
    /// PlayMode (<c>[UnityTest]</c>) covers RetryAsyncTask.
    /// </summary>
    public class UtilityTest
    {
        // ═══════════════════════════════════════════════════════════════════
        // ParseQueryString
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void ParseQueryString_SimplePair_ParsedCorrectly()
        {
            var result = Utility.ParseQueryString("?foo=bar");
            Assert.AreEqual("bar", result["foo"]);
        }

        [Test]
        public void ParseQueryString_MultiplePairs_AllParsed()
        {
            var result = Utility.ParseQueryString("?a=1&b=2&c=3");
            Assert.AreEqual("1", result["a"]);
            Assert.AreEqual("2", result["b"]);
            Assert.AreEqual("3", result["c"]);
        }

        [Test]
        public void ParseQueryString_WithoutLeadingQuestionMark_StillParsed()
        {
            var result = Utility.ParseQueryString("key=value");
            Assert.AreEqual("value", result["key"]);
        }

        [Test]
        public void ParseQueryString_WithFragment_FragmentStripped()
        {
            var result = Utility.ParseQueryString("?a=1#section");
            Assert.IsTrue(result.ContainsKey("a"), "Key 'a' must be present");
            Assert.AreEqual("1", result["a"]);
            Assert.IsFalse(result.ContainsKey("section"), "Fragment must not appear as a key");
        }

        [Test]
        public void ParseQueryString_UriEncodedValue_Decoded()
        {
            var result = Utility.ParseQueryString("?name=Hello%20World");
            Assert.AreEqual("Hello World", result["name"],
                "Percent-encoded spaces must be decoded");
        }

        [Test]
        public void ParseQueryString_PairWithNoEquals_Skipped()
        {
            var result = Utility.ParseQueryString("?badpair&good=1");
            Assert.IsFalse(result.ContainsKey("badpair"),
                "A pair without '=' must be skipped");
            Assert.AreEqual("1", result["good"]);
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetCoPublisherLogo
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetCoPublisherLogo_KnownCompany_ReturnsSpecificLogo()
        {
            Assert.AreEqual("OegWhiteLogo", Utility.GetCoPublisherLogo("OEG JSC"));
        }

        [Test]
        public void GetCoPublisherLogo_UnknownCompany_ReturnsNoctuaDefault()
        {
            Assert.AreEqual("NoctuaLogoWithText", Utility.GetCoPublisherLogo("Acme Corp"));
        }

        [Test]
        public void GetCoPublisherLogo_NullCompany_ReturnsNoctuaDefault()
        {
            Assert.AreEqual("NoctuaLogoWithText", Utility.GetCoPublisherLogo(null));
        }

        // ═══════════════════════════════════════════════════════════════════
        // ContainsFlag
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void ContainsFlag_FlagPresent_ReturnsTrue()
        {
            Assert.IsTrue(Utility.ContainsFlag("featureA,featureB,featureC", "featureB"));
        }

        [Test]
        public void ContainsFlag_FlagAbsent_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ContainsFlag("featureA,featureC", "featureB"));
        }

        [Test]
        public void ContainsFlag_CaseInsensitive_ReturnsTrue()
        {
            Assert.IsTrue(Utility.ContainsFlag("FeatureA,FeatureB", "featureb"),
                "Flag matching must be case-insensitive");
        }

        [Test]
        public void ContainsFlag_NullFlags_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ContainsFlag(null, "anything"),
                "Null flags string must not throw and must return false");
        }

        [Test]
        public void ContainsFlag_FlagWithWhitespace_TrimmingApplied()
        {
            Assert.IsTrue(Utility.ContainsFlag("featureA, featureB , featureC", "featureB"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // ParseBooleanFeatureFlag
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void ParseBooleanFeatureFlag_ValueTrue_ReturnsTrue()
        {
            var flags = new Dictionary<string, string> { { "f", "true" } };
            Assert.IsTrue(Utility.ParseBooleanFeatureFlag(flags, "f"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_ValueOne_ReturnsTrue()
        {
            var flags = new Dictionary<string, string> { { "f", "1" } };
            Assert.IsTrue(Utility.ParseBooleanFeatureFlag(flags, "f"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_ValueOn_ReturnsTrue()
        {
            var flags = new Dictionary<string, string> { { "f", "on" } };
            Assert.IsTrue(Utility.ParseBooleanFeatureFlag(flags, "f"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_ValueFalse_ReturnsFalse()
        {
            var flags = new Dictionary<string, string> { { "f", "false" } };
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "f"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_KeyAbsent_ReturnsFalse()
        {
            var flags = new Dictionary<string, string> { { "other", "true" } };
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(flags, "missing"));
        }

        [Test]
        public void ParseBooleanFeatureFlag_NullDict_ReturnsFalse()
        {
            Assert.IsFalse(Utility.ParseBooleanFeatureFlag(null, "f"),
                "Null dictionary must not throw and must return false");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ValidateEmail
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void ValidateEmail_ValidAddress_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, Utility.ValidateEmail("user@example.com"));
        }

        [Test]
        public void ValidateEmail_EmptyString_ReturnsErrorMessage()
        {
            Assert.IsNotEmpty(Utility.ValidateEmail(""), "Empty email must return an error");
        }

        [Test]
        public void ValidateEmail_WhitespaceOnly_ReturnsErrorMessage()
        {
            Assert.IsNotEmpty(Utility.ValidateEmail("   "), "Whitespace-only email must return an error");
        }

        [Test]
        public void ValidateEmail_NullInput_ReturnsErrorMessage()
        {
            Assert.IsNotEmpty(Utility.ValidateEmail(null), "Null email must return an error");
        }

        [Test]
        public void ValidateEmail_MissingAtSign_ReturnsErrorMessage()
        {
            Assert.IsNotEmpty(Utility.ValidateEmail("userexample.com"));
        }

        [Test]
        public void ValidateEmail_SubdomainAddress_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, Utility.ValidateEmail("user@mail.example.co.uk"));
        }

        // ═══════════════════════════════════════════════════════════════════
        // ValidatePassword
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void ValidatePassword_SixChars_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, Utility.ValidatePassword("abc123"),
                "Exactly 6 characters must be accepted");
        }

        [Test]
        public void ValidatePassword_EmptyString_ReturnsErrorMessage()
        {
            Assert.IsNotEmpty(Utility.ValidatePassword(""), "Empty password must return an error");
        }

        [Test]
        public void ValidatePassword_NullInput_ReturnsErrorMessage()
        {
            Assert.IsNotEmpty(Utility.ValidatePassword(null), "Null password must return an error");
        }

        [Test]
        public void ValidatePassword_FiveChars_ReturnsErrorMessage()
        {
            Assert.IsNotEmpty(Utility.ValidatePassword("ab123"),
                "5-character password must be rejected (minimum is 6)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ValidateReenterPassword
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void ValidateReenterPassword_MatchingPasswords_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, Utility.ValidateReenterPassword("secret", "secret"));
        }

        [Test]
        public void ValidateReenterPassword_MismatchedPasswords_ReturnsErrorMessage()
        {
            Assert.IsNotEmpty(Utility.ValidateReenterPassword("secret", "different"),
                "Mismatched passwords must return an error");
        }

        [Test]
        public void ValidateReenterPassword_EmptyReentry_ReturnsErrorMessage()
        {
            Assert.IsNotEmpty(Utility.ValidateReenterPassword("secret", ""),
                "Empty re-entry must return an error");
        }

        [Test]
        public void ValidateReenterPassword_NullReentry_ReturnsErrorMessage()
        {
            Assert.IsNotEmpty(Utility.ValidateReenterPassword("secret", null),
                "Null re-entry must return an error");
        }

        [Test]
        public void ValidateReenterPassword_CaseSensitiveComparison()
        {
            Assert.IsNotEmpty(Utility.ValidateReenterPassword("Secret", "secret"),
                "Password comparison must be case-sensitive");
        }

        // ═══════════════════════════════════════════════════════════════════
        // GetTranslation
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetTranslation_KeyFound_ReturnsLocalizedText()
        {
            var t = new Dictionary<string, string> { { "greet", "Hello" } };
            Assert.AreEqual("Hello", Utility.GetTranslation("greet", t));
        }

        [Test]
        public void GetTranslation_KeyMissing_ReturnsFallbackKey()
        {
            var t = new Dictionary<string, string> { { "other", "X" } };
            Assert.AreEqual("greet", Utility.GetTranslation("greet", t),
                "Missing key must fall back to returning the key itself");
        }

        [Test]
        public void GetTranslation_NullDictionary_ReturnsFallbackKey()
        {
            Assert.AreEqual("greet", Utility.GetTranslation("greet", null),
                "Null translations dict must return the key without throwing");
        }


        [Test]
        [Timeout(5000)]
        public async Task RetryAsyncTask_SuccessOnFirstTry()
        {
            {
                var result = await Utility.RetryAsyncTask(async () => await UniTask.FromResult("Success"));
                Assert.AreEqual("Success", result);
            }
        );

        [Test]
        [Timeout(5000)]
        public async Task RetryAsyncTask_SuccessAfterRetries()
            {
                int attempt = 0;

                var result = await Utility.RetryAsyncTask(
                    {
                        if (attempt < 2)
                        {
                            attempt++;

                            throw new NoctuaException(NoctuaErrorCode.Networking, "Networking error");
                        }

                        return await UniTask.FromResult("Success");
                    }
                );

                Assert.AreEqual("Success", result);
                Assert.AreEqual(2, attempt);
            }
        );

        [Test]
        [Timeout(5000)]
        public async Task RetryAsyncTask_ThrowsNonNetworkingException()
            {
                bool exceptionThrown = false;

                try
                {
                    await Utility.RetryAsyncTask<object>(
                        async () => { throw new NoctuaException(NoctuaErrorCode.Application, "Application error"); }
                    );
                }
                catch (NoctuaException e)
                {
                    exceptionThrown = true;
                    Assert.AreNotEqual(NoctuaErrorCode.Networking, (NoctuaErrorCode)e.ErrorCode);
                }

                Assert.IsTrue(exceptionThrown);
            }
        );

        [Test]
        [Timeout(5000)]
        public async Task RetryAsyncTask_ThrowsOtherNonNetworkingException()
            {
                bool exceptionThrown = false;

                try
                {
                    await Utility.RetryAsyncTask<object>(async () => { throw new Exception("Other error"); });
                }
                catch (Exception e)
                {
                    exceptionThrown = true;
                }

                Assert.IsTrue(exceptionThrown);
            }
        );

        [Test]
        [Timeout(5000)]
        public async Task RetryAsyncTask_MaxRetriesReached()
            {
                int attempt = 0;
                double initialDelay = 0.5;
                double exponent = 2.0;

                Stopwatch stopwatch = new Stopwatch();
                List<double> delays = new List<double>();

                try
                {
                    stopwatch.Start();

                    await Utility.RetryAsyncTask<object>(
                        {
                            delays.Add(stopwatch.Elapsed.TotalSeconds);
                            stopwatch.Restart();
                            attempt++;

                            throw new NoctuaException(NoctuaErrorCode.Networking, "Networking error");
                        },
                        maxRetries: 3,
                        initialDelaySeconds: initialDelay,
                        exponent: exponent
                    );
                }
                catch (NoctuaException e)
                {
                    stopwatch.Stop();
                    Assert.AreEqual(NoctuaErrorCode.Networking, (NoctuaErrorCode)e.ErrorCode);
                }

                Assert.AreEqual(4, attempt); // Initial try + 3 retries
                
                Assert.AreEqual(4, delays.Count);
                Assert.AreEqual(0, delays[0], 0.1);
                Assert.AreEqual(initialDelay, delays[1], 0.5);
                Assert.AreEqual(initialDelay * exponent, delays[2], 0.5 * exponent);
                Assert.AreEqual(initialDelay * exponent * exponent, delays[3], 0.5 * exponent * exponent);
            }
        );
        
        [Test]
        [Timeout(5000)]
        public async Task RetryAsyncTask_MaxDelayReached()
            {
                int attempt = 0;

                Stopwatch stopwatch = new Stopwatch();
                List<double> delays = new List<double>();

                try
                {
                    stopwatch.Start();

                    await Utility.RetryAsyncTask<object>(
                        {
                            delays.Add(stopwatch.Elapsed.TotalSeconds);
                            stopwatch.Restart();
                            attempt++;
                            UnityEngine.Debug.Log($"Retry Attempt {attempt}");

                            throw new NoctuaException(NoctuaErrorCode.Networking, "Networking error");
                        },
                        maxRetries: 6,
                        maxDelaySeconds: 4.0
                    );
                }
                catch (NoctuaException e)
                {
                    stopwatch.Stop();
                    Assert.AreEqual(NoctuaErrorCode.Networking, (NoctuaErrorCode)e.ErrorCode);
                }

                Assert.AreEqual(7, attempt); // Initial try + 3 retries
                
                Assert.AreEqual(7, delays.Count);

                for (int i = 4; i < delays.Count; i++)
                {
                    Assert.AreEqual(4.0, delays[i], 1);
                }
            }
        );
    }
}