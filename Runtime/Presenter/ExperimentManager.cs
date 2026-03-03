using System.Collections.Generic;

/// <summary>
/// Static manager for A/B testing experiments, session tags, and feature flags.
/// Stores key-value flags in memory for the duration of the app session.
/// </summary>
public static class ExperimentManager
{
    private const string KEY_CURRENT_EXPERIMENT = "current_experiment";
    private const string KEY_CURRENT_FEATURE = "current_feature";
    private const string KEY_CURRENT_SESSION_ID = "current_session_id";

    private static Dictionary<string, object> _experimentFlags = new Dictionary<string, object>();

    /// <summary>
    /// Sets an experiment flag with the given key and value.
    /// </summary>
    /// <param name="key">The flag key.</param>
    /// <param name="value">The flag value.</param>
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

    public static void SetSessionId(string sessionId)
    {
        SetFlag(KEY_CURRENT_SESSION_ID, sessionId);
    }

    public static string GetSessionId()
    {
        return GetFlag<string>(KEY_CURRENT_SESSION_ID, string.Empty);
    }

    public static void SetGeneralExperiment(string key, string value)
    {
        SetFlag(key, value);
    }

    public static string GetGeneralExperiment(string key)
    {
        return GetFlag<string>(key, string.Empty);
    }

    public static void SetExperiment(string experimentName)
    {
        SetFlag(KEY_CURRENT_EXPERIMENT, experimentName);
    }

    public static string GetActiveExperiment()
    {
        return GetFlag<string>(KEY_CURRENT_EXPERIMENT, string.Empty);
    }

    public static void SetSessionTag(string featureName)
    {
        SetFlag(KEY_CURRENT_FEATURE, featureName);
    }

    public static string GetSessionTag()
    {
        return GetFlag<string>(KEY_CURRENT_FEATURE, string.Empty);
    }
}
