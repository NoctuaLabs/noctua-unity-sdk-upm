using System;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using UnityEngine;

namespace Tests.Runtime.IAA
{
    /// <summary>
    /// Controllable fake IAdNetwork for IAA unit tests.
    /// Tracks calls and exposes flags to simulate inventory/readiness.
    /// </summary>
    public class MockAdNetwork : IAdNetwork
    {
        // ── State controls (set by tests) ─────────────────────────────────
        public bool InterstitialReady  { get; set; } = true;
        public bool RewardedReady      { get; set; } = true;
        public bool AppOpenReady       { get; set; } = true;

        /// <summary>
        /// Controls what <see cref="HasBannerAdUnit"/> returns.
        /// Also set to <c>true</c> automatically by <see cref="SetBannerAdUnitId"/>.
        /// </summary>
        public bool BannerAdUnitSet    { get; set; } = false;

        // ── Call tracking ─────────────────────────────────────────────────
        public int  InitializeCallCount          { get; private set; }
        public int  LoadInterstitialCallCount     { get; private set; }
        public int  ShowInterstitialCallCount     { get; private set; }
        public int  LoadRewardedCallCount         { get; private set; }
        public int  ShowRewardedCallCount         { get; private set; }
        public int  ShowBannerCallCount           { get; private set; }
        public int  LoadAppOpenCallCount          { get; private set; }
        public int  ShowAppOpenCallCount          { get; private set; }
        public int  SetAppOpenAdUnitCallCount     { get; private set; }

        public string LastInterstitialAdUnitId    { get; private set; }
        public string LastRewardedAdUnitId        { get; private set; }
        public string LastAppOpenAdUnitId         { get; private set; }

        // ── IAdNetwork.NetworkName ─────────────────────────────────────────
        public string NetworkName { get; set; } = "mock";

        // ── Event backing fields ──────────────────────────────────────────
        private event Action _onInitialized;
        private event Action _onAdDisplayed;
        private event Action _onAdFailedDisplayed;
        private event Action _onAdClicked;
        private event Action _onAdImpressionRecorded;
        private event Action _onAdClosed;
        private event Action<double, string> _onUserEarnedReward;
        private event Action<double, string, Dictionary<string, string>> _onAdRevenuePaid;

        // ── IAdNetwork events ─────────────────────────────────────────────
        event Action IAdNetwork.OnInitialized
        {
            add    => _onInitialized += value;
            remove => _onInitialized -= value;
        }

        event Action IAdNetwork.OnAdDisplayed
        {
            add    => _onAdDisplayed += value;
            remove => _onAdDisplayed -= value;
        }

        event Action IAdNetwork.OnAdFailedDisplayed
        {
            add    => _onAdFailedDisplayed += value;
            remove => _onAdFailedDisplayed -= value;
        }

        event Action IAdNetwork.OnAdClicked
        {
            add    => _onAdClicked += value;
            remove => _onAdClicked -= value;
        }

        event Action IAdNetwork.OnAdImpressionRecorded
        {
            add    => _onAdImpressionRecorded += value;
            remove => _onAdImpressionRecorded -= value;
        }

        event Action IAdNetwork.OnAdClosed
        {
            add    => _onAdClosed += value;
            remove => _onAdClosed -= value;
        }

        event Action<double, string> IAdNetwork.OnUserEarnedReward
        {
            add    => _onUserEarnedReward += value;
            remove => _onUserEarnedReward -= value;
        }

        event Action<double, string, Dictionary<string, string>> IAdNetwork.OnAdRevenuePaid
        {
            add    => _onAdRevenuePaid += value;
            remove => _onAdRevenuePaid -= value;
        }

        // ── IAdNetwork methods ────────────────────────────────────────────

        public void Initialize(Action initCompleteAction)
        {
            InitializeCallCount++;
            initCompleteAction?.Invoke();
        }

        public void SetInterstitialAdUnitID(string adUnitID) => LastInterstitialAdUnitId = adUnitID;
        public void LoadInterstitialAd()  => LoadInterstitialCallCount++;
        public void ShowInterstitial()    => ShowInterstitialCallCount++;

        public void SetRewardedAdUnitID(string adUnitID) => LastRewardedAdUnitId = adUnitID;
        public void LoadRewardedAd()  => LoadRewardedCallCount++;
        public void ShowRewardedAd()  => ShowRewardedCallCount++;

        public void SetBannerAdUnitId(string adUnitID) { BannerAdUnitSet = true; }
        public bool HasBannerAdUnit() => BannerAdUnitSet;
        public void ShowBannerAd() => ShowBannerCallCount++;

        public void SetAppOpenAdUnitID(string adUnitID)
        {
            LastAppOpenAdUnitId = adUnitID;
            SetAppOpenAdUnitCallCount++;
        }

        public void LoadAppOpenAd()   => LoadAppOpenCallCount++;
        public void ShowAppOpenAd()   => ShowAppOpenCallCount++;

        public bool IsInterstitialReady() => InterstitialReady;
        public bool IsRewardedAdReady()   => RewardedReady;
        public bool IsAppOpenAdReady()    => AppOpenReady;

        // ── Event triggers (used by tests to simulate network callbacks) ──

        public void TriggerAdDisplayed()          => _onAdDisplayed?.Invoke();
        public void TriggerAdFailedDisplayed()    => _onAdFailedDisplayed?.Invoke();
        public void TriggerAdClicked()            => _onAdClicked?.Invoke();
        public void TriggerAdImpressionRecorded() => _onAdImpressionRecorded?.Invoke();
        public void TriggerAdClosed()             => _onAdClosed?.Invoke();
        public void TriggerUserEarnedReward(double amount, string type)
            => _onUserEarnedReward?.Invoke(amount, type);
        public void TriggerAdRevenuePaid(double revenue, string currency, Dictionary<string, string> meta)
            => _onAdRevenuePaid?.Invoke(revenue, currency, meta);
    }

    /// <summary>Fake IAdRevenueTracker that records all TrackCustomEvent and TrackAdRevenue calls.</summary>
    public class MockAdRevenueTracker : IAdRevenueTracker
    {
        public List<(string EventName, Dictionary<string, IConvertible> Params)> Events { get; } = new();

        public void TrackAdRevenue(string source, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
        {
            var p = extraPayload != null
                ? new Dictionary<string, IConvertible>(extraPayload)
                : new Dictionary<string, IConvertible>();
            p["source"]   = source;
            p["revenue"]  = revenue;
            p["currency"] = currency;
            Events.Add(("ad_revenue", p));
        }

        public void TrackCustomEvent(string eventName, Dictionary<string, IConvertible> eventParams = null)
        {
            Events.Add((eventName, eventParams != null
                ? new Dictionary<string, IConvertible>(eventParams)
                : null));
        }

        public bool WasFired(string eventName) =>
            Events.Exists(e => e.EventName == eventName);

        public int CountFired(string eventName) =>
            Events.FindAll(e => e.EventName == eventName).Count;

        public void Clear() => Events.Clear();
    }

    /// <summary>
    /// Thread-safe <see cref="IAdRevenueTracker"/> for race-condition tests.
    ///
    /// Backed by <see cref="System.Collections.Concurrent.ConcurrentBag{T}"/> so
    /// concurrent background threads can write without data corruption. Use this
    /// instead of <see cref="MockAdRevenueTracker"/> whenever the test fires
    /// callbacks from multiple threads or tasks simultaneously.
    /// </summary>
    public class ConcurrentMockTracker : IAdRevenueTracker
    {
        private readonly System.Collections.Concurrent.ConcurrentBag<string> _events
            = new System.Collections.Concurrent.ConcurrentBag<string>();

        public int TotalEventCount => _events.Count;

        public void TrackAdRevenue(
            string source,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null)
        {
            _events.Add("ad_revenue");
        }

        public void TrackCustomEvent(
            string eventName,
            Dictionary<string, IConvertible> eventParams = null)
        {
            _events.Add(eventName);
        }

        public bool WasFired(string eventName) => _events.Any(e => e == eventName);

        public int CountFired(string eventName)
        {
            int count = 0;
            foreach (string e in _events)
                if (e == eventName) count++;
            return count;
        }

        /// <summary>
        /// Clears all recorded events. Thread-safe: <c>ConcurrentBag.Clear()</c>
        /// is atomic on .NET Standard 2.1+.
        /// </summary>
        public void Clear() => _events.Clear();
    }
}
