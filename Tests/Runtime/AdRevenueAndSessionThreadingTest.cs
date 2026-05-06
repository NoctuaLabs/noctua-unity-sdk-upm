using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using com.noctuagames.sdk.AdPlaceholder;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Tests.Runtime.IAA;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    /// <summary>
    /// Threading contract tests for <see cref="AdRevenueTrackingManager"/> and
    /// <see cref="SessionTracker"/>, covering background-thread safety and the
    /// main-thread marshalling requirement for PlayerPrefs access.
    ///
    /// Background:
    ///   AppLovin MAX delivers <c>OnAdRevenuePaidEvent</c> on a background thread
    ///   (<c>MaxSdkBase.HandleBackgroundCallback</c>). All three Process* methods in
    ///   <see cref="AdRevenueTrackingManager"/> read and write PlayerPrefs — Unity's
    ///   PlayerPrefs API is documented as main-thread-only and throws
    ///   <see cref="UnityException"/> when accessed from background threads.
    ///   <see cref="MediationManager"/> wraps every revenue callback in
    ///   <c>PostToMainThread()</c> to satisfy this constraint.
    ///
    ///   <see cref="SessionTracker"/> has the same constraint:
    ///   <c>OnApplicationPause</c> accesses PlayerPrefs via <c>SaveSessionState()</c>
    ///   and <c>RecoverOrphanedSession()</c>. It is designed to be called exclusively
    ///   from the Unity main thread (MonoBehaviour lifecycle) and documents that
    ///   contract explicitly.
    ///
    /// Test groups:
    ///
    ///   Group A — AdRevenueTrackingManager per-format background-thread constraint
    ///     A1. ProcessInterstitialThresholds_FromBackgroundThread_Throws
    ///     A2. ProcessRewardedThresholds_FromBackgroundThread_Throws
    ///
    ///   Group B — AdRevenueTrackingManager BG-safe setter methods
    ///     B1. SetAdRevenueTracker_FromBackgroundThread_DoesNotThrow
    ///     B2. SetTaichiConfig_FromBackgroundThread_DoesNotThrow
    ///     B3. DroppedEventCount_ReadFromBackgroundThread_DoesNotThrow
    ///
    ///   Group C — MediationManager.PostToMainThread wrapping revenue callbacks
    ///     C1. PostToMainThread_NullContext_MainThread_RevenueProcessed
    ///     C2. PostToMainThread_NullContext_BackgroundThread_ProcessRevenueThrows
    ///
    ///   Group D — SessionTracker.OnApplicationPause main-thread requirement
    ///     D1. OnApplicationPause_Pause_WithActiveSession_FromBackgroundThread_Throws
    ///     D2. OnApplicationPause_Pause_WithNoSession_FromBackgroundThread_DoesNotThrow
    ///     D3. OnApplicationPause_Resume_WithExpiredSession_FromBackgroundThread_Throws
    ///
    ///   Group E — SessionTracker.RunHeartbeat main-thread switch
    ///     E1. RunHeartbeat_WritesPlayerPrefs_AfterMainThreadSwitch
    ///     E2. RunHeartbeat_ShortPeriod_FiresMultipleHeartbeats
    /// </summary>
    [TestFixture]
    public class AdRevenueAndSessionThreadingTest
    {
        // ── Setup / teardown ─────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            // Taichi PlayerPrefs
            PlayerPrefs.DeleteKey("Noctua_Taichi_TotalRevenue");
            PlayerPrefs.DeleteKey("Noctua_Taichi_TotalAdCount");
            PlayerPrefs.DeleteKey("Noctua_Taichi_TotalImpressions");
            PlayerPrefs.DeleteKey("Noctua_Taichi_InterstitialCount");
            PlayerPrefs.DeleteKey("Noctua_Taichi_RewardedCount");
            PlayerPrefs.DeleteKey("Noctua_Taichi_RewardedRevenue");

            // Session PlayerPrefs
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionId");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionCumulativeMs");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionUnsentMs");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionLastTimestamp");
            PlayerPrefs.Save();

            LogAssert.ignoreFailingMessages = true;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static TaichiConfig DefaultTaichiConfig() => new TaichiConfig
        {
            RevenueThreshold           = 0.01f,
            AdCountThreshold           = 10,
            TotalImpressionThreshold   = 10,
            InterstitialCountThreshold = 5,
            RewardedCountThreshold     = 5,
            RewardedRevenueThreshold   = 0.01f,
        };

        // Minimal IAdPlaceholderUI stub for constructing a MediationManager without ad SDKs.
        private class NoopAdPlaceholderUI : IAdPlaceholderUI
        {
            public void ShowAdPlaceholder(AdPlaceholderType adType) { }
            public void CloseAdPlaceholder() { }
        }

        private static MediationManager NullIaaManager() =>
            new MediationManager(new NoopAdPlaceholderUI(), null);

        // Reflection handles for private members of MediationManager
        private static readonly MethodInfo PostToMainThreadMethod =
            typeof(MediationManager).GetMethod(
                "PostToMainThread",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo PauseStatusField =
            typeof(SessionTracker).GetField(
                "_pauseStatus",
                BindingFlags.NonPublic | BindingFlags.Instance);

        // ════════════════════════════════════════════════════════════════════════
        // Group A — AdRevenueTrackingManager per-format background-thread contract
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A1 — ProcessInterstitialThresholds reads PlayerPrefs (KeyTotalImpressions,
        /// KeyInterstitialCount) and must run on the Unity main thread.
        ///
        /// AppLovin MAX fires <c>OnAdRevenuePaidEvent</c> on a background thread
        /// (<c>MaxSdkBase.HandleBackgroundCallback</c>). <see cref="MediationManager"/>
        /// marshals the call to the main thread via <c>PostToMainThread()</c> before
        /// invoking <c>ProcessInterstitialThresholds</c>. Calling directly from a
        /// background thread skips that marshal and throws <see cref="UnityException"/>.
        ///
        /// Companion test: Group A in <c>AdRevenueTrackingManagerTest.cs</c> covers
        /// <c>ProcessAllFormatsThresholds</c>; this test extends that contract to the
        /// interstitial-specific path.
        /// </summary>
        [Test]
        public void ProcessInterstitialThresholds_FromBackgroundThread_Throws()
        {
            var tracker = new MockAdRevenueTracker();
            var mgr     = new AdRevenueTrackingManager(tracker, DefaultTaichiConfig());

            var ex = Assert.Throws<AggregateException>(() =>
                Task.Run(() => mgr.ProcessInterstitialThresholds(0.01)).Wait());

            Assert.IsInstanceOf<UnityException>(ex.InnerException,
                $"Expected UnityException (PlayerPrefs main-thread-only). Got: {ex.InnerException?.GetType().Name}");
            StringAssert.Contains("main thread", ex.InnerException.Message,
                "Unity's message must state the 'main thread' requirement");
        }

        /// <summary>
        /// A2 — ProcessRewardedThresholds reads PlayerPrefs (KeyTotalImpressions,
        /// KeyRewardedCount, KeyRewardedRevenue) and must run on the Unity main thread.
        ///
        /// Same threading contract as <c>ProcessInterstitialThresholds</c> (A1) — the
        /// rewarded-specific Taichi counters all live in PlayerPrefs.
        /// </summary>
        [Test]
        public void ProcessRewardedThresholds_FromBackgroundThread_Throws()
        {
            var tracker = new MockAdRevenueTracker();
            var mgr     = new AdRevenueTrackingManager(tracker, DefaultTaichiConfig());

            var ex = Assert.Throws<AggregateException>(() =>
                Task.Run(() => mgr.ProcessRewardedThresholds(0.01)).Wait());

            Assert.IsInstanceOf<UnityException>(ex.InnerException,
                $"Expected UnityException (PlayerPrefs main-thread-only). Got: {ex.InnerException?.GetType().Name}");
            StringAssert.Contains("main thread", ex.InnerException.Message,
                "Unity's message must state the 'main thread' requirement");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Group B — AdRevenueTrackingManager BG-safe setter methods
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// B1 — SetAdRevenueTracker() only writes to the private <c>_adRevenueTracker</c>
        /// field. No PlayerPrefs, no Unity APIs → safe to call from any thread.
        ///
        /// Production scenario: a game may wire the tracker from a background initialisation
        /// coroutine, or a late SDK-ready callback may fire on a non-main thread. The wiring
        /// itself must not throw; the subsequent Process* call must still be marshalled.
        /// </summary>
        [Test]
        public void SetAdRevenueTracker_FromBackgroundThread_DoesNotThrow()
        {
            var mgr     = new AdRevenueTrackingManager(null, DefaultTaichiConfig());
            var tracker = new MockAdRevenueTracker();

            Exception caughtEx = null;
            var thread = new Thread(() =>
            {
                try   { mgr.SetAdRevenueTracker(tracker); }
                catch (Exception ex) { caughtEx = ex; }
            }) { IsBackground = true };

            thread.Start();
            thread.Join(3000);

            Assert.IsNull(caughtEx,
                $"SetAdRevenueTracker() must not throw from a background thread. Got: {caughtEx}");
        }

        /// <summary>
        /// B2 — SetTaichiConfig() only writes to the private <c>_taichiConfig</c> field.
        /// No PlayerPrefs, no Unity APIs → safe to call from any thread.
        ///
        /// Production scenario: remote-config fetches often complete on a thread-pool thread
        /// and the callback may arrive before or after the main-thread initialization path.
        /// </summary>
        [Test]
        public void SetTaichiConfig_FromBackgroundThread_DoesNotThrow()
        {
            var tracker = new MockAdRevenueTracker();
            var mgr     = new AdRevenueTrackingManager(tracker, DefaultTaichiConfig());

            Exception caughtEx = null;
            var thread = new Thread(() =>
            {
                try   { mgr.SetTaichiConfig(null); }
                catch (Exception ex) { caughtEx = ex; }
            }) { IsBackground = true };

            thread.Start();
            thread.Join(3000);

            Assert.IsNull(caughtEx,
                $"SetTaichiConfig() must not throw from a background thread. Got: {caughtEx}");
        }

        /// <summary>
        /// B3 — The <see cref="AdRevenueTrackingManager.DroppedEventCount"/> property
        /// returns a plain <c>int</c> field with no Unity API involvement.
        /// Reading it from a background thread must not throw.
        ///
        /// This property is used by monitoring / alerting code that may query it from
        /// background threads (e.g. a periodic health-check coroutine).
        /// </summary>
        [Test]
        public void DroppedEventCount_ReadFromBackgroundThread_DoesNotThrow()
        {
            var mgr = new AdRevenueTrackingManager(null, DefaultTaichiConfig());

            Exception caughtEx = null;
            int readValue = -1;
            var thread = new Thread(() =>
            {
                try   { readValue = mgr.DroppedEventCount; }
                catch (Exception ex) { caughtEx = ex; }
            }) { IsBackground = true };

            thread.Start();
            thread.Join(3000);

            Assert.IsNull(caughtEx,
                $"DroppedEventCount must be readable from a background thread. Got: {caughtEx}");
            Assert.AreEqual(0, readValue,
                "DroppedEventCount must be 0 when no events were dropped");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Group C — MediationManager.PostToMainThread wrapping revenue callbacks
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// C1 — In EditMode, <c>_mainThreadContext</c> is null (because
        /// <c>SynchronizationContext.Current</c> is null at construction time).
        /// <c>PostToMainThread()</c> therefore executes the wrapped action inline on
        /// the caller thread. When the caller is the Unity main thread (as it is in
        /// NUnit EditMode), the revenue action succeeds — PlayerPrefs is accessible.
        ///
        /// This simulates the production path where:
        ///   AppLovin BG callback → PostToMainThread → queued to main thread → ProcessRevenue OK
        ///
        /// In EditMode the queue is replaced by inline execution, but the caller IS the
        /// main thread so the result is equivalent.
        /// </summary>
        [Test]
        public void PostToMainThread_NullContext_MainThread_RevenueProcessed()
        {
            var mgr             = NullIaaManager();
            var revenueTracker  = new MockAdRevenueTracker();
            var revenueManager  = new AdRevenueTrackingManager(revenueTracker, DefaultTaichiConfig());

            Assert.DoesNotThrow(() =>
                PostToMainThreadMethod.Invoke(mgr, new object[]
                {
                    (Action)(() => revenueManager.ProcessAllFormatsThresholds(0.01))
                }),
                "PostToMainThread wrapping ProcessAllFormatsThresholds must succeed on the main thread");

            Assert.IsTrue(revenueTracker.WasFired("Total_Ads_Revenue_001"),
                "Revenue threshold event must fire after PostToMainThread executes inline on main thread");
        }

        /// <summary>
        /// C2 — Documents the broken path: when <c>_mainThreadContext</c> is null AND
        /// <c>PostToMainThread()</c> is invoked from a background thread, the action
        /// executes inline on the background thread. The revenue process then throws
        /// <see cref="UnityException"/> because PlayerPrefs is not thread-safe.
        ///
        /// In production, <c>_mainThreadContext</c> is always set to the main thread's
        /// <see cref="System.Threading.SynchronizationContext"/> at construction time.
        /// If it were somehow null (e.g. destroyed SyncContext), the inline fallback would
        /// expose the underlying BG-thread PlayerPrefs constraint — making revenue
        /// callbacks fail silently where they previously succeeded.
        ///
        /// This test locks in the contract: null context + BG thread + revenue action = throw.
        /// Any future change that makes the Process* methods thread-safe (e.g. a thread-safe
        /// store replacing PlayerPrefs) should update or remove this test accordingly.
        /// </summary>
        [Test]
        public void PostToMainThread_NullContext_BackgroundThread_ProcessRevenueThrows()
        {
            var mgr            = NullIaaManager();
            var revenueTracker = new MockAdRevenueTracker();
            var revenueManager = new AdRevenueTrackingManager(revenueTracker, DefaultTaichiConfig());

            // Task.Run → background thread → PostToMainThread (null context, inline) →
            // ProcessAllFormatsThresholds → PlayerPrefs.GetFloat → UnityException
            var ex = Assert.Throws<AggregateException>(() =>
                Task.Run(() =>
                    PostToMainThreadMethod.Invoke(mgr, new object[]
                    {
                        (Action)(() => revenueManager.ProcessAllFormatsThresholds(0.01))
                    })
                ).Wait());

            // Reflection wraps thrown exceptions in TargetInvocationException.
            // Unwrap to find the actual cause.
            var inner = ex.InnerException;
            Assert.IsNotNull(inner,
                "An exception must be thrown when PostToMainThread executes inline on a background thread");

            var cause = inner is TargetInvocationException tie ? tie.InnerException : inner;
            Assert.IsInstanceOf<UnityException>(cause,
                $"Root cause must be UnityException (PlayerPrefs BG violation). Got: {cause?.GetType().Name}: {cause?.Message}");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Group D — SessionTracker.OnApplicationPause main-thread requirement
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// D1 — <c>OnApplicationPause(true)</c> with an active session calls
        /// <c>SaveSessionState()</c>, which writes to PlayerPrefs. Calling from a
        /// background thread therefore throws <see cref="UnityException"/>.
        ///
        /// In production, Unity's MonoBehaviour lifecycle always delivers
        /// <c>OnApplicationPause</c> on the main thread. Ad SDK callbacks that attempt
        /// to indirectly trigger a pause transition from their own threads would
        /// expose this constraint.
        /// </summary>
        [Test]
        public void OnApplicationPause_Pause_WithActiveSession_FromBackgroundThread_Throws()
        {
            var mock    = new MockEventSender();
            var tracker = new SessionTracker(new SessionTrackerConfig(), mock, null);

            // Start a session on the main thread → _sessionId != null
            tracker.OnApplicationPause(false);

            // Pause from background thread:
            //   _foregroundStopwatch.Stop() + send events → safe
            //   _nextSessionTimeout = ... → safe
            //   SaveSessionState() → PlayerPrefs.SetString → throws
            var ex = Assert.Throws<AggregateException>(() =>
                Task.Run(() => tracker.OnApplicationPause(true)).Wait());

            Assert.IsInstanceOf<UnityException>(ex.InnerException,
                $"Expected UnityException from SaveSessionState PlayerPrefs write. " +
                $"Got: {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}");
            StringAssert.Contains("main thread", ex.InnerException.Message);

            tracker.Dispose();
        }

        /// <summary>
        /// D2 — <c>OnApplicationPause(true)</c> WITHOUT an active session (<c>_sessionId == null</c>)
        /// calls <c>SaveSessionState()</c>, which has an early-return guard:
        ///   <c>if (_sessionId == null) return;</c>
        ///
        /// No PlayerPrefs access occurs → the call is safe from a background thread.
        ///
        /// Production scenario: an ad SDK may fire pause callbacks before the game
        /// session has started (e.g. during mediation SDK initialisation). This must
        /// not crash the app.
        ///
        /// Implementation note: <c>_pauseStatus</c> is initialised to <c>true</c> in
        /// the constructor. The test uses reflection to reset it to <c>false</c> so the
        /// BG call is not filtered out by the duplicate-status guard. This isolates the
        /// SaveSessionState early-return path cleanly.
        /// </summary>
        [Test]
        public void OnApplicationPause_Pause_WithNoSession_FromBackgroundThread_DoesNotThrow()
        {
            var mock    = new MockEventSender();
            var tracker = new SessionTracker(new SessionTrackerConfig(), mock, null);

            // _pauseStatus starts as true. Reset to false via reflection so the BG
            // pause call proceeds past the duplicate-status guard, reaching SaveSessionState.
            // _sessionId remains null (no session was ever started).
            PauseStatusField?.SetValue(tracker, false);

            Exception caughtEx = null;
            var thread = new Thread(() =>
            {
                try   { tracker.OnApplicationPause(true); }
                catch (Exception ex) { caughtEx = ex; }
            }) { IsBackground = true };

            thread.Start();
            thread.Join(3000);

            Assert.IsNull(caughtEx,
                $"OnApplicationPause(true) with no active session must not throw from BG " +
                $"(SaveSessionState early return guards against PlayerPrefs access). Got: {caughtEx}");

            tracker.Dispose();
        }

        /// <summary>
        /// D3 — <c>OnApplicationPause(false)</c> after a session timeout clears the
        /// expired session via <c>ClearSessionState()</c>, which calls
        /// <c>PlayerPrefs.DeleteKey</c>. Calling from a background thread throws.
        ///
        /// Reproduces the full lifecycle: start → pause → wait for timeout → resume from BG.
        /// The timeout fires in the resume path, triggering <c>ClearSessionState()</c>.
        ///
        /// Uses <c>SessionTimeoutMs = 50</c> so the test completes in under a second.
        /// </summary>
        [UnityTest]
        public IEnumerator OnApplicationPause_Resume_WithExpiredSession_FromBackgroundThread_Throws() =>
            UniTask.ToCoroutine(async () =>
            {
                var mock    = new MockEventSender();
                var config  = new SessionTrackerConfig { SessionTimeoutMs = 50 }; // 50 ms
                var tracker = new SessionTracker(config, mock, null);

                // Start + pause on main thread; _nextSessionTimeout = now + 50ms
                tracker.OnApplicationPause(false);
                tracker.OnApplicationPause(true);

                // Wait for the 50ms session timeout to expire
                await UniTask.Delay(200);

                // Resume from background: timeout elapsed → ClearSessionState()
                // → PlayerPrefs.DeleteKey → throws UnityException
                Exception caughtEx = null;
                await Task.Run(() =>
                {
                    try   { tracker.OnApplicationPause(false); }
                    catch (Exception ex) { caughtEx = ex; }
                });

                Assert.IsNotNull(caughtEx,
                    "UnityException must be thrown when ClearSessionState() runs on a background thread");
                Assert.IsInstanceOf<UnityException>(caughtEx,
                    $"Expected UnityException from PlayerPrefs.DeleteKey on BG thread. " +
                    $"Got: {caughtEx?.GetType().Name}: {caughtEx?.Message}");
                StringAssert.Contains("main thread", caughtEx.Message);

                tracker.Dispose();
            });

        // ════════════════════════════════════════════════════════════════════════
        // Group E — SessionTracker.RunHeartbeat main-thread switch
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// E1 — <c>RunHeartbeat</c> uses <c>await UniTask.SwitchToMainThread()</c>
        /// before calling <c>SaveSessionState()</c>. This test verifies the PlayerPrefs
        /// key is written after a heartbeat fires, proving the main-thread switch ran.
        ///
        /// If <c>SwitchToMainThread()</c> were removed, the heartbeat would call
        /// <c>PlayerPrefs.SetString</c> from the background task thread and throw
        /// <see cref="UnityException"/>, causing the heartbeat loop to stall.
        ///
        /// Uses <c>HeartbeatPeriodMs = 200</c> so the key appears within one second.
        /// </summary>
        [UnityTest]
        public IEnumerator RunHeartbeat_WritesPlayerPrefs_AfterMainThreadSwitch() =>
            UniTask.ToCoroutine(async () =>
            {
                var mock    = new MockEventSender();
                var config  = new SessionTrackerConfig { HeartbeatPeriodMs = 200 };
                var tracker = new SessionTracker(config, mock, null);

                tracker.OnApplicationPause(false); // starts session + RunHeartbeat loop
                await UniTask.Delay(50);

                // Wait for at least one heartbeat period plus scheduling margin
                await UniTask.Delay(800);

                // After heartbeat: SwitchToMainThread → SaveSessionState
                // → PlayerPrefs.SetString("NoctuaOrphanedSessionCumulativeMs", ...)
                var stored = PlayerPrefs.GetString("NoctuaOrphanedSessionCumulativeMs", "");
                Assert.IsFalse(string.IsNullOrEmpty(stored),
                    "NoctuaOrphanedSessionCumulativeMs must be written by RunHeartbeat. " +
                    "If empty, UniTask.SwitchToMainThread() did not execute before SaveSessionState().");

                Assert.IsTrue(long.TryParse(stored, out _),
                    $"Stored PlayerPrefs value '{stored}' must be a parseable long (cumulative engagement ms)");

                tracker.Dispose();
            });

        /// <summary>
        /// E2 — With a short heartbeat period, multiple <c>session_heartbeat</c> events
        /// must fire within the expected window. This indirectly confirms that the
        /// <c>RunHeartbeat</c> loop continues executing correctly after each
        /// <c>await UniTask.SwitchToMainThread()</c> — i.e., the switch does not stall
        /// or cancel the loop.
        ///
        /// If <c>SwitchToMainThread()</c> were broken (e.g. incorrect cancellation token
        /// propagation), the loop would stop after the first heartbeat.
        /// </summary>
        [UnityTest]
        public IEnumerator RunHeartbeat_ShortPeriod_FiresMultipleHeartbeats() =>
            UniTask.ToCoroutine(async () =>
            {
                var mock    = new MockEventSender();
                var config  = new SessionTrackerConfig { HeartbeatPeriodMs = 200 };
                var tracker = new SessionTracker(config, mock, null);

                tracker.OnApplicationPause(false);

                // 200ms period × 3 expected heartbeats + scheduling margin
                await UniTask.Delay(900);

                var heartbeatCount = 0;
                foreach (var ev in mock.SentEvents)
                {
                    if (ev.Name == "session_heartbeat") heartbeatCount++;
                }

                Assert.GreaterOrEqual(heartbeatCount, 2,
                    $"At least 2 session_heartbeat events must fire in 900ms with 200ms period. " +
                    $"Got {heartbeatCount}. This indicates RunHeartbeat stalled after " +
                    $"UniTask.SwitchToMainThread().");

                tracker.Dispose();
            });
    }
}
