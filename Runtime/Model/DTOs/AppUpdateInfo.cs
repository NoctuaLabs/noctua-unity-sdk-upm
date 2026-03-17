namespace com.noctuagames.sdk
{
    public class AppUpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public bool IsImmediateAllowed { get; set; }
        public bool IsFlexibleAllowed { get; set; }
        public int AvailableVersionCode { get; set; }
        public int StalenessDays { get; set; }
    }

    public enum AppUpdateResult
    {
        Success = 0,
        UserCancelled = 1,
        Failed = 2,
        NotAvailable = 3
    }
}
