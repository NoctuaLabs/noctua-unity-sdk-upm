using UnityEngine;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>Default <see cref="IPlayerPrefsStore"/> backed by <c>UnityEngine.PlayerPrefs</c>.</summary>
    public sealed class UnityPlayerPrefsStore : IPlayerPrefsStore
    {
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
