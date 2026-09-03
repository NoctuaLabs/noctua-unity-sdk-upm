using System.Collections.Generic;
using com.noctuagames.sdk.LiveOpsCampaign;
using NUnit.Framework;
using UnityEngine.TextCore.Text;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignFontSourceTest
    {
        // The SDK ships this Font Asset under Resources/ — a reliable "known good" path.
        private const string BundledFontAsset = "NotoSansThai SDF";

        [Test]
        public void EmptyOrNullPath_ReturnsNull()
        {
            var src = new CampaignFontSource();
            Assert.IsNull(src.SentinelGet(""));
            Assert.IsNull(src.SentinelGet(null));
            Assert.IsNull(src.SentinelGet("   "));
        }

        [Test]
        public void MissingResourceAsset_ReturnsNull_NoThrow_NotCached()
        {
            var src = new CampaignFontSource();
            FontAsset got = null;
            Assert.DoesNotThrow(() => got = src.SentinelGet("Campaign/NoSuchFontAsset_xyz"));
            Assert.IsNull(got);
            Assert.IsFalse(src.IsCached("Campaign/NoSuchFontAsset_xyz"));
        }

        [Test]
        public void BundledAsset_LoadsAndCachesByPath()
        {
            var src = new CampaignFontSource();

            var fa = src.SentinelGet(BundledFontAsset);
            if (fa == null) Assert.Ignore($"'{BundledFontAsset}' not resolvable via Resources in this runner");

            Assert.IsTrue(src.IsCached(BundledFontAsset));
            Assert.AreSame(fa, src.SentinelGet(BundledFontAsset));           // cache hit
            Assert.AreSame(fa, src.SentinelGet("  " + BundledFontAsset + " ")); // trimmed key hits the same entry
        }

        [Test]
        public void Preload_WalksItemFonts_NoThrow()
        {
            var src = new CampaignFontSource();
            var config = new CampaignConfig
            {
                Campaigns = new List<CampaignItem>
                {
                    new CampaignItem { Id = "a", Fonts = new Dictionary<string, string> { { "Brand", BundledFontAsset } } },
                    new CampaignItem { Id = "b" }, // no fonts — must be skipped, not NRE
                },
            };

            Assert.DoesNotThrow(() => src.Preload(config));
            Assert.DoesNotThrow(() => src.Preload(null));
        }
    }

    [TestFixture]
    public class CampaignFontPathResolutionTest
    {
        private static readonly Dictionary<string, string> ItemFonts =
            new Dictionary<string, string> { { "Brand", "Fonts/Item-Brand" }, { "Blank", "  " } };

        [Test]
        public void ItemRegistryKey_ResolvesToItsPath()
        {
            Assert.AreEqual("Fonts/Item-Brand", CampaignRenderer.ResolveFontPath("Brand", ItemFonts));
        }

        [Test]
        public void NonKey_IsUsedAsTheResourcesPathDirectly()
        {
            Assert.AreEqual("Fonts/Honk-Regular",
                CampaignRenderer.ResolveFontPath("Fonts/Honk-Regular", ItemFonts));
            Assert.AreEqual("Fonts/Honk-Regular",
                CampaignRenderer.ResolveFontPath("  Fonts/Honk-Regular  ", null));
        }

        [Test]
        public void BlankRegistryValue_FallsThroughToTheFamilyString()
        {
            Assert.AreEqual("Blank", CampaignRenderer.ResolveFontPath("Blank", ItemFonts));
        }

        [Test]
        public void EmptyFamily_ReturnsNull()
        {
            Assert.IsNull(CampaignRenderer.ResolveFontPath(null, ItemFonts));
            Assert.IsNull(CampaignRenderer.ResolveFontPath("   ", null));
        }
    }

    internal static class CampaignFontSourceTestExtensions
    {
        /// <summary>Synchronous helper — <see cref="ICampaignFontSource.GetFont"/> calls back inline.</summary>
        public static FontAsset SentinelGet(this CampaignFontSource src, string resourcesPath)
        {
            FontAsset got = null;
            src.GetFont(resourcesPath, fa => got = fa);
            return got;
        }
    }
}
