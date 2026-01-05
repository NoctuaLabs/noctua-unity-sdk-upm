using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class TestEventLoader
{
    private readonly object _queueLock = new object();
    public List<Dictionary<string, IConvertible>> _eventQueue = new();

    public string MockJson = null;

    public int EventQueueCount => _eventQueue.Count;

    public void LoadEventsFromPlayerPrefs_Wrapper()
    {
        string eventsJson;

        if (!string.IsNullOrEmpty(MockJson))
        {
            eventsJson = MockJson;
        }
        else
        {
            eventsJson = PlayerPrefs.GetString("NoctuaEvents", "[]");
        }

        if (eventsJson == null)
        {
            eventsJson = "[]";
        }

        if (eventsJson.Length > 800000)
        {
            PlayerPrefs.SetString("NoctuaEvents", "[]");
            PlayerPrefs.Save();
            eventsJson = "[]";
        }

        var events = new List<Dictionary<string, IConvertible>>();

        try
        {
            events = JsonConvert.DeserializeObject<List<Dictionary<string, IConvertible>>>(eventsJson);
        }
        catch
        {
            var objects = new List<Dictionary<string, object>>();
            try
            {
                objects = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(eventsJson);
            }
            catch { }

            if (objects == null)
                objects = new List<Dictionary<string, object>>();

            foreach (var evt in objects)
            {
                var dict = new Dictionary<string, IConvertible>();
                foreach (var kv in evt)
                {
                    if (kv.Value is IConvertible cv)
                        dict[kv.Key] = cv;
                }
                events.Add(dict);
            }
        }

        lock (_queueLock)
        {
            _eventQueue = new List<Dictionary<string, IConvertible>>(events);
        }
    }
}
