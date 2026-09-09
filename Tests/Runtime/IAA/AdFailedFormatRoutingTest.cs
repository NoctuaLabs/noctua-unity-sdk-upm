using System.Reflection;
using com.noctuagames.sdk;
using com.noctuagames.sdk.AdPlaceholder;
using NUnit.Framework;
using UnityEngine.TestTools;
using IAAConfig = com.noctuagames.sdk.IAA;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Regression tests for the format-carrying ad-failure signal.
    ///
    /// <para>A banner's auto-refresh raises a load failure on its own, with no user action and no
    /// game-initiated show. That failure used to arrive as a format-agnostic
    /// <c>OnAdFailedDisplayed</c>, which <see cref="MediationManager"/> attributed to the last
    /// requested fullscreen format — so an idle player got a fullscreen interstitial house ad out of
    /// nowhere, and a real fullscreen ad on screen had its app-open guard cleared underneath it.</para>
    ///
    /// <para>The failing format now travels with the failure
    /// (<see cref="IAdNetwork.OnAdFailedDisplayedFormat"/>), so nothing has to be guessed.</para>
    /// </summary>
    [TestFixture]
    public class AdFailedFormatRoutingTest
    {
        private MockAdNetwork _primary;
        private MockAdNetwork _secondary;

        [SetUp]
        public void SetUp()
        {
            _primary   = new MockAdNetwork { NetworkName = "admob" };
            _secondary = new MockAdNetwork { NetworkName = "applovin" };
        }

        // ─── Orchestrator forwards the format ────────────────────────────────

        [Test]
        public void NetworkFailure_ForwardsFailingFormat_NotAGuess()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            string forwarded = null;
            orc.OnAdFailedDisplayedFormat += f => forwarded = f;

            _primary.TriggerAdFailedDisplayed(AdFormatKey.Banner);

            Assert.AreEqual(AdFormatKey.Banner, forwarded,
                "the format that actually failed must reach subscribers");
        }

        [Test]
        public void NetworkFailure_FromSecondary_ForwardsFailingFormat()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            string forwarded = null;
            orc.OnAdFailedDisplayedFormat += f => forwarded = f;

            _secondary.TriggerAdFailedDisplayed(AdFormatKey.Rewarded);

            Assert.AreEqual(AdFormatKey.Rewarded, forwarded);
        }

        [Test]
        public void NetworkFailure_RaisesBothEventsOnce_SoNeitherCanDisagree()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            int legacy = 0, withFormat = 0;
            orc.OnAdFailedDisplayed       += ()  => legacy++;
            orc.OnAdFailedDisplayedFormat += _   => withFormat++;

            _primary.TriggerAdFailedDisplayed(AdFormatKey.Interstitial);

            Assert.AreEqual(1, legacy,     "legacy format-agnostic event must still fire exactly once");
            Assert.AreEqual(1, withFormat, "format-carrying event must fire exactly once");
        }

        [Test]
        public void ShowWithFallback_NeitherReady_ReportsTheRequestedFormat()
        {
            _primary.InterstitialReady   = false;
            _secondary.InterstitialReady = false;
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            string forwarded = null;
            orc.OnAdFailedDisplayedFormat += f => forwarded = f;

            orc.ShowWithFallback(AdFormatKey.Interstitial, n => n.ShowInterstitial(),
                                 n => n.IsInterstitialReady());

            Assert.AreEqual(AdFormatKey.Interstitial, forwarded,
                "a terminal no-fill must report the format that was actually requested");
        }

        // ─── A banner failure must not disturb a fullscreen ad ───────────────

        [Test]
        public void BannerFailure_WhileFullscreenAdShowing_LeavesIsAdShowingTrue()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            _primary.TriggerAdDisplayed();
            Assert.IsTrue(orc.IsAdShowing, "precondition: a fullscreen ad is on screen");

            // The banner behind it fails to refresh — it never occupied the screen.
            _primary.TriggerAdFailedDisplayed(AdFormatKey.Banner);

            Assert.IsTrue(orc.IsAdShowing,
                "a banner failure must not clear the fullscreen flag out from under a showing ad");
        }

        [Test]
        public void FullscreenFailure_ClearsIsAdShowing()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            _primary.TriggerAdDisplayed();
            Assert.IsTrue(orc.IsAdShowing);

            _primary.TriggerAdFailedDisplayed(AdFormatKey.Interstitial);

            Assert.IsFalse(orc.IsAdShowing, "a fullscreen failure still releases the screen");
        }

        // ─── MediationManager no longer guesses from the last request ────────

        [Test]
        public void BannerFailure_ShowsBannerHouseAd_NeverTheFullscreenOne()
        {
            var ui = new RecordingPlaceholderUI();
            var m  = CreateManager(ui, new CrossPromotionConfig
            {
                Interstitial = new CrossPromotionEntry { AssetUrl = "https://cdn/inter.mp4" },
                Banner       = new CrossPromotionEntry { AssetUrl = "https://cdn/banner.png" },
            });

            // The game asked for an interstitial at some earlier point — the old code recorded this
            // and used it to attribute every later failure.
            m.ShowAdPlaceholder(AdPlaceholderType.Interstitial);

            // Now an unrelated banner refresh fails while the player sits idle.
            InvokePrivate(m, "NotifyAdNotAvailable", AdFormatKey.Banner);

            Assert.AreEqual(0, ui.FullscreenShowCount,
                "an idle banner failure must never pop the fullscreen interstitial house ad");
            Assert.AreEqual(1, ui.BannerShowCount,
                "it belongs on the non-modal banner surface");
        }

        [Test]
        public void ShowAdPlaceholder_ArmsNothing()
        {
            var ui = new RecordingPlaceholderUI();
            var m  = CreateManager(ui, new CrossPromotionConfig
            {
                Interstitial = new CrossPromotionEntry { AssetUrl = "https://cdn/inter.mp4" },
            });

            m.ShowAdPlaceholder(AdPlaceholderType.Interstitial);

            Assert.AreEqual(0, ui.FullscreenShowCount, "arming must not show anything by itself");
            Assert.AreEqual(0, ui.BannerShowCount);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        /// <summary>Minimal placeholder UI that only counts which surface was asked to show.</summary>
        private class RecordingPlaceholderUI : IAdPlaceholderUI
        {
            public int FullscreenShowCount;
            public int BannerShowCount;

            public void ShowAdPlaceholder(AdPlaceholderType adType, CrossPromotionEntry entry) => FullscreenShowCount++;
            public void ShowBannerPlaceholder(CrossPromotionEntry entry) => BannerShowCount++;
            public void CloseBannerPlaceholder() { }
            public void SetBannerLayout(AdPlaceholderSize size, AdPlaceholderPosition position) { }
            public void PreloadAdPlaceholder(CrossPromotionConfig config) { }
            public void CloseAdPlaceholder() { }
            public bool IsAssetCached(string assetUrl) => true;
            public void SetPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetPlaceholderShownCallback(System.Action onShown) { }
            public void SetPlaceholderFailedCallback(System.Action onFailed) { }
            public void SetBannerPlaceholderClosedCallback(System.Action onClosed) { }
            public void SetBannerPlaceholderClickedCallback(System.Action onClicked) { }
            public void SetBannerPlaceholderShownCallback(System.Action onShown) { }
            public void SetBannerPlaceholderFailedCallback(System.Action onFailed) { }
        }

        private static MediationManager CreateManager(IAdPlaceholderUI ui, CrossPromotionConfig crossPromotion)
        {
            // Constructing MediationManager runs CreateNetworks, which logs (and errors when no ad SDK
            // is compiled in). None of that is under test here, so tolerate it.
            LogAssert.ignoreFailingMessages = true;
            return new MediationManager(ui, new IAAConfig
            {
                Mediation      = "admob",
                CrossPromotion = crossPromotion,
            });
        }

        private static void InvokePrivate(MediationManager m, string method, params object[] args)
        {
            var mi = typeof(MediationManager).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"private instance method '{method}' not found");
            mi.Invoke(m, args);
        }

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;
    }
}
