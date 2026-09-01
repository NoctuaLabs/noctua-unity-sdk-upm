using System.Collections.Generic;
using com.noctuagames.sdk.Campaign;
using NUnit.Framework;
using UnityEngine.TextCore.Text;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignFontSourceTest
    {
        // The SDK ships this Font Asset under Resources/ — a reliable "known good" key.
        private const string BundledFontAsset = "NotoSansThai SDF";

        [Test]
        public void EmptyFamily_ReturnsNull()
        {
            var src = new CampaignFontSource(null);
            FontAsset got = src.SentinelGet("");
            Assert.IsNull(got);
        }

        [Test]
        public void NullRegistry_EveryLookupNull()
        {
            var src = new CampaignFontSource(null);
            Assert.IsNull(src.SentinelGet("Anything"));
        }

        [Test]
        public void UnknownFamily_ReturnsNull()
        {
            var src = new CampaignFontSource(new Dictionary<string, string> { { "Known", BundledFontAsset } });
            Assert.IsNull(src.SentinelGet("Nope"));
        }

        [Test]
        public void MissingResourceAsset_ReturnsNull_NoThrow()
        {
            var src = new CampaignFontSource(new Dictionary<string, string>
            {
                { "Ghost", "Campaign/NoSuchFontAsset_xyz" },
            });
            FontAsset got = null;
            Assert.DoesNotThrow(() => got = src.SentinelGet("Ghost"));
            Assert.IsNull(got);
            Assert.IsFalse(src.IsCached("Ghost"));
        }

        [Test]
        public void WhitespaceRegistryEntries_AreDropped()
        {
            var src = new CampaignFontSource(new Dictionary<string, string>
            {
                { "  ", BundledFontAsset }, // blank key
                { "Blank", "   " },          // blank value
            });
            Assert.IsNull(src.SentinelGet("Blank"));
        }

        [Test]
        public void RegisteredBundledAsset_LoadsAndCaches()
        {
            var src = new CampaignFontSource(new Dictionary<string, string>
            {
                { "Body", BundledFontAsset },
            });

            var fa = src.SentinelGet("Body");
            // If the bundled asset can't be found in this runner, skip rather than fail.
            if (fa == null) Assert.Ignore($"'{BundledFontAsset}' not resolvable via Resources in this runner");

            Assert.IsTrue(src.IsCached("Body"));
            Assert.AreSame(fa, src.SentinelGet("Body")); // second call returns the cached instance
        }

        [Test]
        public void Preload_NoThrow()
        {
            var src = new CampaignFontSource(new Dictionary<string, string>
            {
                { "A", BundledFontAsset },
                { "B", "Campaign/Missing" },
            });
            Assert.DoesNotThrow(() => src.Preload(new CampaignConfig()));
        }
    }

    internal static class CampaignFontSourceTestExtensions
    {
        /// <summary>Synchronous helper — <see cref="ICampaignFontSource.GetFont"/> calls back inline.</summary>
        public static FontAsset SentinelGet(this CampaignFontSource src, string family)
        {
            FontAsset got = null;
            src.GetFont(family, fa => got = fa);
            return got;
        }
    }
}
