using System.Collections.Generic;

public static class ExperimentManager
{
    private const string KEY_EXPERIMENT_ACTIVE = "experiment_active";

    private static Dictionary<string, object> _experimentFlags = new Dictionary<string, object>();

    public static void SetFlag(string key, object value)
    {
        _experimentFlags[key] = value;
    }

    public static T GetFlag<T>(string key, T defaultValue = default)
    {
        if (_experimentFlags.TryGetValue(key, out var val))
        {
            return (T)val;
        }
        return defaultValue;
    }

    public static void Clear()
    {
        _experimentFlags.Clear();
    }

    public static void SetExperiment(string experimentName)
    {
        SetFlag(KEY_EXPERIMENT_ACTIVE, experimentName);
    }

    public static string GetActiveExperiment()
    {
        return GetFlag<string>(KEY_EXPERIMENT_ACTIVE, string.Empty);
    }
}
