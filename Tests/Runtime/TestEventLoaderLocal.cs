using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

public class TestEventLoaderLocal
{
    private readonly object _queueLock = new();
    public List<Dictionary<string, IConvertible>> _eventQueue = new();

    public async UniTask LoadEventsFromLocalStorageAsync()
    {
        List<string> storedEvents;
        try
        {
            storedEvents = await Noctua.GetEventsAsync();
        }
        catch
        {
            storedEvents = new List<string>();
        }

        var events = new List<Dictionary<string, IConvertible>>();

        foreach (var json in storedEvents)
        {
            try
            {
                var evt = JsonConvert.DeserializeObject<Dictionary<string, IConvertible>>(json);
                if (evt != null)
                    events.Add(evt);
            }
            catch { }
        }

        lock (_queueLock)
        {
            _eventQueue = new List<Dictionary<string, IConvertible>>(events);
        }
    }

    public void PersistQueueToLocalStorage()
    {
        try
        {
            var jsonList = _eventQueue
                .Select(e => JsonConvert.SerializeObject(e))
                .ToList();

            var payload = JsonConvert.SerializeObject(jsonList);
            Noctua.SaveEvents(payload);
        }
        catch { }
    }
}
