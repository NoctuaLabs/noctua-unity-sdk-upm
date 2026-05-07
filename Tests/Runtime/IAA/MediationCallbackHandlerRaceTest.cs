using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Tests for <see cref="HybridAdOrchestrator"/> callback handler behaviour and race conditions.
    ///
    /// Coverage:
    ///   Group I  — Initialization callbacks (onPrimaryReady / onSecondaryReady ordering).
    ///   Group H  — Per-event handler forwarding: each of the 7 events from primary and secondary
    ///              individually, plus <see cref="HybridAdOrchestrator.IsAdShowing"/> state transitions.
    ///   Group R  — Race conditions: both networks fire the same event simultaneously via
    ///              background <see cref="Task"/>s.  Subscriber count must reach the expected value
    ///              without deadlock, and <c>IsAdShowing</c> must land in the correct terminal state.
    ///   Group M  — Multiple-subscriber semantics: two listeners both receive every event from
    ///              both networks under sequential and concurrent triggers.
    ///
    /// Threading note
    /// ───────────────
    /// <see cref="HybridAdOrchestrator._isAdShowing"/> is a plain <c>bool</c> field (no lock /
    /// volatile).  Concurrent writes from two threads are a benign data race on modern x86/x64
    /// (aligned bool reads/writes are natively atomic), but the RESULT is intentionally
    /// non-deterministic when both threads write different values.  Race tests that touch
    /// <c>IsAdShowing</c> only assert the final value when both networks agree (e.g. both fire
    /// AdDisplayed → both write <c>true</c> → outcome is always <c>true</c>).
    ///
    /// Multicast delegate note
    /// ───────────────────────
    /// C# multicast delegates are immutable.  <c>_onAdDisplayed?.Invoke()</c> captures the
    /// current delegate reference in a local, then calls it.  Two threads calling this
    /// simultaneously are safe (no crash) — they both invoke the same subscriber chain.
    /// </summary>
    [TestFixture]
    public class MediationCallbackHandlerRaceTest
    {
        private MockAdNetwork _primary;
        private MockAdNetwork _secondary;

        // Generous timeout for all concurrent Task.WhenAll waits (milliseconds)
        private const int RaceTimeoutMs = 5_000;

        [SetUp]
        public void SetUp()
        {
            _primary   = new MockAdNetwork { NetworkName = "primary" };
            _secondary = new MockAdNetwork { NetworkName = "secondary" };

            // Clear AdNetworkPerformanceTracker PlayerPrefs to avoid cross-test pollution.
            // Use the actual NetworkName fields so the keys match if names are ever changed.
            foreach (var net in new[] { _primary.NetworkName, _secondary.NetworkName })
            foreach (var fmt in new[] { AdFormatKey.Interstitial, AdFormatKey.Rewarded,
                                        AdFormatKey.Banner, AdFormatKey.AppOpen })
            {
                PlayerPrefs.DeleteKey($"NoctuaAdPerf_fill_{net}_{fmt}");
                PlayerPrefs.DeleteKey($"NoctuaAdPerf_rev_{net}_{fmt}");
            }
            PlayerPrefs.Save();
        }

        // ─── Group I — Initialization callbacks ───────────────────────────────

        /// <summary>
        /// I1: onPrimaryReady fires immediately after primary network calls its init callback.
        /// </summary>
        [Test]
        public void I1_Initialize_PrimaryOnly_OnPrimaryReadyFires()
        {
            var orc = new HybridAdOrchestrator(_primary);

            bool primaryReadyFired = false;
            orc.Initialize(onPrimaryReady: () => primaryReadyFired = true);

            Assert.IsTrue(primaryReadyFired, "onPrimaryReady must fire once primary init callback completes");
            Assert.AreEqual(1, _primary.InitializeCallCount, "Primary network must be initialized exactly once");
        }

        /// <summary>
        /// I2: onSecondaryReady fires AFTER onPrimaryReady when both networks are present.
        ///     Initialize() is sequential: secondary init starts only after primary's callback.
        /// </summary>
        [Test]
        public void I2_Initialize_BothNetworks_SequentialOrder_PrimaryBeforeSecondary()
        {
            var orc   = new HybridAdOrchestrator(_primary, _secondary);
            var order = new List<string>();

            orc.Initialize(
                onPrimaryReady:   () => order.Add("primary"),
                onSecondaryReady: () => order.Add("secondary"));

            Assert.AreEqual(new[] { "primary", "secondary" }, order,
                "onPrimaryReady must fire before onSecondaryReady");
            Assert.AreEqual(1, _primary.InitializeCallCount,   "Primary init call count must be 1");
            Assert.AreEqual(1, _secondary.InitializeCallCount, "Secondary init call count must be 1");
        }

        /// <summary>
        /// I3: With primary-only orchestrator, onSecondaryReady is never invoked.
        ///     The field-level <c>_secondary</c> mock is never passed to the orchestrator, so
        ///     testing its <c>InitializeCallCount</c> would be tautological (the orchestrator has
        ///     no reference to it).  The meaningful invariant is that <c>onSecondaryReady</c> does
        ///     not fire — that is what this test verifies.
        /// </summary>
        [Test]
        public void I3_Initialize_PrimaryOnly_SecondaryReadyNeverFires()
        {
            var orc = new HybridAdOrchestrator(_primary); // no secondary

            bool secondaryReadyFired = false;
            orc.Initialize(
                onPrimaryReady:   () => { },
                onSecondaryReady: () => secondaryReadyFired = true);

            Assert.IsFalse(secondaryReadyFired, "onSecondaryReady must NOT fire when secondary network is absent");
        }

        /// <summary>
        /// I4: With a single Initialize() call, each callback fires exactly once.
        ///     (Note: <see cref="HybridAdOrchestrator.Initialize"/> has no idempotency guard —
        ///     calling it twice would double-init both networks.  This test only validates the
        ///     single-call contract.)
        /// </summary>
        [Test]
        public void I4_Initialize_SingleCall_EachCallbackFiresExactlyOnce()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            int primaryCount   = 0;
            int secondaryCount = 0;

            orc.Initialize(
                onPrimaryReady:   () => primaryCount++,
                onSecondaryReady: () => secondaryCount++);

            Assert.AreEqual(1, primaryCount,   "onPrimaryReady must fire exactly once");
            Assert.AreEqual(1, secondaryCount, "onSecondaryReady must fire exactly once");
        }

        /// <summary>
        /// I5: Initialize() with null callbacks does not throw — both callback params are optional
        ///     and the implementation guards with <c>?.Invoke()</c>.
        /// </summary>
        [Test]
        public void I5_Initialize_NullCallbacks_DoesNotThrow()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            Assert.DoesNotThrow(
                () => orc.Initialize(onPrimaryReady: null, onSecondaryReady: null),
                "Initialize must not throw when callbacks are null");

            // Both networks still initialize (the null guards only protect the callbacks, not init)
            Assert.AreEqual(1, _primary.InitializeCallCount,   "Primary must initialize even with null callback");
            Assert.AreEqual(1, _secondary.InitializeCallCount, "Secondary must initialize even with null callback");
        }

        // ─── Group H — Per-event handler forwarding ───────────────────────────

        /// <summary>
        /// H1: OnAdDisplayed from primary raises the orchestrator event and sets IsAdShowing.
        /// </summary>
        [Test]
        public void H1_OnAdDisplayed_Primary_FiresEventAndSetsIsAdShowing()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int fired = 0;
            orc.OnAdDisplayed += () => fired++;

            _primary.TriggerAdDisplayed();

            Assert.AreEqual(1, fired, "OnAdDisplayed subscriber must be called once");
            Assert.IsTrue(orc.IsAdShowing, "IsAdShowing must be true after OnAdDisplayed");
        }

        /// <summary>
        /// H2: OnAdDisplayed from secondary raises the orchestrator event and sets IsAdShowing.
        /// </summary>
        [Test]
        public void H2_OnAdDisplayed_Secondary_FiresEventAndSetsIsAdShowing()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int fired = 0;
            orc.OnAdDisplayed += () => fired++;

            _secondary.TriggerAdDisplayed();

            Assert.AreEqual(1, fired, "OnAdDisplayed subscriber must be called once from secondary");
            Assert.IsTrue(orc.IsAdShowing, "IsAdShowing must be true after secondary OnAdDisplayed");
        }

        /// <summary>
        /// H3: OnAdFailedDisplayed from primary raises event and resets IsAdShowing to false.
        /// </summary>
        [Test]
        public void H3_OnAdFailedDisplayed_Primary_FiresEventAndClearsIsAdShowing()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            // Put IsAdShowing into true first
            _primary.TriggerAdDisplayed();
            Assert.IsTrue(orc.IsAdShowing);

            int fired = 0;
            orc.OnAdFailedDisplayed += () => fired++;

            _primary.TriggerAdFailedDisplayed();

            Assert.AreEqual(1, fired, "OnAdFailedDisplayed subscriber must fire once");
            Assert.IsFalse(orc.IsAdShowing, "IsAdShowing must be false after OnAdFailedDisplayed");
        }

        /// <summary>
        /// H4: OnAdFailedDisplayed from secondary raises event and resets IsAdShowing.
        /// </summary>
        [Test]
        public void H4_OnAdFailedDisplayed_Secondary_FiresEventAndClearsIsAdShowing()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            _secondary.TriggerAdDisplayed();
            Assert.IsTrue(orc.IsAdShowing);

            int fired = 0;
            orc.OnAdFailedDisplayed += () => fired++;

            _secondary.TriggerAdFailedDisplayed();

            Assert.AreEqual(1, fired, "OnAdFailedDisplayed from secondary must fire once");
            Assert.IsFalse(orc.IsAdShowing, "IsAdShowing must be false after secondary OnAdFailedDisplayed");
        }

        /// <summary>
        /// H5: OnAdClosed from primary raises event and resets IsAdShowing.
        /// </summary>
        [Test]
        public void H5_OnAdClosed_Primary_FiresEventAndClearsIsAdShowing()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            _primary.TriggerAdDisplayed();

            int fired = 0;
            orc.OnAdClosed += () => fired++;

            _primary.TriggerAdClosed();

            Assert.AreEqual(1, fired, "OnAdClosed subscriber must fire once from primary");
            Assert.IsFalse(orc.IsAdShowing, "IsAdShowing must be false after primary OnAdClosed");
        }

        /// <summary>
        /// H6: OnAdClosed from secondary raises event and resets IsAdShowing.
        /// </summary>
        [Test]
        public void H6_OnAdClosed_Secondary_FiresEventAndClearsIsAdShowing()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            _secondary.TriggerAdDisplayed();

            int fired = 0;
            orc.OnAdClosed += () => fired++;

            _secondary.TriggerAdClosed();

            Assert.AreEqual(1, fired, "OnAdClosed subscriber must fire once from secondary");
            Assert.IsFalse(orc.IsAdShowing, "IsAdShowing must be false after secondary OnAdClosed");
        }

        /// <summary>
        /// H7: OnAdClicked from primary and secondary each forward to subscribers independently.
        /// </summary>
        [Test]
        public void H7_OnAdClicked_BothNetworks_EachFireIndependently()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int clicks = 0;
            orc.OnAdClicked += () => clicks++;

            _primary.TriggerAdClicked();
            Assert.AreEqual(1, clicks, "Primary OnAdClicked must produce 1 subscriber call");

            _secondary.TriggerAdClicked();
            Assert.AreEqual(2, clicks, "Secondary OnAdClicked must produce 2nd subscriber call");
        }

        /// <summary>
        /// H8: OnAdImpressionRecorded from primary and secondary each forward independently.
        /// </summary>
        [Test]
        public void H8_OnAdImpressionRecorded_BothNetworks_EachFireIndependently()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int impressions = 0;
            orc.OnAdImpressionRecorded += () => impressions++;

            _primary.TriggerAdImpressionRecorded();
            Assert.AreEqual(1, impressions, "Primary OnAdImpressionRecorded must produce 1 call");

            _secondary.TriggerAdImpressionRecorded();
            Assert.AreEqual(2, impressions, "Secondary OnAdImpressionRecorded must produce 2nd call");
        }

        /// <summary>
        /// H9: OnUserEarnedReward forwards amount and type correctly from both networks.
        /// </summary>
        [Test]
        public void H9_OnUserEarnedReward_BothNetworks_AmountAndTypeForwarded()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            var rewards = new List<(double Amount, string Type)>();
            orc.OnUserEarnedReward += (amt, type) => rewards.Add((amt, type));

            _primary.TriggerUserEarnedReward(10.0, "coins");
            _secondary.TriggerUserEarnedReward(50.0, "gems");

            Assert.AreEqual(2, rewards.Count);
            Assert.AreEqual((10.0, "coins"), rewards[0], "Primary reward must be forwarded verbatim");
            Assert.AreEqual((50.0, "gems"),  rewards[1], "Secondary reward must be forwarded verbatim");
        }

        /// <summary>
        /// H10: OnAdRevenuePaid forwards revenue, currency, and metadata correctly from both networks.
        /// </summary>
        [Test]
        public void H10_OnAdRevenuePaid_BothNetworks_PayloadForwardedVerbatim()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            var payments = new List<(double Revenue, string Currency, Dictionary<string, string> Meta)>();
            orc.OnAdRevenuePaid += (rev, cur, meta) => payments.Add((rev, cur, meta));

            var primaryMeta   = new Dictionary<string, string> { ["format"] = "interstitial" };
            var secondaryMeta = new Dictionary<string, string> { ["format"] = "rewarded" };

            _primary.TriggerAdRevenuePaid(0.05, "USD", primaryMeta);
            _secondary.TriggerAdRevenuePaid(0.12, "EUR", secondaryMeta);

            Assert.AreEqual(2, payments.Count);
            Assert.AreEqual(0.05, payments[0].Revenue);
            Assert.AreEqual("USD", payments[0].Currency);
            Assert.AreSame(primaryMeta, payments[0].Meta, "Metadata dict must be the same reference (not copied)");
            Assert.AreEqual(0.12, payments[1].Revenue);
            Assert.AreEqual("EUR", payments[1].Currency);
            Assert.AreSame(secondaryMeta, payments[1].Meta);
        }

        /// <summary>
        /// H11: IsAdShowing state machine — full cycle: displayed → closed → not showing.
        ///      Verifies every transition in the correct order.
        /// </summary>
        [Test]
        public void H11_IsAdShowing_FullCycle_DisplayedThenClosed()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            Assert.IsFalse(orc.IsAdShowing, "initial state: not showing");

            _primary.TriggerAdDisplayed();
            Assert.IsTrue(orc.IsAdShowing, "after OnAdDisplayed: showing");

            _primary.TriggerAdClosed();
            Assert.IsFalse(orc.IsAdShowing, "after OnAdClosed: not showing");
        }

        /// <summary>
        /// H12: IsAdShowing reset via OnAdFailedDisplayed — displayed → failed → not showing.
        /// </summary>
        [Test]
        public void H12_IsAdShowing_FailedDisplayed_ResetsState()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            _secondary.TriggerAdDisplayed();
            Assert.IsTrue(orc.IsAdShowing);

            _secondary.TriggerAdFailedDisplayed();
            Assert.IsFalse(orc.IsAdShowing, "OnAdFailedDisplayed must reset IsAdShowing");
        }

        // ─── Group R — Race conditions ────────────────────────────────────────

        /// <summary>
        /// R1: Both networks fire OnAdDisplayed simultaneously from background threads.
        ///     The subscriber must be called exactly twice (once per network), and there must be
        ///     no deadlock.  IsAdShowing must be true (both writers set true).
        /// </summary>
        [Test]
        public void R1_Race_BothNetworks_OnAdDisplayed_Simultaneously_CountIsTwo_NoDeadlock()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int count = 0;
            orc.OnAdDisplayed += () => Interlocked.Increment(ref count);

            var t1 = Task.Run(() => _primary.TriggerAdDisplayed());
            var t2 = Task.Run(() => _secondary.TriggerAdDisplayed());

            bool completed = WaitAll(t1, t2);

            Assert.IsTrue(completed, "Both background tasks must complete within the timeout (no deadlock)");
            Assert.AreEqual(2, count, "OnAdDisplayed subscriber must be called once per network (total 2)");
            // Both threads write `true` — final value is unambiguously true
            Assert.IsTrue(orc.IsAdShowing, "IsAdShowing must be true when both networks report ad displayed");
        }

        /// <summary>
        /// R2: Both networks fire OnAdClosed simultaneously.
        ///     Both write IsAdShowing = false; final state must be false.
        /// </summary>
        [Test]
        public void R2_Race_BothNetworks_OnAdClosed_Simultaneously_CountIsTwo_IsAdShowingFalse()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int count = 0;
            orc.OnAdClosed += () => Interlocked.Increment(ref count);

            // Put into showing state first (sequential, no race)
            _primary.TriggerAdDisplayed();
            Assert.IsTrue(orc.IsAdShowing);

            var t1 = Task.Run(() => _primary.TriggerAdClosed());
            var t2 = Task.Run(() => _secondary.TriggerAdClosed());

            bool completed = WaitAll(t1, t2);

            Assert.IsTrue(completed, "No deadlock expected");
            Assert.AreEqual(2, count, "OnAdClosed must fire twice (once per network)");
            // Both threads write false — outcome is unambiguously false
            Assert.IsFalse(orc.IsAdShowing, "IsAdShowing must be false after both networks close");
        }

        /// <summary>
        /// R3: Both networks fire OnAdFailedDisplayed simultaneously.
        ///     Both write IsAdShowing = false; final state must be false.
        /// </summary>
        [Test]
        public void R3_Race_BothNetworks_OnAdFailedDisplayed_Simultaneously_CountIsTwo()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int count = 0;
            orc.OnAdFailedDisplayed += () => Interlocked.Increment(ref count);

            _primary.TriggerAdDisplayed();

            var t1 = Task.Run(() => _primary.TriggerAdFailedDisplayed());
            var t2 = Task.Run(() => _secondary.TriggerAdFailedDisplayed());

            bool completed = WaitAll(t1, t2);

            Assert.IsTrue(completed, "No deadlock expected");
            Assert.AreEqual(2, count, "OnAdFailedDisplayed must fire twice");
            Assert.IsFalse(orc.IsAdShowing, "IsAdShowing must be false after both networks report fail");
        }

        /// <summary>
        /// R4: Both networks fire OnAdClicked simultaneously.
        ///     Event does not mutate IsAdShowing; subscriber count must reach 2.
        /// </summary>
        [Test]
        public void R4_Race_BothNetworks_OnAdClicked_Simultaneously_CountIsTwo()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int count = 0;
            orc.OnAdClicked += () => Interlocked.Increment(ref count);

            var t1 = Task.Run(() => _primary.TriggerAdClicked());
            var t2 = Task.Run(() => _secondary.TriggerAdClicked());

            bool completed = WaitAll(t1, t2);

            Assert.IsTrue(completed, "No deadlock expected");
            Assert.AreEqual(2, count, "OnAdClicked must fire exactly twice (once per network)");
        }

        /// <summary>
        /// R5: Both networks fire OnAdImpressionRecorded simultaneously.
        ///     Subscriber count must reach 2.
        /// </summary>
        [Test]
        public void R5_Race_BothNetworks_OnAdImpressionRecorded_Simultaneously_CountIsTwo()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int count = 0;
            orc.OnAdImpressionRecorded += () => Interlocked.Increment(ref count);

            var t1 = Task.Run(() => _primary.TriggerAdImpressionRecorded());
            var t2 = Task.Run(() => _secondary.TriggerAdImpressionRecorded());

            bool completed = WaitAll(t1, t2);

            Assert.IsTrue(completed, "No deadlock expected");
            Assert.AreEqual(2, count, "OnAdImpressionRecorded must fire exactly twice");
        }

        /// <summary>
        /// R6: Both networks fire OnUserEarnedReward simultaneously.
        ///     Subscriber count must reach 2 (one per network reward).
        /// </summary>
        [Test]
        public void R6_Race_BothNetworks_OnUserEarnedReward_Simultaneously_CountIsTwo()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int count = 0;
            orc.OnUserEarnedReward += (_, _amount) => Interlocked.Increment(ref count);

            var t1 = Task.Run(() => _primary.TriggerUserEarnedReward(10, "coins"));
            var t2 = Task.Run(() => _secondary.TriggerUserEarnedReward(20, "gems"));

            bool completed = WaitAll(t1, t2);

            Assert.IsTrue(completed, "No deadlock expected");
            Assert.AreEqual(2, count, "OnUserEarnedReward must fire exactly twice");
        }

        /// <summary>
        /// R7: Both networks fire OnAdRevenuePaid simultaneously.
        ///     Subscriber count must reach 2.
        /// </summary>
        [Test]
        public void R7_Race_BothNetworks_OnAdRevenuePaid_Simultaneously_CountIsTwo()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int count = 0;
            orc.OnAdRevenuePaid += (_, _cur, _meta) => Interlocked.Increment(ref count);

            var t1 = Task.Run(() => _primary.TriggerAdRevenuePaid(0.01, "USD", null));
            var t2 = Task.Run(() => _secondary.TriggerAdRevenuePaid(0.02, "USD", null));

            bool completed = WaitAll(t1, t2);

            Assert.IsTrue(completed, "No deadlock expected");
            Assert.AreEqual(2, count, "OnAdRevenuePaid must fire exactly twice");
        }

        /// <summary>
        /// R8: All 7 event types fired from both networks concurrently in a single wave.
        ///     No deadlock; subscriber counts per event-type must each be exactly 2.
        /// </summary>
        [Test]
        public void R8_Race_AllSevenEvents_BothNetworks_Simultaneously_AllCountsAreTwo()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            int displayed   = 0, failed    = 0, clicked = 0;
            int impression  = 0, closed    = 0, reward  = 0, revenue = 0;

            orc.OnAdDisplayed          += () =>           Interlocked.Increment(ref displayed);
            orc.OnAdFailedDisplayed    += () =>           Interlocked.Increment(ref failed);
            orc.OnAdClicked            += () =>           Interlocked.Increment(ref clicked);
            orc.OnAdImpressionRecorded += () =>           Interlocked.Increment(ref impression);
            orc.OnAdClosed             += () =>           Interlocked.Increment(ref closed);
            orc.OnUserEarnedReward     += (_, _t) =>       Interlocked.Increment(ref reward);
            orc.OnAdRevenuePaid        += (_, _c, _m) =>  Interlocked.Increment(ref revenue);

            // Synthetic stress test: firing OnAdDisplayed and OnAdFailedDisplayed from the same
            // network simultaneously is operationally impossible in production (they are mutually
            // exclusive per ad slot), but tests thread safety of the delegate chain and
            // IsAdShowing under maximum concurrent load.  IsAdShowing is NOT asserted in R8
            // because the outcome is non-deterministic (last-write-wins between conflicting writers).
            // Fire all 7 events from BOTH networks at the same time
            var tasks = new[]
            {
                Task.Run(() => _primary.TriggerAdDisplayed()),
                Task.Run(() => _secondary.TriggerAdDisplayed()),

                Task.Run(() => _primary.TriggerAdFailedDisplayed()),
                Task.Run(() => _secondary.TriggerAdFailedDisplayed()),

                Task.Run(() => _primary.TriggerAdClicked()),
                Task.Run(() => _secondary.TriggerAdClicked()),

                Task.Run(() => _primary.TriggerAdImpressionRecorded()),
                Task.Run(() => _secondary.TriggerAdImpressionRecorded()),

                Task.Run(() => _primary.TriggerAdClosed()),
                Task.Run(() => _secondary.TriggerAdClosed()),

                Task.Run(() => _primary.TriggerUserEarnedReward(1, "x")),
                Task.Run(() => _secondary.TriggerUserEarnedReward(2, "y")),

                Task.Run(() => _primary.TriggerAdRevenuePaid(0.01, "USD", null)),
                Task.Run(() => _secondary.TriggerAdRevenuePaid(0.02, "USD", null)),
            };

            bool completed = WaitAll(tasks);

            Assert.IsTrue(completed, "All 14 background tasks must complete without deadlock");
            Assert.AreEqual(2, displayed,  "OnAdDisplayed count");
            Assert.AreEqual(2, failed,     "OnAdFailedDisplayed count");
            Assert.AreEqual(2, clicked,    "OnAdClicked count");
            Assert.AreEqual(2, impression, "OnAdImpressionRecorded count");
            Assert.AreEqual(2, closed,     "OnAdClosed count");
            Assert.AreEqual(2, reward,     "OnUserEarnedReward count");
            Assert.AreEqual(2, revenue,    "OnAdRevenuePaid count");
        }

        /// <summary>
        /// R9: Primary fires OnAdDisplayed while secondary fires OnAdClosed simultaneously.
        ///     The two writers disagree (primary writes true, secondary writes false).
        ///     The test only verifies no deadlock and that both events fire — it does NOT assert
        ///     <c>IsAdShowing</c> because the outcome is inherently non-deterministic (last-write-wins).
        /// </summary>
        [Test]
        public void R9_Race_ConflictingIsAdShowing_DisplayedVsClosed_NoDeadlock_BothEventsFire()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int displayCount = 0;
            int closedCount  = 0;
            orc.OnAdDisplayed += () => Interlocked.Increment(ref displayCount);
            orc.OnAdClosed    += () => Interlocked.Increment(ref closedCount);

            var t1 = Task.Run(() => _primary.TriggerAdDisplayed());
            var t2 = Task.Run(() => _secondary.TriggerAdClosed());

            bool completed = WaitAll(t1, t2);

            Assert.IsTrue(completed, "No deadlock expected");
            Assert.AreEqual(1, displayCount, "OnAdDisplayed must fire exactly once");
            Assert.AreEqual(1, closedCount,  "OnAdClosed must fire exactly once");
            // IsAdShowing is intentionally not asserted — see class XML doc for rationale.
        }

        // ─── Group M — Multiple subscribers ───────────────────────────────────

        /// <summary>
        /// M1: Two subscribers on OnAdDisplayed both receive all events from both networks.
        ///     Total per subscriber = 2; no event is skipped for any subscriber.
        /// </summary>
        [Test]
        public void M1_MultipleSubscribers_OnAdDisplayed_BothReceiveAllEvents()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            int subscriber1Count = 0;
            int subscriber2Count = 0;
            orc.OnAdDisplayed += () => Interlocked.Increment(ref subscriber1Count);
            orc.OnAdDisplayed += () => Interlocked.Increment(ref subscriber2Count);

            _primary.TriggerAdDisplayed();
            _secondary.TriggerAdDisplayed();

            Assert.AreEqual(2, subscriber1Count, "Subscriber 1 must receive OnAdDisplayed from both networks");
            Assert.AreEqual(2, subscriber2Count, "Subscriber 2 must receive OnAdDisplayed from both networks");
        }

        /// <summary>
        /// M2: Two subscribers on all 7 events — each receives exactly one call per trigger,
        ///     across both primary and secondary networks (14 total triggers, 2 subscribers each).
        /// </summary>
        [Test]
        public void M2_MultipleSubscribers_AllEvents_BothReceiveEveryTrigger()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            // Use arrays indexed by subscriber (0 or 1) for all 7 event types
            int[] displayed   = new int[2], failed  = new int[2], clicked    = new int[2];
            int[] impression  = new int[2], closed  = new int[2], reward     = new int[2];
            int[] revenue     = new int[2];

            for (int i = 0; i < 2; i++)
            {
                int idx = i; // capture for lambda
                orc.OnAdDisplayed          += () =>            displayed[idx]++;
                orc.OnAdFailedDisplayed    += () =>            failed[idx]++;
                orc.OnAdClicked            += () =>            clicked[idx]++;
                orc.OnAdImpressionRecorded += () =>            impression[idx]++;
                orc.OnAdClosed             += () =>            closed[idx]++;
                // Sequential — non-atomic array[idx]++ is safe here (see comment below)
                orc.OnUserEarnedReward     += (_, _t) =>       reward[idx]++;
                orc.OnAdRevenuePaid        += (_, _c, _m) =>   revenue[idx]++;
            }

            // Fire each event from both networks SEQUENTIALLY — this test focuses on
            // multi-subscriber routing correctness, not thread safety.  Non-atomic array[idx]++
            // is safe because all triggers run on the calling (main) thread.
            // For concurrent multi-subscriber correctness see M4.
            _primary.TriggerAdDisplayed();        _secondary.TriggerAdDisplayed();
            _primary.TriggerAdFailedDisplayed();  _secondary.TriggerAdFailedDisplayed();
            _primary.TriggerAdClicked();          _secondary.TriggerAdClicked();
            _primary.TriggerAdImpressionRecorded(); _secondary.TriggerAdImpressionRecorded();
            _primary.TriggerAdClosed();           _secondary.TriggerAdClosed();
            _primary.TriggerUserEarnedReward(1, "a"); _secondary.TriggerUserEarnedReward(2, "b");
            _primary.TriggerAdRevenuePaid(0.1, "USD", null); _secondary.TriggerAdRevenuePaid(0.2, "USD", null);

            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(2, displayed[i],  $"Subscriber {i} OnAdDisplayed count");
                Assert.AreEqual(2, failed[i],     $"Subscriber {i} OnAdFailedDisplayed count");
                Assert.AreEqual(2, clicked[i],    $"Subscriber {i} OnAdClicked count");
                Assert.AreEqual(2, impression[i], $"Subscriber {i} OnAdImpressionRecorded count");
                Assert.AreEqual(2, closed[i],     $"Subscriber {i} OnAdClosed count");
                Assert.AreEqual(2, reward[i],     $"Subscriber {i} OnUserEarnedReward count");
                Assert.AreEqual(2, revenue[i],    $"Subscriber {i} OnAdRevenuePaid count");
            }
        }

        /// <summary>
        /// M3: Unsubscribed listener no longer receives events after removal.
        ///     Verifies -= on C# multicast delegate removes exactly one registration.
        /// </summary>
        [Test]
        public void M3_Subscriber_AfterUnsubscribe_NoLongerReceivesEvents()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);
            int count = 0;
            Action handler = () => count++;

            orc.OnAdDisplayed += handler;
            _primary.TriggerAdDisplayed();
            Assert.AreEqual(1, count, "Handler must fire before unsubscribe");

            orc.OnAdDisplayed -= handler;
            _primary.TriggerAdDisplayed();
            Assert.AreEqual(1, count, "Handler must NOT fire after unsubscribe");
        }

        /// <summary>
        /// M4: Two subscribers on OnAdDisplayed during concurrent triggers from both networks.
        ///     Each subscriber must receive exactly 2 calls (once per network), total = 4.
        /// </summary>
        [Test]
        public void M4_Race_MultipleSubscribers_OnAdDisplayed_BothReceiveAllConcurrentEvents()
        {
            var orc = new HybridAdOrchestrator(_primary, _secondary);

            int sub1 = 0, sub2 = 0;
            orc.OnAdDisplayed += () => Interlocked.Increment(ref sub1);
            orc.OnAdDisplayed += () => Interlocked.Increment(ref sub2);

            var t1 = Task.Run(() => _primary.TriggerAdDisplayed());
            var t2 = Task.Run(() => _secondary.TriggerAdDisplayed());

            bool completed = WaitAll(t1, t2);

            Assert.IsTrue(completed, "No deadlock expected");
            Assert.AreEqual(2, sub1, "Subscriber 1 must receive 2 events (primary + secondary)");
            Assert.AreEqual(2, sub2, "Subscriber 2 must receive 2 events (primary + secondary)");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Waits for all tasks to complete successfully within <see cref="RaceTimeoutMs"/>.
        ///
        /// Returns <c>true</c> when all tasks finish within the timeout (no deadlock).
        /// Returns <c>false</c> on genuine timeout (tasks still running — deadlock suspected).
        /// Rethrows <see cref="AggregateException"/> if any task faults, surfacing real errors
        /// immediately rather than masking them behind a confusing count mismatch.
        ///
        /// Contrast with the sibling file <c>MediationCallbackRaceTest.cs</c>, which swallows
        /// faults in tests that explicitly expect <c>UnityException</c> from background threads.
        /// The tasks in this file are not expected to fault — if they do, it is a real bug.
        /// </summary>
        private static bool WaitAll(params Task[] tasks)
        {
            // Task.WhenAll(tasks).Wait(timeout):
            //   • Returns true  — all tasks ran to completion within timeout
            //   • Returns false — timeout, tasks may still be running (deadlock)
            //   • Throws AggregateException — tasks faulted (completed with errors)
            // We let AggregateException propagate so the test fails with the actual exception
            // rather than silently passing the "no deadlock" assertion with a hidden fault.
            return Task.WhenAll(tasks).Wait(RaceTimeoutMs);
        }
    }
}
