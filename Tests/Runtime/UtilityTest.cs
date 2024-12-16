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
    }
}