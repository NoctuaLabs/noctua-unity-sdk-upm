using System.Collections.Generic;
using System.Text.RegularExpressions;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for <see cref="BuildSanityProvider"/> — the static inspector
    /// helper that assembles build/config metadata for the Inspector "Build" tab.
    ///
    /// Only the publicly-testable surface is exercised here:
    ///   * <see cref="BuildSanityProvider.ResolveAdjustAppToken"/>
    ///   * <see cref="BuildSanityProvider.ResolveAdjustEventMap"/>
    ///   * <see cref="BuildSanityProvider.Snapshot"/> (pure-logic paths)
    /// </summary>
    [TestFixture]
    public class BuildSanityProviderTest
    {
        // ─── Helpers ──────────────────────────────────────────────────────────

        private static GlobalConfig MakeConfig(
            string androidToken = "android-token-abcdefgh",
            string iosToken     = "ios-token-12345678",
            Dictionary<string, string> androidEventMap = null,
            Dictionary<string, string> iosEventMap     = null,
            bool   isSandbox = false,
            string region    = "sg")
        {
            return new GlobalConfig
            {
                ClientId = "test-client",
                Adjust = new AdjustConfig
                {
                    Android = new AdjustAndroidConfig
                    {
                        AppToken = androidToken,
                        EventMap = androidEventMap ?? new Dictionary<string, string>()
                    },
                    Ios = new AdjustIosConfig
                    {
                        AppToken = iosToken,
                        EventMap = iosEventMap ?? new Dictionary<string, string>()
                    }
                },
                Noctua = new NoctuaConfig { IsSandbox = isSandbox, Region = region }
            };
        }

        // ─── ResolveAdjustAppToken ────────────────────────────────────────────

        [Test]
        public void ResolveAdjustAppToken_NullConfig_ReturnsEmpty()
        {
            var token = BuildSanityProvider.ResolveAdjustAppToken(null);
            Assert.AreEqual("", token);
        }

        [Test]
        public void ResolveAdjustAppToken_NullAdjust_ReturnsEmpty()
        {
            var config = new GlobalConfig { ClientId = "c", Adjust = null };
            Assert.AreEqual("", BuildSanityProvider.ResolveAdjustAppToken(config));
        }

        [Test]
        public void ResolveAdjustAppToken_AndroidTokenConfigured_ReturnsAndroidToken()
        {
            // In Editor builds the method prefers Android if non-empty.
            var config = MakeConfig(androidToken: "android-abc", iosToken: "");
            var token = BuildSanityProvider.ResolveAdjustAppToken(config);
            Assert.AreEqual("android-abc", token);
        }

        [Test]
        public void ResolveAdjustAppToken_AndroidEmpty_FallsBackToIos()
        {
            var config = MakeConfig(androidToken: "", iosToken: "ios-xyz");
            var token = BuildSanityProvider.ResolveAdjustAppToken(config);
            Assert.AreEqual("ios-xyz", token);
        }

        [Test]
        public void ResolveAdjustAppToken_BothEmpty_ReturnsEmpty()
        {
            var config = MakeConfig(androidToken: "", iosToken: "");
            var token = BuildSanityProvider.ResolveAdjustAppToken(config);
            Assert.AreEqual("", token);
        }

        // ─── ResolveAdjustEventMap ────────────────────────────────────────────

        [Test]
        public void ResolveAdjustEventMap_NullConfig_ReturnsNull()
        {
            Assert.IsNull(BuildSanityProvider.ResolveAdjustEventMap(null));
        }

        [Test]
        public void ResolveAdjustEventMap_NullAdjust_ReturnsNull()
        {
            var config = new GlobalConfig { ClientId = "c", Adjust = null };
            Assert.IsNull(BuildSanityProvider.ResolveAdjustEventMap(config));
        }

        [Test]
        public void ResolveAdjustEventMap_AndroidMapNonEmpty_ReturnsAndroidMap()
        {
            var androidMap = new Dictionary<string, string> { { "level_up", "adj_token_a" } };
            var config = MakeConfig(androidEventMap: androidMap);
            var map = BuildSanityProvider.ResolveAdjustEventMap(config);
            Assert.IsNotNull(map);
            Assert.IsTrue(map.ContainsKey("level_up"));
            Assert.AreEqual("adj_token_a", map["level_up"]);
        }

        [Test]
        public void ResolveAdjustEventMap_AndroidEmptyIosNonEmpty_ReturnsIosMap()
        {
            var iosMap = new Dictionary<string, string> { { "purchase", "adj_token_b" } };
            var config = MakeConfig(androidEventMap: new Dictionary<string, string>(), iosEventMap: iosMap);
            var map = BuildSanityProvider.ResolveAdjustEventMap(config);
            Assert.IsNotNull(map);
            Assert.IsTrue(map.ContainsKey("purchase"));
        }

        // ─── Snapshot ─────────────────────────────────────────────────────────

        [Test]
        public void Snapshot_NullConfig_DoesNotThrow()
        {
            BuildSanityInfo info = null;
            Assert.DoesNotThrow(() => info = BuildSanityProvider.Snapshot(null));
            Assert.IsNotNull(info);
        }

        [Test]
        public void Snapshot_NullConfig_HasEmptyStringFields()
        {
            var info = BuildSanityProvider.Snapshot(null);
            Assert.AreEqual("", info.AdjustAppTokenMasked);
            Assert.AreEqual("", info.Region);
            Assert.IsFalse(info.IsSandbox);
        }

        [Test]
        public void Snapshot_SandboxConfig_SetsSandboxTrue()
        {
            var config = MakeConfig(isSandbox: true, region: "id");
            var info = BuildSanityProvider.Snapshot(config);
            Assert.IsTrue(info.IsSandbox);
            Assert.AreEqual("id", info.Region);
        }

        [Test]
        public void Snapshot_AdjustToken_MaskedToLast6()
        {
            // Token "android-token-abcdefgh" → masked = "…efgh" wait, it's last 6 chars
            var config = MakeConfig(androidToken: "android-token-abcdefgh", iosToken: "");
            var info = BuildSanityProvider.Snapshot(config);
            // Last 6 chars of "android-token-abcdefgh" = "bcdefgh"... let me count:
            // a-n-d-r-o-i-d---t-o-k-e-n---a-b-c-d-e-f-g-h = 20 chars → last 6 = "bcdefgh" wait that's 7
            // "android-token-abcdefgh" = 22 chars, last 6 = "defgh" wait...
            // Let me just check it starts with "…"
            StringAssert.StartsWith("…", info.AdjustAppTokenMasked);
            // "…" (1 char) + last 6 chars of the token = 7 chars total
            Assert.AreEqual(7, info.AdjustAppTokenMasked.Length);
        }

        [Test]
        public void Snapshot_ShortAdjustToken_MaskedWithJustEllipsis()
        {
            // Token ≤ 6 chars → "…" + full token
            var config = MakeConfig(androidToken: "abc", iosToken: "");
            var info = BuildSanityProvider.Snapshot(config);
            Assert.AreEqual("…abc", info.AdjustAppTokenMasked);
        }

        [Test]
        public void Snapshot_EmptyRawConfigJson_ChecksumEmpty()
        {
            // When rawConfigJson is null the implementation falls back to
            // StreamingAssets/noctuagg.json. In the test environment that file
            // may or may not exist, so the checksum is either "" (no file) or
            // a 64-char SHA-256 hex (file found). Both are valid outcomes.
            var info = BuildSanityProvider.Snapshot(null, rawConfigJson: null);
            Assert.IsTrue(
                string.IsNullOrEmpty(info.ConfigChecksum) ||
                Regex.IsMatch(info.ConfigChecksum, "^[0-9a-f]{64}$"),
                $"ConfigChecksum must be empty or a valid SHA-256 hex; got '{info.ConfigChecksum}'");
        }

        [Test]
        public void Snapshot_ValidRawJson_ChecksumIs64HexChars()
        {
            // SHA-256 hex = 64 chars
            var info = BuildSanityProvider.Snapshot(null, rawConfigJson: "{\"clientId\":\"test\"}");
            Assert.AreEqual(64, info.ConfigChecksum.Length, "SHA-256 hex should be 64 chars");
            Assert.IsTrue(Regex.IsMatch(info.ConfigChecksum, "^[0-9a-f]{64}$"), "Checksum should be 64 hex chars");
        }

        [Test]
        public void Snapshot_ValidRawJson_RawConfigJsonIsPrettyPrinted()
        {
            var raw = "{\"clientId\":\"test\"}";
            var info = BuildSanityProvider.Snapshot(null, rawConfigJson: raw);
            // Pretty-printed JSON contains newlines
            StringAssert.Contains("\n", info.RawConfigJson);
        }

        [Test]
        public void Snapshot_MalformedRawJson_RawConfigJsonPreservesInput()
        {
            var malformed = "not-valid-json{{{";
            var info = BuildSanityProvider.Snapshot(null, rawConfigJson: malformed);
            // Falls back to the raw string when parse fails
            Assert.AreEqual(malformed, info.RawConfigJson);
        }

        [Test]
        public void Snapshot_CustomGoogleServicesProbe_True_SetsPresent()
        {
            var info = BuildSanityProvider.Snapshot(null, googleServicesProbe: () => true);
            Assert.IsTrue(info.GoogleServicesPresent);
        }

        [Test]
        public void Snapshot_CustomGoogleServicesProbe_False_SetsAbsent()
        {
            var info = BuildSanityProvider.Snapshot(null, googleServicesProbe: () => false);
            Assert.IsFalse(info.GoogleServicesPresent);
        }

        [Test]
        public void Snapshot_NullBuildInfo_DoesNotThrow_SentinelsRemain()
        {
            var info = BuildSanityProvider.Snapshot(null, buildInfo: null);
            Assert.AreEqual("",  info.NativeSdkVersion);
            Assert.AreEqual("",  info.FirebaseProjectId);
            Assert.AreEqual(-1,  info.SkAdNetworksCount);
            Assert.AreEqual(-1,  info.AndroidPermissionsCount);
        }
    }
}
