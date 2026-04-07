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
        public HybridAdOrchestrator(
            IAdNetwork primary,
            IAdNetwork secondary = null,
            Dictionary<string, string> adFormatOverrides = null,
            AdNetworkPerformanceTracker performanceTracker = null,
            bool dynamicOptimization = false)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _secondary = secondary;
            _adFormatOverrides = adFormatOverrides ?? new Dictionary<string, string>();
            _performanceTracker = performanceTracker;
            _dynamicOptimization = dynamicOptimization;

            SubscribeToNetworkEvents(_primary);
            if (_secondary != null)
            {
                SubscribeToNetworkEvents(_secondary);
            }
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
        /// </summary>
        /// <param name="format">The ad format being shown (for routing).</param>
        /// <param name="showAction">Action that takes a network and shows the ad.</param>
        /// <param name="isReady">Function that checks if a network has the ad ready.</param>
        public void ShowWithFallback(string format, Action<IAdNetwork> showAction, Func<IAdNetwork, bool> isReady = null)
        {
            var preferred = GetNetworkForFormat(format);
            var fallback = preferred == _primary ? _secondary : _primary;

            if (isReady == null || isReady(preferred))
            {
                _log.Debug($"Showing {format} ad from preferred network: {preferred.NetworkName}");
                _isAdShowing = true;
                showAction(preferred);
                return;
            }

            if (fallback != null && (isReady == null || isReady(fallback)))
            {
                _log.Info($"Preferred network ({preferred.NetworkName}) not ready for {format}. Falling back to {fallback.NetworkName}.");
                _isAdShowing = true;
                showAction(fallback);
                return;
            }

            _log.Warning($"No network has a ready {format} ad.");
            _onAdFailedDisplayed?.Invoke();
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
