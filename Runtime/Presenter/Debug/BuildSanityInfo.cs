namespace com.noctuagames.sdk
{
    /// <summary>
    /// Aggregated build / config metadata for the Inspector "Build"
    /// sanity panel. Pure data; constructed once per panel render via
    /// <see cref="BuildSanityProvider"/>.
    ///
    /// Field semantics:
    ///   * String fields use "" for "not configured" / "unavailable".
    ///   * Int sentinel `-1` means "platform doesn't expose this metric".
    ///   * Sensitive values (Adjust app token) are pre-masked at the
    ///     provider layer — game code that snapshots this struct can
    ///     surface or log it without leaking secrets.
    /// </summary>
    public sealed class BuildSanityInfo
    {
        public string UnitySdkVersion        { get; set; } = "";
        public string NativeSdkVersion       { get; set; } = "";
        public string BundleId               { get; set; } = "";
        public string AppVersion             { get; set; } = "";
        public string UnityVersion           { get; set; } = "";

        /// <summary>SHA-256 hex of the raw `noctuagg.json` bytes.</summary>
        public string ConfigChecksum         { get; set; } = "";

        /// <summary>Last 6 chars of the Adjust app token, prefixed with "…". Empty if unset.</summary>
        public string AdjustAppTokenMasked   { get; set; } = "";

        /// <summary>Firebase project ID from the bundled config plist / google-services.json.</summary>
        public string FirebaseProjectId      { get; set; } = "";

        /// <summary>True iff `google-services.json` is reachable in StreamingAssets (Android relevance).</summary>
        public bool   GoogleServicesPresent  { get; set; }

        /// <summary>iOS only — count of `SKAdNetworkItems` in Info.plist. -1 elsewhere.</summary>
        public int    SkAdNetworksCount      { get; set; } = -1;

        /// <summary>Android only — count of manifest-declared permissions. -1 elsewhere.</summary>
        public int    AndroidPermissionsCount{ get; set; } = -1;

        public bool   IsSandbox              { get; set; }
        public string Region                 { get; set; } = "";

        /// <summary>
        /// Pretty-printed full <c>noctuagg.json</c> contents. Empty string
        /// outside sandbox mode (the composition root only retains the raw
        /// JSON when sandbox is enabled, to avoid keeping secrets resident
        /// in production memory). The Build tab renders this verbatim so
        /// devs can verify every config field — game ID, base URLs, tracker
        /// configs, eventMaps, Firebase project IDs — at a glance.
        /// </summary>
        public string RawConfigJson          { get; set; } = "";
    }
}
