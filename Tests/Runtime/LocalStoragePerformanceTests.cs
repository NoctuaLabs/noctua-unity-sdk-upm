using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using com.noctuagames.sdk;
using Debug = UnityEngine.Debug;

public class NoctuaEventStressTests
{
    private const float TIMEOUT_SECONDS = 10f;
    private TestEventLoaderLocal _eventLoader;

    // ---------------- SETUP ----------------
    [UnitySetUp]
    public IEnumerator Setup()
    {
        // Init Noctua safely
        yield return Noctua.InitAsync().ToCoroutine();

        Noctua.DeleteEvents();
        _eventLoader = new TestEventLoaderLocal();

        yield return null;
    }

    [UnityTest]
    public IEnumerator StressTest_1000_Events()
        => RunStressTest(1000);

    [UnityTest]
    public IEnumerator StressTest_5000_Events()
        => RunStressTest(5000);

    [UnityTest]
    public IEnumerator StressTest_10000_Events()
        => RunStressTest(10000);

    [UnityTest]
    public IEnumerator StressTest_100000_Events()
        => RunStressTest(100000);

    // ---------------- TEST CORE ----------------
    private IEnumerator RunStressTest(int eventCount)
    {
        var stopwatch = Stopwatch.StartNew();

        // Generate events
        var events = GenerateEvents(eventCount);

        // Fill queue
        _eventLoader._eventQueue = new List<Dictionary<string, IConvertible>>(events);

        // ---------- SAVE ----------
        _eventLoader.PersistQueueToLocalStorage();

        stopwatch.Stop();
        Debug.Log($"[StressTest] Persisted {eventCount} events in {stopwatch.ElapsedMilliseconds} ms");

        // ---------- LOAD ----------
        bool completed = false;
        Exception exception = null;

        _eventLoader.LoadEventsFromLocalStorageAsync()
            .ContinueWith(() => completed = true)
            .Forget();

        float timeout = TIMEOUT_SECONDS;
        while (!completed && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(completed, "LoadEventsFromLocalStorageAsync timed out");
        Assert.NotNull(_eventLoader._eventQueue);
        Assert.AreEqual(eventCount, _eventLoader._eventQueue.Count,
            $"Expected {eventCount} events but got {_eventLoader._eventQueue.Count}");

        // ---------- CLEANUP ----------
        Noctua.DeleteEvents();
        yield return null;
    }

    // ---------------- HELPERS ----------------
    private List<Dictionary<string, IConvertible>> GenerateEvents(int count)
    {
        var list = new List<Dictionary<string, IConvertible>>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new Dictionary<string, IConvertible>
            {
                { "event_name", "stress_test_event" },
                { "index", i },
                { "timestamp", DateTime.UtcNow.ToString("o") }
            });
        }

        return list;
    }
}
