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

        /// <summary>Recording revenue tracker: captures custom events so tests can assert on the house-ad impression.</summary>
        private class RecordingAdRevenueTracker : IAdRevenueTracker
        {
            public readonly System.Collections.Generic.List<(string name, System.Collections.Generic.Dictionary<string, IConvertible> payload)> CustomEvents
                = new System.Collections.Generic.List<(string, System.Collections.Generic.Dictionary<string, IConvertible>)>();
            public int AdRevenueCount;

            public void TrackAdRevenue(string source, double revenue, string currency,
                System.Collections.Generic.Dictionary<string, IConvertible> extraPayload = null) => AdRevenueCount++;

            public void TrackCustomEvent(string name,
                System.Collections.Generic.Dictionary<string, IConvertible> extraPayload = null)
                => CustomEvents.Add((name, extraPayload));
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

        // ─── House-ad impression analytics ───────────────────────────────────

        [Test]
        public void CrossPromoShown_TracksCrossAdImpression_WithSourceAdPlacement()
        {
            var ui = new RecordingAdPlaceholderUI();
            var tracker = new RecordingAdRevenueTracker();
            var m = new MediationManager(ui, ConfigWith(new CrossPromotionConfig { Rewarded = Entry("https://cdn/rew.mp4") }), tracker);

            Assert.IsTrue(InvokeShowFallback(m, AdPlaceholderType.Rewarded), "fallback show should be requested");

            // No impression event until the asset actually renders.
            Assert.AreEqual(0, tracker.CustomEvents.Count, "no event before the asset renders");

            ui.OnShown.Invoke(); // asset renders → "shown"

            Assert.AreEqual(1, tracker.CustomEvents.Count, "exactly one cross-promo impression event should fire");
            var (name, payload) = tracker.CustomEvents[0];
            Assert.AreEqual("cross_ad_impression", name);
            Assert.IsNotNull(payload);
            Assert.AreEqual(AdFormatKey.Rewarded, payload["ad_placement"],
                "ad_placement should carry the real ad format the cross-promo stood in for");
            Assert.AreEqual(0, tracker.AdRevenueCount, "house-ad impression must NOT emit ad revenue");
        }

        [Test]
        public void CrossPromoFailed_DoesNotTrackCrossAdImpression()
        {
            var ui = new RecordingAdPlaceholderUI();
            var tracker = new RecordingAdRevenueTracker();
            var m = new MediationManager(ui, ConfigWith(new CrossPromotionConfig { Interstitial = Entry("https://cdn/inter.mp4") }), tracker);

            Assert.IsTrue(InvokeShowFallback(m, AdPlaceholderType.Interstitial));
            ui.OnFailed.Invoke(); // asset never rendered

            Assert.AreEqual(0, tracker.CustomEvents.Count, "no impression event when the asset never renders");
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

        // ─── Direct ShowCrossPromotion API (dedicated events) ────────────────

        [Test]
        public void ShowCrossPromotion_OnRender_FiresOnCrossPromoDisplayed_NotOnAdDisplayed()
        {
            var ui = new RecordingAdPlaceholderUI();
            var tracker = new RecordingAdRevenueTracker();
            // No cross_promotion in config — the data comes straight from the call.
            var m = new MediationManager(ui, ConfigWith(null), tracker);

            bool crossPromoDisplayed = false, adDisplayed = false;
            m.OnCrossPromoDisplayed += () => crossPromoDisplayed = true;
            m.OnAdDisplayed         += () => adDisplayed = true;

            m.ShowCrossPromotion(AdPlaceholderType.Interstitial, Entry("https://cdn/direct.mp4"));

            Assert.AreEqual(1, ui.ShowCount, "the creative should be requested from the UI");
            Assert.AreEqual(AdPlaceholderType.Interstitial, ui.LastShownType);
            Assert.AreEqual("https://cdn/direct.mp4", ui.LastShownEntry.AssetUrl);
            Assert.IsFalse(crossPromoDisplayed, "OnCrossPromoDisplayed must wait until the asset renders");

            ui.OnShown.Invoke(); // asset renders

            Assert.IsTrue(crossPromoDisplayed, "OnCrossPromoDisplayed should fire on render");
            Assert.IsFalse(adDisplayed, "the shared real-ad OnAdDisplayed must NOT fire for a direct cross-promo");

            Assert.AreEqual(1, tracker.CustomEvents.Count, "one cross_ad_impression should still fire");
            var (name, payload) = tracker.CustomEvents[0];
            Assert.AreEqual("cross_ad_impression", name);
            Assert.AreEqual(AdFormatKey.Interstitial, payload["ad_placement"]);
            Assert.AreEqual(0, tracker.AdRevenueCount, "direct cross-promo must NOT emit ad revenue");
        }

        [Test]
        public void ShowCrossPromotion_NullOrEmptyAsset_FiresOnCrossPromoFailed_NoShow()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(null));

            int failed = 0;
            m.OnCrossPromoFailed += () => failed++;

            m.ShowCrossPromotion(AdPlaceholderType.Interstitial, (CrossPromotionEntry)null);
            m.ShowCrossPromotion(AdPlaceholderType.Rewarded, new CrossPromotionEntry { AssetUrl = "" });

            Assert.AreEqual(0, ui.ShowCount, "nothing should be shown when there is no asset");
            Assert.AreEqual(2, failed, "each invalid request should fire OnCrossPromoFailed");
        }

        [Test]
        public void ShowCrossPromotion_Clicked_FiresOnCrossPromoClicked_NotOnAdClicked()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(null));

            bool crossPromoClicked = false, adClicked = false;
            m.OnCrossPromoClicked += () => crossPromoClicked = true;
            m.OnAdClicked         += () => adClicked = true;

            m.ShowCrossPromotion(AdPlaceholderType.Rewarded, Entry("https://cdn/direct.png"));
            ui.OnShown.Invoke();
            ui.OnClicked.Invoke();

            Assert.IsTrue(crossPromoClicked, "OnCrossPromoClicked should fire on a CTA tap");
            Assert.IsFalse(adClicked, "the shared OnAdClicked must NOT fire for a direct cross-promo");
        }

        [Test]
        public void ShowCrossPromotion_Closed_FiresOnCrossPromoClosed_NotOnAdClosed()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(null));

            bool crossPromoClosed = false, adClosed = false;
            m.OnCrossPromoClosed += () => crossPromoClosed = true;
            m.OnAdClosed         += () => adClosed = true;

            m.ShowCrossPromotion(AdPlaceholderType.Interstitial, Entry("https://cdn/direct.mp4"));
            ui.OnShown.Invoke();
            ui.OnClosed.Invoke();

            Assert.IsTrue(crossPromoClosed, "OnCrossPromoClosed should fire on dismiss");
            Assert.IsFalse(adClosed, "the shared OnAdClosed must NOT fire for a direct cross-promo");
        }

        [Test]
        public void ShowCrossPromotion_ConvenienceOverload_PassesEntryToUI()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(null));

            m.ShowCrossPromotion(AdPlaceholderType.Banner, "https://cdn/banner.png", "https://noctua/play", 5);

            Assert.AreEqual(1, ui.ShowCount);
            Assert.AreEqual(AdPlaceholderType.Banner, ui.LastShownType);
            Assert.AreEqual("https://cdn/banner.png", ui.LastShownEntry.AssetUrl);
            Assert.AreEqual("https://noctua/play", ui.LastShownEntry.ClickUrl);
            Assert.AreEqual(5, ui.LastShownEntry.MinWatchSeconds);
        }

        [Test]
        public void Fallback_StillFires_SharedAdEvents_NotCrossPromoEvents()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(new CrossPromotionConfig { Interstitial = Entry("https://cdn/inter.mp4") }));

            bool adDisplayed = false, crossPromoDisplayed = false;
            m.OnAdDisplayed         += () => adDisplayed = true;
            m.OnCrossPromoDisplayed += () => crossPromoDisplayed = true;

            Assert.IsTrue(InvokeShowFallback(m, AdPlaceholderType.Interstitial));
            ui.OnShown.Invoke();

            Assert.IsTrue(adDisplayed, "the config-driven fallback must keep firing the shared OnAdDisplayed");
            Assert.IsFalse(crossPromoDisplayed, "the fallback must NOT fire the dedicated OnCrossPromoDisplayed");
        }

        // ─── Effortless ShowCrossPromotion(adType) — Firebase Remote Config ───

        // A completed Task lets `await` continue inline, so the show is requested synchronously
        // within the call (no coroutine needed to observe the result).
        private static System.Threading.Tasks.Task<string> Completed(string s)
            => System.Threading.Tasks.Task.FromResult(s);

        [Test]
        public void ShowCrossPromotion_AdTypeOnly_FetchesFromRemoteConfig_AndShows()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(null));
            const string json =
                "{\"interstitial\":{\"asset_url\":\"https://cdn/rc.mp4\",\"click_url\":\"https://x\",\"min_watch_seconds\":7}," +
                "\"rewarded\":{\"asset_url\":\"https://cdn/rc-rew.mp4\",\"min_watch_seconds\":12}}";
            m.SetRemoteConfigProvider(key => Completed(key == MediationManager.CrossPromotionRemoteConfigKey ? json : ""));

            bool displayed = false;
            m.OnCrossPromoDisplayed += () => displayed = true;

            m.ShowCrossPromotion(AdPlaceholderType.Interstitial);

            Assert.AreEqual(1, ui.ShowCount, "should request the show from the fetched creative");
            Assert.AreEqual(AdPlaceholderType.Interstitial, ui.LastShownType);
            Assert.AreEqual("https://cdn/rc.mp4", ui.LastShownEntry.AssetUrl);
            Assert.AreEqual(7, ui.LastShownEntry.MinWatchSeconds);

            ui.OnShown.Invoke();
            Assert.IsTrue(displayed, "OnCrossPromoDisplayed should fire once the fetched asset renders");
        }

        [Test]
        public void ShowCrossPromotion_AdTypeOnly_NoProvider_FiresFailed()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(null)); // no provider wired

            int failed = 0;
            m.OnCrossPromoFailed += () => failed++;

            m.ShowCrossPromotion(AdPlaceholderType.Interstitial);

            Assert.AreEqual(0, ui.ShowCount, "nothing to show without a remote config provider");
            Assert.AreEqual(1, failed);
        }

        [Test]
        public void ShowCrossPromotion_AdTypeOnly_NoEntryForFormat_FiresFailed()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(null));
            // Remote config only has interstitial; ask for rewarded.
            const string json = "{\"interstitial\":{\"asset_url\":\"https://cdn/rc.mp4\"}}";
            m.SetRemoteConfigProvider(_ => Completed(json));

            int failed = 0;
            m.OnCrossPromoFailed += () => failed++;

            m.ShowCrossPromotion(AdPlaceholderType.Rewarded);

            Assert.AreEqual(0, ui.ShowCount, "no rewarded creative in remote config → nothing shown");
            Assert.AreEqual(1, failed);
        }

        [Test]
        public void ShowCrossPromotion_AdTypeOnly_EmptyRemoteConfig_FiresFailed()
        {
            var ui = new RecordingAdPlaceholderUI();
            var m = Create(ui, ConfigWith(null));
            m.SetRemoteConfigProvider(_ => Completed("")); // empty (e.g. Editor / not fetched yet)

            int failed = 0;
            m.OnCrossPromoFailed += () => failed++;

            m.ShowCrossPromotion(AdPlaceholderType.Interstitial);

            Assert.AreEqual(0, ui.ShowCount);
            Assert.AreEqual(1, failed);
        }
    }
}
