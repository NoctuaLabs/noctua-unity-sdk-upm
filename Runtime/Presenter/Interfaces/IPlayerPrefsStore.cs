namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>Tiny persistence seam so the gate is unit-testable without Unity.</summary>
    public interface IPlayerPrefsStore
    {
        string GetString(string key, string fallback);
        int GetInt(string key, int fallback);
        void SetString(string key, string value);
        void SetInt(string key, int value);
        void Save();
    }
}
