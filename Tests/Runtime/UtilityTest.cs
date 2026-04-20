using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine.TestTools;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    public class UtilityTest
    {
        [UnityTest]
        public IEnumerator RetryAsyncTask_SuccessOnFirstTry() => UniTask.ToCoroutine(
            async () =>
            {
                var result = await Utility.RetryAsyncTask(async () => await UniTask.FromResult("Success"));
                Assert.AreEqual("Success", result);
            }
        );

        [UnityTest]
        public IEnumerator RetryAsyncTask_SuccessAfterRetries() => UniTask.ToCoroutine(
            async () =>
            {
                int attempt = 0;

                var result = await Utility.RetryAsyncTask(
                    async () =>
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

        [UnityTest]
        public IEnumerator RetryAsyncTask_ThrowsNonNetworkingException() => UniTask.ToCoroutine(
            async () =>
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

        [UnityTest]
        public IEnumerator RetryAsyncTask_ThrowsOtherNonNetworkingException() => UniTask.ToCoroutine(
            async () =>
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

        [UnityTest]
        public IEnumerator RetryAsyncTask_MaxRetriesReached() => UniTask.ToCoroutine(
            async () =>
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
                        async () =>
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
        
        // ─── ApplyErrorTranslation ────────────────────────────────────────────

        [Test]
        public void ApplyErrorTranslation_WithTranslations_UpdatesAllSixFields()
        {
            var translations = new Dictionary<string, string>
            {
                { "ErrorEmailEmpty",       "Email kosong" },
                { "ErrorEmailNotValid",    "Email tidak valid" },
                { "ErrorPasswordEmpty",    "Password kosong" },
                { "ErrorPasswordShort",    "Password terlalu pendek" },
                { "ErrorRePasswordEmpty",  "Ulangi password kosong" },
                { "ErrorRePasswordNotMatch", "Password tidak cocok" },
            };

            Utility.ApplyErrorTranslation(translations);

            Assert.AreEqual("Email kosong",           Utility.errorEmailEmpty);
            Assert.AreEqual("Email tidak valid",      Utility.errorEmailNotValid);
            Assert.AreEqual("Password kosong",        Utility.errorPasswordEmpty);
            Assert.AreEqual("Password terlalu pendek", Utility.errorPasswordShort);
            Assert.AreEqual("Ulangi password kosong", Utility.errorRePasswordEmpty);
            Assert.AreEqual("Password tidak cocok",   Utility.errorRePasswordNotMatch);
        }

        [Test]
        public void ApplyErrorTranslation_NullDict_FallsBackToKey()
        {
            // Reset to a known state first
            Utility.ApplyErrorTranslation(new Dictionary<string, string>());
            // With null translations, GetTranslation returns the key itself
            Utility.ApplyErrorTranslation(null);

            // All fields should now be the enum key names
            Assert.AreEqual("ErrorEmailEmpty",        Utility.errorEmailEmpty);
            Assert.AreEqual("ErrorEmailNotValid",     Utility.errorEmailNotValid);
            Assert.AreEqual("ErrorPasswordEmpty",     Utility.errorPasswordEmpty);
            Assert.AreEqual("ErrorPasswordShort",     Utility.errorPasswordShort);
            Assert.AreEqual("ErrorRePasswordEmpty",   Utility.errorRePasswordEmpty);
            Assert.AreEqual("ErrorRePasswordNotMatch", Utility.errorRePasswordNotMatch);
        }

        // ─── GetPlatformType ──────────────────────────────────────────────────

        [Test]
        public void GetPlatformType_InTestEnvironment_ReturnsDirect()
        {
            // In editor/test runtime Application.installerName is neither
            // "com.android.vending" nor "com.apple.appstore" → hits the fallback
            var result = Utility.GetPlatformType();
            Assert.AreEqual(PaymentType.direct.ToString(), result);
        }

        // ─── LoadTranslations ─────────────────────────────────────────────────

        [Test]
        public void LoadTranslations_DoesNotThrow()
        {
            // Resource may or may not exist in test environment — only verify no crash
            Dictionary<string, string> result = null;
            Assert.DoesNotThrow(() => result = Utility.LoadTranslations("en"));
            // result is either null (resource missing) or a valid dict — both acceptable
        }

        [UnityTest]
        public IEnumerator RetryAsyncTask_MaxDelayReached() => UniTask.ToCoroutine(
            async () =>
            {
                int attempt = 0;

                Stopwatch stopwatch = new Stopwatch();
                List<double> delays = new List<double>();

                try
                {
                    stopwatch.Start();

                    await Utility.RetryAsyncTask<object>(
                        async () =>
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