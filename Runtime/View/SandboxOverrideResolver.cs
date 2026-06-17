using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Pure (Unity-free) decision logic for the runtime sandbox-override flow.
    ///
    /// The side effects — reading/writing <c>PlayerPrefs</c> and showing the restart dialog —
    /// live in the composition root (<c>Noctua.Initialization</c>). This class holds only the
    /// testable parsing and comparison so the flow can be unit-tested without Unity.
    /// </summary>
    public static class SandboxOverrideResolver
    {
        /// <summary>Remote feature flag key that carries the sandbox override.</summary>
        public const string RemoteSandboxFlagKey = "sandboxEnabled";

        /// <summary>
        /// Effective sandbox value used to wire services at construction: a persisted
        /// override (resolved in a prior session) wins; otherwise the bundled config value.
        /// </summary>
        public static bool ResolveEffective(bool hasPersistedOverride, bool persistedValue, bool configValue)
            => hasPersistedOverride ? persistedValue : configValue;

        /// <summary>
        /// Parse the remote feature flags' sandbox override. Returns <c>false</c> (not found)
        /// for a null dictionary, a missing key, or an unparseable value; on success
        /// <paramref name="value"/> holds the parsed boolean.
        /// </summary>
        public static bool TryGetRemoteSandbox(IReadOnlyDictionary<string, string> remoteFlags, out bool value)
        {
            value = false;
            return remoteFlags != null
                && remoteFlags.TryGetValue(RemoteSandboxFlagKey, out var raw)
                && bool.TryParse(raw?.Trim().ToLowerInvariant(), out value);
        }

        /// <summary>
        /// Whether a restart is required: the target sandbox value differs from the value
        /// that was used to wire the current session.
        /// </summary>
        public static bool NeedsRestart(bool targetSandbox, bool currentSandbox)
            => targetSandbox != currentSandbox;

        /// <summary>
        /// Whether to clear the cached override and revert the source of truth to
        /// <c>noctuagg.json</c>. True whenever init no longer provides a sandbox flag yet a
        /// cached override is still persisted — preventing a device from being stuck on a
        /// stale value (applies whether online or offline).
        /// </summary>
        public static bool ShouldRevertToConfig(bool remoteProvidedValue, bool hasPersistedOverride)
            => !remoteProvidedValue && hasPersistedOverride;
    }
}
