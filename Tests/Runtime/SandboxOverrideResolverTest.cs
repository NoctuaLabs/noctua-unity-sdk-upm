using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Covers every decision branch of the runtime sandbox-override flowchart:
    ///   - constructor "read PlayerPrefs override if any"  -> <see cref="SandboxOverrideResolver.ResolveEffective"/>
    ///   - "Is RemoteFeatureFlags have sandboxEnabled?"     -> <see cref="SandboxOverrideResolver.TryGetRemoteSandbox"/>
    ///   - "if value different from current?"               -> <see cref="SandboxOverrideResolver.NeedsRestart"/>
    /// </summary>
    public class SandboxOverrideResolverTest
    {
        private static Dictionary<string, string> Flags(string sandboxEnabled) =>
            new Dictionary<string, string> { ["sandboxEnabled"] = sandboxEnabled };

        // ----- ResolveEffective: "constructor reads PlayerPrefs override if any" -----

        [Test]
        public void ResolveEffective_NoOverride_UsesConfig_True()
        {
            Assert.IsTrue(SandboxOverrideResolver.ResolveEffective(hasPersistedOverride: false, persistedValue: false, configValue: true));
        }

        [Test]
        public void ResolveEffective_NoOverride_UsesConfig_False()
        {
            Assert.IsFalse(SandboxOverrideResolver.ResolveEffective(hasPersistedOverride: false, persistedValue: true, configValue: false));
        }

        [Test]
        public void ResolveEffective_OverrideTrue_WinsOverConfigFalse()
        {
            Assert.IsTrue(SandboxOverrideResolver.ResolveEffective(hasPersistedOverride: true, persistedValue: true, configValue: false));
        }

        [Test]
        public void ResolveEffective_OverrideFalse_WinsOverConfigTrue()
        {
            Assert.IsFalse(SandboxOverrideResolver.ResolveEffective(hasPersistedOverride: true, persistedValue: false, configValue: true));
        }

        // ----- TryGetRemoteSandbox: "Is RemoteFeatureFlags have sandboxEnabled?" (No branch) -----

        [Test]
        public void TryGetRemoteSandbox_NullDictionary_NotFound()
        {
            Assert.IsFalse(SandboxOverrideResolver.TryGetRemoteSandbox(null, out var value));
            Assert.IsFalse(value);
        }

        [Test]
        public void TryGetRemoteSandbox_EmptyDictionary_NotFound()
        {
            Assert.IsFalse(SandboxOverrideResolver.TryGetRemoteSandbox(new Dictionary<string, string>(), out _));
        }

        [Test]
        public void TryGetRemoteSandbox_MissingKey_NotFound()
        {
            var flags = new Dictionary<string, string> { ["vnLegalPurposeEnabled"] = "true" };
            Assert.IsFalse(SandboxOverrideResolver.TryGetRemoteSandbox(flags, out _));
        }

        [Test]
        public void TryGetRemoteSandbox_InvalidValue_NotFound()
        {
            Assert.IsFalse(SandboxOverrideResolver.TryGetRemoteSandbox(Flags("yes"), out _));
        }

        [Test]
        public void TryGetRemoteSandbox_NumericValue_NotFound()
        {
            // Remote flags are boolean strings; "1" is not a valid bool.
            Assert.IsFalse(SandboxOverrideResolver.TryGetRemoteSandbox(Flags("1"), out _));
        }

        [Test]
        public void TryGetRemoteSandbox_EmptyValue_NotFound()
        {
            Assert.IsFalse(SandboxOverrideResolver.TryGetRemoteSandbox(Flags(""), out _));
        }

        // ----- TryGetRemoteSandbox: "Yes" branch -----

        [Test]
        public void TryGetRemoteSandbox_True_FoundTrue()
        {
            Assert.IsTrue(SandboxOverrideResolver.TryGetRemoteSandbox(Flags("true"), out var value));
            Assert.IsTrue(value);
        }

        [Test]
        public void TryGetRemoteSandbox_False_FoundFalse()
        {
            Assert.IsTrue(SandboxOverrideResolver.TryGetRemoteSandbox(Flags("false"), out var value));
            Assert.IsFalse(value);
        }

        [Test]
        public void TryGetRemoteSandbox_MixedCaseAndWhitespace_Parsed()
        {
            Assert.IsTrue(SandboxOverrideResolver.TryGetRemoteSandbox(Flags("  TRUE  "), out var value));
            Assert.IsTrue(value);
        }

        // ----- NeedsRestart: "if value different from current?" -----

        [Test]
        public void NeedsRestart_SameTrue_False()
        {
            Assert.IsFalse(SandboxOverrideResolver.NeedsRestart(targetSandbox: true, currentSandbox: true));
        }

        [Test]
        public void NeedsRestart_SameFalse_False()
        {
            Assert.IsFalse(SandboxOverrideResolver.NeedsRestart(targetSandbox: false, currentSandbox: false));
        }

        [Test]
        public void NeedsRestart_RemoteTrue_CurrentFalse_True()
        {
            Assert.IsTrue(SandboxOverrideResolver.NeedsRestart(targetSandbox: true, currentSandbox: false));
        }

        [Test]
        public void NeedsRestart_RemoteFalse_CurrentTrue_True()
        {
            Assert.IsTrue(SandboxOverrideResolver.NeedsRestart(targetSandbox: false, currentSandbox: true));
        }

        // ----- ShouldRevertToConfig: clear stale cache + revert to noctuagg.json -----

        [Test]
        public void ShouldRevertToConfig_RemoteAbsentHasCache_True()
        {
            // Flag no longer provided, a cache still exists -> revert (online or offline).
            Assert.IsTrue(SandboxOverrideResolver.ShouldRevertToConfig(
                remoteProvidedValue: false, hasPersistedOverride: true));
        }

        [Test]
        public void ShouldRevertToConfig_RemoteProvided_False()
        {
            // Remote provided a value — it wins; not a revert case.
            Assert.IsFalse(SandboxOverrideResolver.ShouldRevertToConfig(
                remoteProvidedValue: true, hasPersistedOverride: true));
        }

        [Test]
        public void ShouldRevertToConfig_RemoteAbsentNoCache_False()
        {
            // Nothing cached to clear — already on noctuagg.json.
            Assert.IsFalse(SandboxOverrideResolver.ShouldRevertToConfig(
                remoteProvidedValue: false, hasPersistedOverride: false));
        }
    }
}
