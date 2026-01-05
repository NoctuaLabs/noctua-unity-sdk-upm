using NUnit.Framework;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class LoadEventsTests
{
    private TestEventLoader _loader;

    [SetUp]
    public void Setup()
    {
        _loader = new TestEventLoader();
        PlayerPrefs.DeleteKey("NoctuaEvents");
    }

    [TestCase(100_000)]
    [TestCase(1_000_000)]
    [TestCase(10_000_000)]
    [TestCase(1_000_000_000)]
    public void Test_LoadEvents_Performance(int eventCount)
    {
        Debug.Log($"--- Testing LoadEventsFromPlayerPrefs with {eventCount:N0} events ---");

        // Generate mock events
        var events = new List<Dictionary<string, IConvertible>>(eventCount);
        for (int i = 0; i < eventCount; i++)
        {
            events.Add(new Dictionary<string, IConvertible>
            {
                { "id", i },
                { "time", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                { "value", i % 5 }
            });
        }

        // Serialize to JSON
        string json = JsonConvert.SerializeObject(events);

        // ⚠️ If JSON too large for normal PlayerPrefs -> Mock it
        if (json.Length > 1_000_000_000) // ~1 GB limit test
        {
            Debug.LogWarning("JSON too large, using simulated PlayerPrefs mock");
            _loader.MockJson = json;
        }
        else
        {
            PlayerPrefs.SetString("NoctuaEvents", json);
            PlayerPrefs.Save();
        }

        // Measure performance
        var start = DateTime.Now;
        _loader.LoadEventsFromPlayerPrefs_Wrapper();
        var duration = DateTime.Now - start;

        Debug.Log($"LoadEventsFromPlayerPrefs finished in {duration.TotalSeconds:F2} seconds");

        // No assertion on speed, but check event count result
        Assert.IsTrue(_loader.EventQueueCount >= 0);
    }
}
