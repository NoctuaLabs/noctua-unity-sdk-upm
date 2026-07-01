using System;
using System.Reflection;
using com.noctuagames.sdk;
using com.noctuagames.sdk.AdPlaceholder;
using NUnit.Framework;
using UnityEngine.TestTools;
using IAAConfig = com.noctuagames.sdk.IAA;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Unit tests for the cross-promotion placeholder orchestration in <see cref="MediationManager"/>:
    /// the fallback is shown only when no real ad displays, it drives the ad lifecycle
    /// (OnAdDisplayed → OnAdClicked → OnAdClosed), the "no real ad" signal is deferred while it shows,
    /// a real ad force-closing it suppresses OnAdClosed, and it never grants a reward.
    ///
    /// These exercise the Presenter-layer logic with a recording <see cref="IAdPlaceholderUI"/> stub —
    /// no networks, no HTTP, no VideoPlayer (all device-only) are involved.
    /// </summary>
    [TestFixture]
    public class CrossPromotionPlaceholderTest
    {
        /// <summary>Recording placeholder UI: captures the closed/clicked callbacks and Show/Close calls.</summary>
        private class RecordingAdPlaceholderUI : IAdPlaceholderUI
        {
            public int ShowCount;
            public int CloseCount;
            public AdPlaceholderType LastShownType;
            public CrossPromotionEntry LastShownEntry;
            public Action OnClosed;
            public Action OnClicked;
            public Action OnShown;
            public Action OnFailed;

            /// <summary>Controls what <see cref="IsAssetCached"/> returns (readiness tests).</summary>
            public bool AssetCached = true;

            public void ShowAdPlaceholder(AdPlaceholderType adType, CrossPromotionEntry entry)
            {
                ShowCount++;
                LastShownType = adType;
                LastShownEntry = entry;
            }

            public void PreloadAdPlaceholder(CrossPromotionConfig config) { }
            public void SetPlaceholderClosedCallback(Action onClosed) => OnClosed = onClosed;
            public void SetPlaceholderClickedCallback(Action onClicked) => OnClicked = onClicked;
            public void SetPlaceholderShownCallback(Action onShown) => OnShown = onShown;
            public void SetPlaceholderFailedCallback(Action onFailed) => OnFailed = onFailed;

            // Simulate the real UI: closing it fires the registered closed callback.
            public void CloseAdPlaceholder()
            {
                CloseCount++;
                OnClosed?.Invoke();
            }

            public bool IsAssetCached(string assetUrl) => AssetCached;
        }

        [SetUp]
        public void SetUp()
        {
            // Constructing MediationManager runs CreateNetworks, which logs (and errors when no ad SDK
            // is compiled in). None of that is under test here, so tolerate it.
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        // Mirror the proven MinimalIaa("admob") construction pattern used by the sibling
        // MediationManager fixtures so the constructor's CreateNetworks runs its normal path.
        // The cross-promotion logic under test never touches the orchestrator/network.
        private static IAAConfig ConfigWith(CrossPromotionConfig crossPromotion) => new IAAConfig
        {
            Mediation = "admob",
            CrossPromotion = crossPromotion,
        };

        private static CrossPromotionEntry Entry(string url) => new CrossPromotionEntry { AssetUrl = url };

        private static MediationManager Create(RecordingAdPlaceholderUI ui, IAAConfig config)
        {
            // Constructing MediationManager runs CreateNetworks which logs; tolerate it (matches the
            // sibling MediationManager fixtures' pattern).
            LogAssert.ignoreFailingMessages = true;
            return new MediationManager(ui, config);
        }

        private static void InvokePrivate(MediationManager m, string method, params object[] args)
        {
            var mi = typeof(MediationManager).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"private instance method '{method}' not found");
            mi.Invoke(m, args);
        }

        private static bool InvokeShowFallback(MediationManager m, AdPlaceholderType type)
        {
            var mi = typeof(MediationManager).GetMethod("ShowCrossPromoFallback", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "ShowCrossPromoFallback not found");
            return (bool)mi.Invoke(m, new object[] { type });
        }

        // ─── No-ad signal → fallback ─────────────────────────────────────────

        [Test]
        public void NotifyAdNotAvailable_WithCrossPromo_RequestsShow_ThenFiresOnAdDisplayedOnRender()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Rewarded = Entry("https://cdn/rew.mp4") }));

            bool displayed = false, notAvailable = false;
            m.OnAdDisplayed += () => displayed = true;
            m.OnAdNotAvailable += _ => notAvailable = true;

            InvokePrivate(m, "NotifyAdNotAvailable", AdFormatKey.Rewarded);

            Assert.AreEqual(1, ui.ShowCount, "cross-promo show should be requested");
            Assert.AreEqual(AdPlaceholderType.Rewarded, ui.LastShownType);
            Assert.AreEqual("https://cdn/rew.mp4", ui.LastShownEntry.AssetUrl);
            Assert.IsFalse(displayed, "OnAdDisplayed must wait until the asset actually renders");
            Assert.IsFalse(notAvailable, "OnAdNotAvailable must be deferred while the asset loads");

            // Simulate the asset rendering.
            ui.OnShown.Invoke();
            Assert.IsTrue(displayed, "OnAdDisplayed should fire once the asset renders");
            Assert.IsFalse(notAvailable);
        }

        [Test]
        public void AssetNotReady_FiresOnAdNotAvailable_NotDisplayed()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Interstitial = Entry("https://cdn/inter.mp4") }));

            bool displayed = false; string notAvailableFormat = null;
            m.OnAdDisplayed += () => displayed = true;
            m.OnAdNotAvailable += f => notAvailableFormat = f;

            InvokePrivate(m, "NotifyAdNotAvailable", AdFormatKey.Interstitial);
            Assert.AreEqual(1, ui.ShowCount, "show should be requested");

            // Simulate the asset failing to load (not ready / offline / no cache).
            ui.OnFailed.Invoke();

            Assert.IsFalse(displayed, "OnAdDisplayed must NOT fire when the asset never rendered");
            Assert.AreEqual(AdFormatKey.Interstitial, notAvailableFormat,
                "asset-not-ready must report OnAdNotAvailable for the requested format");
        }

        [Test]
        public void NotifyAdNotAvailable_NoCrossPromoConfigured_FiresOnAdNotAvailableImmediately()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(null));

            bool notAvailable = false; string reportedFormat = null;
            m.OnAdNotAvailable += f => { notAvailable = true; reportedFormat = f; };

            InvokePrivate(m, "NotifyAdNotAvailable", AdFormatKey.Interstitial);

            Assert.AreEqual(0, ui.ShowCount, "nothing should be shown when cross-promo is not configured");
            Assert.IsTrue(notAvailable, "OnAdNotAvailable should fire immediately when no cross-promo can show");
            Assert.AreEqual(AdFormatKey.Interstitial, reportedFormat);
        }

        [Test]
        public void NotifyAdNotAvailable_FormatWithoutEntry_FiresOnAdNotAvailable()
        {
            var ui = new RecordingAdPlaceholderUI();
            // Only interstitial configured; request rewarded.
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Interstitial = Entry("https://cdn/inter.mp4") }));

            bool notAvailable = false;
            m.OnAdNotAvailable += _ => notAvailable = true;

            InvokePrivate(m, "NotifyAdNotAvailable", AdFormatKey.Rewarded);

            Assert.AreEqual(0, ui.ShowCount, "no rewarded entry → no cross-promo");
            Assert.IsTrue(notAvailable);
        }

        [Test]
        public void NotifyAdNotAvailable_AppOpen_NeverShowsPlaceholder()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Interstitial = Entry("https://cdn/inter.mp4") }));

            bool notAvailable = false;
            m.OnAdNotAvailable += _ => notAvailable = true;

            // app_open has no placeholder type.
            InvokePrivate(m, "NotifyAdNotAvailable", AdFormatKey.AppOpen);

            Assert.AreEqual(0, ui.ShowCount);
            Assert.IsTrue(notAvailable);
        }

        // ─── Lifecycle: shown → dismissed / clicked ──────────────────────────

        [Test]
        public void DismissingCrossPromo_FiresOnAdClosed_NoReward()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Interstitial = Entry("https://cdn/inter.mp4") }));

            Assert.IsTrue(InvokeShowFallback(m, AdPlaceholderType.Interstitial), "fallback show should be requested");
            ui.OnShown.Invoke(); // asset renders → now "shown"

            bool closed = false;
            m.OnAdClosed += () => closed = true;

            // Simulate the user dismissing the placeholder (UI → closed callback).
            ui.OnClosed.Invoke();

            Assert.IsTrue(closed, "OnAdClosed should fire when the cross-promo is dismissed");
        }

        [Test]
        public void CtaTap_FiresOnAdClicked()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Rewarded = Entry("https://cdn/rew.png") }));

            bool clicked = false;
            m.OnAdClicked += () => clicked = true;

            Assert.IsNotNull(ui.OnClicked, "clicked callback should be registered by the manager");
            ui.OnClicked.Invoke();

            Assert.IsTrue(clicked, "OnAdClicked should fire on a CTA tap");
        }

        [Test]
        public void ForceClose_WhenRealAdShows_SuppressesOnAdClosed()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Interstitial = Entry("https://cdn/inter.mp4") }));

            Assert.IsTrue(InvokeShowFallback(m, AdPlaceholderType.Interstitial));
            ui.OnShown.Invoke(); // asset renders → now "shown"

            bool closed = false;
            m.OnAdClosed += () => closed = true;

            // A real ad is taking over → force-close.
            m.CloseAdPlaceholder(force: true);

            Assert.AreEqual(1, ui.CloseCount, "the UI should be closed");
            Assert.IsFalse(closed, "force-close (real ad taking over) must suppress the cross-promo OnAdClosed");
        }

        [Test]
        public void ShowCrossPromoFallback_AlreadyShown_DoesNotShowTwice()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Rewarded = Entry("https://cdn/rew.mp4") }));

            Assert.IsTrue(InvokeShowFallback(m, AdPlaceholderType.Rewarded));
            Assert.IsTrue(InvokeShowFallback(m, AdPlaceholderType.Rewarded), "returns true (already showing)");

            Assert.AreEqual(1, ui.ShowCount, "should not re-show while one is already up");
        }

        [Test]
        public void DismissAfterReShow_FiresOnAdClosedAgain()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Rewarded = Entry("https://cdn/rew.mp4") }));

            int closedCount = 0;
            m.OnAdClosed += () => closedCount++;

            // First round: show → render → user dismiss.
            Assert.IsTrue(InvokeShowFallback(m, AdPlaceholderType.Rewarded));
            ui.OnShown.Invoke();
            ui.OnClosed.Invoke();

            // Second round: a later no-fill shows again, renders, then dismiss again.
            Assert.IsTrue(InvokeShowFallback(m, AdPlaceholderType.Rewarded),
                "should be able to show again after the previous one was dismissed");
            ui.OnShown.Invoke();
            ui.OnClosed.Invoke();

            Assert.AreEqual(2, ui.ShowCount);
            Assert.AreEqual(2, closedCount, "each dismissal should fire OnAdClosed");
        }

        // ─── Cross-promo readiness (cache-gated) ─────────────────────────────

        private static bool InvokeIsCrossPromoAvailable(MediationManager m, AdPlaceholderType type)
        {
            var mi = typeof(MediationManager).GetMethod("IsCrossPromoAvailable", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "IsCrossPromoAvailable not found");
            return (bool)mi.Invoke(m, new object[] { type });
        }

        [Test]
        public void IsCrossPromoAvailable_ConfiguredAndCached_ReturnsTrue()
        {
            var ui = new RecordingAdPlaceholderUI { AssetCached = true };
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Rewarded = Entry("https://cdn/rew.mp4") }));

            Assert.IsTrue(InvokeIsCrossPromoAvailable(m, AdPlaceholderType.Rewarded),
                "a configured + cached creative should report available");
        }

        [Test]
        public void IsCrossPromoAvailable_ConfiguredButNotCached_ReturnsFalse()
        {
            var ui = new RecordingAdPlaceholderUI { AssetCached = false };
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Rewarded = Entry("https://cdn/rew.mp4") }));

            Assert.IsFalse(InvokeIsCrossPromoAvailable(m, AdPlaceholderType.Rewarded),
                "configured but uncached must NOT report available (would flash a blank placeholder)");
        }

        [Test]
        public void IsCrossPromoAvailable_NoEntryForFormat_ReturnsFalse()
        {
            var ui = new RecordingAdPlaceholderUI { AssetCached = true };
            // Only interstitial configured; ask about rewarded.
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Interstitial = Entry("https://cdn/inter.mp4") }));

            Assert.IsFalse(InvokeIsCrossPromoAvailable(m, AdPlaceholderType.Rewarded),
                "no entry configured for the format → not available even when cache says yes");
        }

        // ─── Format → placeholder type mapping ───────────────────────────────

        [Test]
        public void MapFormatToPlaceholderType_MapsEachFormat()
        {
            var mi = typeof(MediationManager).GetMethod("MapFormatToPlaceholderType",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "MapFormatToPlaceholderType not found");

            Assert.AreEqual(AdPlaceholderType.Interstitial, mi.Invoke(null, new object[] { AdFormatKey.Interstitial }));
            Assert.AreEqual(AdPlaceholderType.Rewarded, mi.Invoke(null, new object[] { AdFormatKey.Rewarded }));
            Assert.AreEqual(AdPlaceholderType.RewardedInterstitial, mi.Invoke(null, new object[] { AdFormatKey.RewardedInterstitial }));
            Assert.AreEqual(AdPlaceholderType.Banner, mi.Invoke(null, new object[] { AdFormatKey.Banner }));
            Assert.IsNull(mi.Invoke(null, new object[] { AdFormatKey.AppOpen }), "app_open has no placeholder type");
        }

        [Test]
        public void ResolveCrossPromotionEntry_ReturnsPerFormatEntry()
        {
            var mi = typeof(MediationManager).GetMethod("ResolveCrossPromotionEntry",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "ResolveCrossPromotionEntry not found");

            var config = new CrossPromotionConfig
            {
                Interstitial = Entry("inter"),
                Rewarded = Entry("rew"),
                RewardedInterstitial = Entry("ri"),
                Banner = Entry("ban"),
            };

            Assert.AreEqual("inter", ((CrossPromotionEntry)mi.Invoke(null, new object[] { config, AdPlaceholderType.Interstitial })).AssetUrl);
            Assert.AreEqual("rew", ((CrossPromotionEntry)mi.Invoke(null, new object[] { config, AdPlaceholderType.Rewarded })).AssetUrl);
            Assert.AreEqual("ri", ((CrossPromotionEntry)mi.Invoke(null, new object[] { config, AdPlaceholderType.RewardedInterstitial })).AssetUrl);
            Assert.AreEqual("ban", ((CrossPromotionEntry)mi.Invoke(null, new object[] { config, AdPlaceholderType.Banner })).AssetUrl);
        }
    }
}
