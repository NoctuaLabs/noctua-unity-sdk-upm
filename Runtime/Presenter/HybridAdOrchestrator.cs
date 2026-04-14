using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Orchestrates ad display across primary and secondary ad networks with fallback logic.
    /// When only a primary network is configured, all calls delegate directly to it.
    /// When both are configured, supports per-format routing and automatic fallback.
    /// </summary>
    public class HybridAdOrchestrator
    {
        private readonly NoctuaLogger _log = new(typeof(HybridAdOrchestrator));

        private readonly IAdNetwork _primary;
        private readonly IAdNetwork _secondary;
        private readonly Dictionary<string, string> _adFormatOverrides;
        private readonly AdNetworkPerformanceTracker _performanceTracker;
        private readonly bool _dynamicOptimization;
        private CpmFloorManager _cpmFloorManager;
        private string _segmentKey;

        private bool _primaryInitialized;
        private bool _secondaryInitialized;
        private bool _isAdShowing;

        // Events forwarded from whichever network showed the ad
        private event Action _onAdDisplayed;
        private event Action _onAdFailedDisplayed;
        private event Action _onAdClicked;
        private event Action _onAdImpressionRecorded;
        private event Action _onAdClosed;
        private event Action<double, string> _onUserEarnedReward;
        private event Action<double, string, Dictionary<string, string>> _onAdRevenuePaid;

        /// <summary>Fires when any ad is successfully displayed.</summary>
        public event Action OnAdDisplayed { add => _onAdDisplayed += value; remove => _onAdDisplayed -= value; }
        /// <summary>Fires when any ad fails to display.</summary>
        public event Action OnAdFailedDisplayed { add => _onAdFailedDisplayed += value; remove => _onAdFailedDisplayed -= value; }
        /// <summary>Fires when any displayed ad is clicked.</summary>
        public event Action OnAdClicked { add => _onAdClicked += value; remove => _onAdClicked -= value; }
        /// <summary>Fires when an ad impression is recorded.</summary>
        public event Action OnAdImpressionRecorded { add => _onAdImpressionRecorded += value; remove => _onAdImpressionRecorded -= value; }
        /// <summary>Fires when any displayed ad is closed.</summary>
        public event Action OnAdClosed { add => _onAdClosed += value; remove => _onAdClosed -= value; }
        /// <summary>Fires when the user earns a reward (network-agnostic).</summary>
        public event Action<double, string> OnUserEarnedReward { add => _onUserEarnedReward += value; remove => _onUserEarnedReward -= value; }
        /// <summary>Fires when ad revenue is recorded (network-agnostic).</summary>
        public event Action<double, string, Dictionary<string, string>> OnAdRevenuePaid { add => _onAdRevenuePaid += value; remove => _onAdRevenuePaid -= value; }

        /// <summary>Returns whether a fullscreen ad is currently being displayed.</summary>
        public bool IsAdShowing => _isAdShowing;

        /// <summary>Returns the primary ad network.</summary>
        public IAdNetwork Primary => _primary;

        /// <summary>Returns the secondary ad network (may be null).</summary>
        public IAdNetwork Secondary => _secondary;

        /// <summary>Returns true if hybrid mode is active (both networks configured).</summary>
        public bool IsHybridMode => _secondary != null;

        /// <summary>
        /// Creates a new HybridAdOrchestrator.
        /// </summary>
        /// <param name="primary">The primary ad network (required).</param>
        /// <param name="secondary">Optional secondary ad network for fallback.</param>
        /// <param name="adFormatOverrides">Per-format network preference overrides.</param>
        /// <param name="performanceTracker">Optional tracker for dynamic optimization.</param>
        /// <param name="dynamicOptimization">Whether to use performance-based routing.</param>
        /// <param name="cpmFloorManager">Optional CPM floor manager for floor enforcement.</param>
        /// <param name="segmentKey">Composite user segment key for floor resolution (e.g. "t1_nonpayer_loyal_d30plus").</param>
        public HybridAdOrchestrator(
            IAdNetwork primary,
            IAdNetwork secondary = null,
            Dictionary<string, string> adFormatOverrides = null,
            AdNetworkPerformanceTracker performanceTracker = null,
            bool dynamicOptimization = false,
            CpmFloorManager cpmFloorManager = null,
            string segmentKey = null)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _secondary = secondary;
            _adFormatOverrides = adFormatOverrides ?? new Dictionary<string, string>();
            _performanceTracker = performanceTracker;
            _dynamicOptimization = dynamicOptimization;
            _cpmFloorManager = cpmFloorManager;
            _segmentKey = segmentKey ?? "";

            SubscribeToNetworkEvents(_primary);
            if (_secondary != null)
            {
                SubscribeToNetworkEvents(_secondary);
            }
        }

        /// <summary>Updates the cached composite segment key used for CPM floor resolution.</summary>
        internal void UpdateSegmentKey(string segmentKey)
        {
            _segmentKey = segmentKey ?? "";
        }

        /// <summary>Updates the CPM floor manager (called when experiment overrides are applied).</summary>
        internal void UpdateCpmFloorManager(CpmFloorManager cpmFloorManager)
        {
            _cpmFloorManager = cpmFloorManager;
        }

        /// <summary>
        /// Initializes both ad networks.
        /// <para><paramref name="onPrimaryReady"/> fires when the primary network is ready — safe to load primary ads.</para>
        /// <para><paramref name="onSecondaryReady"/> fires when the secondary network is ready — safe to load secondary ads.
        /// Never fires when there is no secondary network.</para>
        /// Both initializations run concurrently; always wait for each callback before calling Load on that network.
        /// </summary>
        public void Initialize(Action onPrimaryReady, Action onSecondaryReady = null)
        {
            _log.Info($"Initializing orchestrator. Primary: {_primary.NetworkName}" +
                (_secondary != null ? $", Secondary: {_secondary.NetworkName}" : ""));

            _primary.Initialize(() =>
            {
                _primaryInitialized = true;
                _log.Info($"Primary network ({_primary.NetworkName}) initialized.");
                onPrimaryReady?.Invoke();
            });

            if (_secondary != null)
            {
                _secondary.Initialize(() =>
                {
                    _secondaryInitialized = true;
                    _log.Info($"Secondary network ({_secondary.NetworkName}) initialized.");
                    onSecondaryReady?.Invoke();
                });
            }
        }

        /// <summary>
        /// Returns the preferred network for the given ad format based on overrides,
        /// dynamic optimization, or defaulting to primary.
        /// </summary>
        /// <param name="format">Ad format: "interstitial", "rewarded", "banner", "app_open", etc.</param>
        public IAdNetwork GetNetworkForFormat(string format)
        {
            // Check per-format overrides first
            if (_adFormatOverrides.TryGetValue(format, out string preferredNetwork))
            {
                if (_secondary != null && preferredNetwork == _secondary.NetworkName)
                {
                    return _secondary;
                }

                // Override target requested a network that isn't available (e.g. secondary not
                // initialized or wrong name). Fall back to primary and warn so it's not silent.
                if (preferredNetwork != _primary.NetworkName)
                {
                    _log.Warning($"Ad format override for '{format}' targets '{preferredNetwork}' " +
                        $"but that network is not available. Falling back to primary ({_primary.NetworkName}).");
                }

                return _primary;
            }

            // Dynamic optimization: use performance tracker
            if (_dynamicOptimization && _performanceTracker != null)
            {
                string best = _performanceTracker.GetPreferredNetwork(format);
                if (best != null && _secondary != null && best == _secondary.NetworkName)
                {
                    return _secondary;
                }
            }

            return _primary;
        }

        /// <summary>
        /// Attempts to show an ad using the preferred network, falling back to the other on failure.
        /// CPM floor checks are applied when a <see cref="CpmFloorManager"/> and
        /// <see cref="AdNetworkPerformanceTracker"/> are configured:
        ///   - HardFail on preferred → skip to fallback.
        ///   - HardFail on both → fire OnAdFailedDisplayed immediately.
        ///   - SoftFail → log warning, proceed with the network anyway.
        /// </summary>
        /// <param name="format">The ad format being shown (for routing).</param>
        /// <param name="showAction">Action that takes a network and shows the ad.</param>
        /// <param name="isReady">Function that checks if a network has the ad ready.</param>
        public void ShowWithFallback(string format, Action<IAdNetwork> showAction, Func<IAdNetwork, bool> isReady = null)
        {
            var preferred = GetNetworkForFormat(format);
            var fallback = preferred == _primary ? _secondary : _primary;

            bool preferredFloorPassed = EvaluateCpmFloor(preferred, format);

            if (preferredFloorPassed && (isReady == null || isReady(preferred)))
            {
                _log.Debug($"Showing {format} ad from preferred network: {preferred.NetworkName}");
                _isAdShowing = true;
                showAction(preferred);
                return;
            }

            if (fallback != null)
            {
                bool fallbackFloorPassed = EvaluateCpmFloor(fallback, format);

                if (fallbackFloorPassed && (isReady == null || isReady(fallback)))
                {
                    string reason = !preferredFloorPassed
                        ? $"Preferred network ({preferred.NetworkName}) hard-floor blocked"
                        : $"Preferred network ({preferred.NetworkName}) not ready";
                    _log.Info($"{reason} for {format}. Falling back to {fallback.NetworkName}.");
                    _isAdShowing = true;
                    showAction(fallback);
                    return;
                }
            }

            _log.Warning($"No network has a ready {format} ad (floor or availability check failed).");
            _onAdFailedDisplayed?.Invoke();
        }

        /// <summary>
        /// Evaluates CPM floor for the given network and format.
        /// Returns true if the network passes (or no floor is configured).
        /// Returns false on HardFail. SoftFail returns true (logged as warning in CpmFloorManager).
        /// </summary>
        private bool EvaluateCpmFloor(IAdNetwork network, string format)
        {
            if (_cpmFloorManager == null || _performanceTracker == null)
                return true;

            double avgCpm = _performanceTracker.GetAverageCpm(network.NetworkName, format);
            int samples   = _performanceTracker.GetSampleCount(network.NetworkName, format);
            var result    = _cpmFloorManager.EvaluateFloor(network.NetworkName, format, avgCpm, samples, _segmentKey);

            return result != CpmFloorResult.HardFail;
        }

        private void SubscribeToNetworkEvents(IAdNetwork network)
        {
            network.OnAdDisplayed += () =>
            {
                _isAdShowing = true;
                _onAdDisplayed?.Invoke();
            };

            network.OnAdFailedDisplayed += () =>
            {
                _isAdShowing = false;
                _onAdFailedDisplayed?.Invoke();
            };

            network.OnAdClicked += () =>
            {
                _onAdClicked?.Invoke();
            };

            network.OnAdImpressionRecorded += () =>
            {
                _onAdImpressionRecorded?.Invoke();
            };

            network.OnAdClosed += () =>
            {
                _isAdShowing = false;
                _onAdClosed?.Invoke();
            };

            network.OnUserEarnedReward += (amount, type) =>
            {
                _onUserEarnedReward?.Invoke(amount, type);
            };

            network.OnAdRevenuePaid += (revenue, currency, metadata) =>
            {
                _onAdRevenuePaid?.Invoke(revenue, currency, metadata);
            };
        }
    }
}
