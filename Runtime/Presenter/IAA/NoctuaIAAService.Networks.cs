using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using com.noctuagames.sdk.AdPlaceholder;
using com.noctuagames.sdk.Events;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
using static GoogleMobileAds.Api.AdValue;
using com.noctuagames.sdk.Admob;
#endif

namespace com.noctuagames.sdk
{
    public partial class NoctuaIAAService
    {
        private void CreateNetworks(IAA iaaConfig)
        {
            _log.Debug($"{LogTag} create_networks - create ad networks from config");
            // ── Compiled-in SDK availability check ─────────────────────────────
            // Surfaces which mediation SDKs the build was compiled against so
            // game devs can spot a missing UPM package (or unset scripting
            // define) early in the log instead of debugging silent ad failures.
            string admobStatus    = "missing";
            string applovinStatus = "missing";
#if UNITY_ADMOB
            admobStatus = "integrated";
#endif
#if UNITY_APPLOVIN
            applovinStatus = "integrated";
#endif
            _log.Info($"IAA SDK availability: AdMob={admobStatus}, AppLovin={applovinStatus}. " +
                $"Requested in noctuagg.json — mediation='{iaaConfig.Mediation}', " +
                $"secondary_mediation='{iaaConfig.SecondaryMediation}'.");

#if !UNITY_ADMOB && !UNITY_APPLOVIN
            _log.Warning(
                "No ad mediation SDK is integrated in this build. Install AppLovin MAX " +
                "and/or Google Mobile Ads via the Noctua Integration Manager " +
                "(menu: Noctua → Noctua Integration Manager) and ensure UNITY_APPLOVIN / " +
                "UNITY_ADMOB scripting defines are set in Player Settings. Without one of " +
                "these, no ads can be shown and iaa.mediation in noctuagg.json has no effect.");
#endif

            // Clean up existing AppLovin instances before replacing them so that stale
            // handlers are unregistered from the static MaxSdkCallbacks events.
#if UNITY_APPLOVIN
            if (_routing != null)
            {
                (_routing.Primary as AppLovinNetwork)?.Cleanup();
                (_routing.Secondary as AppLovinNetwork)?.Cleanup();
            }
#endif

            // Network selection is config-driven: iaa.mediation picks primary,
            // iaa.secondary_mediation picks secondary. The compiled-in SDK
            // defines (UNITY_ADMOB / UNITY_APPLOVIN) gate availability — they
            // do NOT override the config's intent. If the requested primary's
            // SDK isn't compiled in but the secondary's is, the secondary is
            // promoted to primary so the game still gets ads.

            // Diagnose typos / unsupported names early — without this, an
            // unrecognised name would silently fall through to the "no SDK
            // available" error even when the SDK is actually compiled in.
            WarnIfUnknownMediationName("mediation",           iaaConfig.Mediation);
            WarnIfUnknownMediationName("secondary_mediation", iaaConfig.SecondaryMediation);

            // Compile-time SDK availability. Hoisted into local bools so the
            // pure policy function ResolveMediationSelection can be unit-tested
            // without compile-define gymnastics — see MediationSelectionMatrixTest.
            bool admobAvailable    = false;
            bool applovinAvailable = false;
#if UNITY_ADMOB
            admobAvailable = true;
#endif
#if UNITY_APPLOVIN
            applovinAvailable = true;
#endif

            string primaryRequested   = NormalizeMediationName(iaaConfig.Mediation);
            string secondaryRequested = NormalizeMediationName(iaaConfig.SecondaryMediation);

            // Detect diagnostic conditions BEFORE selection so warnings fire
            // with the original config intent intact.
            bool isDuplicate =
                !string.IsNullOrEmpty(primaryRequested) &&
                primaryRequested == secondaryRequested;

            var (selectedPrimary, selectedSecondary) = ResolveMediationSelection(
                iaaConfig.Mediation, iaaConfig.SecondaryMediation,
                admobAvailable, applovinAvailable);

            if (isDuplicate)
            {
                _log.Warning(
                    $"iaa.mediation and iaa.secondary_mediation are both '{primaryRequested}'. " +
                    "Treating as single-network — secondary is ignored. " +
                    "Set them to different networks to enable hybrid mode.");
            }

            // Promotion fired when primary's SDK is missing but secondary's exists.
            bool wasPromoted =
                !isDuplicate &&
                !string.IsNullOrEmpty(primaryRequested) &&
                IsRecognisedMediationName(primaryRequested) &&
                !IsAvailable(primaryRequested, admobAvailable, applovinAvailable) &&
                selectedPrimary != null;
            if (wasPromoted)
            {
                _log.Warning(
                    $"Primary mediation '{iaaConfig.Mediation}' requested but its SDK is not compiled in. " +
                    $"Promoting secondary '{iaaConfig.SecondaryMediation}' to primary.");
            }

            // Secondary requested but its SDK isn't compiled in (and no promotion).
            bool secondaryUnavailable =
                !isDuplicate &&
                !wasPromoted &&
                !string.IsNullOrEmpty(secondaryRequested) &&
                IsRecognisedMediationName(secondaryRequested) &&
                !IsAvailable(secondaryRequested, admobAvailable, applovinAvailable) &&
                selectedPrimary != null;
            if (secondaryUnavailable)
            {
                _log.Warning(
                    $"Secondary mediation '{iaaConfig.SecondaryMediation}' requested but its SDK is not compiled in. " +
                    $"Continuing in single-network mode with primary '{selectedPrimary}'.");
            }

            IAdNetwork primary   = TryCreateNetwork(selectedPrimary);
            IAdNetwork secondary = TryCreateNetwork(selectedSecondary);

            if (primary == null)
            {
                _log.Error(
                    $"No ad network SDK is available for the requested config " +
                    $"(mediation='{iaaConfig.Mediation}', secondary_mediation='{iaaConfig.SecondaryMediation}'). " +
                    "Define UNITY_ADMOB or UNITY_APPLOVIN, or set iaa.mediation in noctuagg.json " +
                    "to a network whose SDK is integrated.");
                // Drop the prior routing reference — its AppLovin handlers
                // were already Cleanup()'d at the top of this method, so leaving
                // it in place would expose half-dead state to subsequent calls.
                _routing = null;
                return;
            }

            // Ensure AppOpen cooldown has a sensible default when not set in cooldown_seconds.
            var mergedCooldowns = iaaConfig.CooldownSeconds ?? new CooldownConfig();
            if (mergedCooldowns.AppOpen <= 0)
            {
                mergedCooldowns.AppOpen = 30;
            }

            // Create supporting managers
            _frequencyCap = new AdFrequencyCap(
                iaaConfig.FrequencyCaps,
                mergedCooldowns,
                iaaConfig.EnabledFormats
            );

            _revenueTracker = new AdRevenueReporter(_adRevenueTracker, iaaConfig.Taichi);

            _performanceTracker = (iaaConfig.DynamicOptimization ?? false)
                ? new AdNetworkPerformanceTracker()
                : null;

            // Preserve the segment manager across CreateNetworks calls so session/install state
            // accumulated in PlayerPrefs is not lost when the config is reapplied.
            _segments ??= new UserSegments();

            _cpmFloor = (iaaConfig.CpmFloors?.Enabled == true)
                ? new CpmFloor(iaaConfig.CpmFloors)
                : null;

            string segmentKey = _segments.GetCompositeSegment(_cachedCountryCode);

            _routing = new HybridAdRouting(
                primary: primary,
                secondary: secondary,
                adFormatOverrides: iaaConfig.AdFormatOverrides,
                performanceTracker: _performanceTracker,
                dynamicOptimization: iaaConfig.DynamicOptimization ?? false,
                cpmFloor: _cpmFloor,
                segmentKey: segmentKey
            );

            _log.Info($"Networks created. Primary: {primary.NetworkName}" +
                (secondary != null ? $", Secondary: {secondary.NetworkName}" : "") +
                $", Hybrid: {_routing.IsHybridMode}" +
                $", CpmFloors: {(_cpmFloor != null ? "enabled" : "disabled")}" +
                $", Segment: {segmentKey}");

            EmitAppliedIaaConfigEvent(
                primary:           primary.NetworkName,
                secondary:         secondary?.NetworkName,
                hybrid:            _routing.IsHybridMode,
                cpmFloorsEnabled:  _cpmFloor != null,
                segmentKey:        segmentKey,
                configOrigin:      _nextConfigOrigin);
        }

        /// <summary>
        /// Builds the <c>applied_iaa_config</c> payload mirroring the "Networks
        /// created" Info log and routes it through the injected NoctuaEventService.
        /// If the tracker isn't yet wired (first CreateNetworks call from the
        /// constructor), the payload is stashed and replayed by
        /// <see cref="FlushPendingAppliedIaaConfigEvent"/> when SetAdRevenueTracker
        /// fires.
        /// </summary>
        private void EmitAppliedIaaConfigEvent(
            string primary,
            string secondary,
            bool   hybrid,
            bool   cpmFloorsEnabled,
            string segmentKey,
            string configOrigin)
        {
            _log.Debug($"{LogTag} emit_applied_iaa_config - emit applied IAA config event");
            var payload = new Dictionary<string, IConvertible>
            {
                { "primary",          primary   ?? "" },
                { "secondary",        secondary ?? "" },
                { "hybrid",           hybrid },
                { "cpm_floors",       cpmFloorsEnabled ? "enabled" : "disabled" },
                { "segment",          segmentKey ?? "" },
                { "config_origin",    configOrigin ?? IaaConfigOriginLocal },
            };

            if (_adRevenueTracker is NoctuaEventService eventService)
            {
                try
                {
                    eventService.TrackCustomEvent("applied_iaa_config", payload);
                }
                catch (Exception ex)
                {
                    _log.Error($"Error emitting applied_iaa_config event: {ex.Message}");
                }
            }
            else
            {
                // Most recent payload wins — if CreateNetworks runs again before
                // the tracker is wired, we want the latest snapshot, not a stale one.
                _pendingAppliedIaaConfigPayload = payload;
                _log.Debug("applied_iaa_config queued — will emit after event tracker is wired.");
            }
        }

        /// <summary>
        /// Replays a deferred <c>applied_iaa_config</c> event the moment a tracker
        /// becomes available. Called from <see cref="SetAdRevenueTracker"/>.
        /// </summary>
        private void FlushPendingAppliedIaaConfigEvent()
        {
            _log.Debug($"{LogTag} flush_applied_iaa_config - flush pending applied IAA config event");
            if (_pendingAppliedIaaConfigPayload == null) return;
            if (_adRevenueTracker is NoctuaEventService eventService)
            {
                try
                {
                    eventService.TrackCustomEvent("applied_iaa_config", _pendingAppliedIaaConfigPayload);
                }
                catch (Exception ex)
                {
                    _log.Error($"Error emitting deferred applied_iaa_config event: {ex.Message}");
                }
                _pendingAppliedIaaConfigPayload = null;
            }
        }

        /// <summary>
        /// Lower-cases and trims a raw mediation name so noctuagg.json can use
        /// any casing (e.g. "AdMob", "ADMOB", "AppLovin") without breaking the
        /// equality-based dispatch in <see cref="TryCreateNetwork"/>.
        /// Returns null for null/empty input so callers can short-circuit.
        /// </summary>
        public static string NormalizeMediationName(string raw)
        {
            _sLog.Debug($"{LogTag} normalize_mediation_name - normalize mediation name");
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return raw.Trim().ToLowerInvariant();
        }

        /// <summary>True iff <paramref name="normalized"/> is one of the supported network names.</summary>
        public static bool IsRecognisedMediationName(string normalized)
        {
            _sLog.Debug($"{LogTag} is_recognised_mediation_name - check mediation name recognised");
            return normalized == AdNetworkName.Admob
                || normalized == AdNetworkName.AppLovin;
        }

        /// <summary>
        /// True iff the (already-normalised) mediation name's SDK is compiled in
        /// per the supplied availability flags.
        /// </summary>
        public static bool IsAvailable(string normalized, bool admobAvailable, bool applovinAvailable)
        {
            _sLog.Debug($"{LogTag} is_network_available - check network availability");
            if (string.IsNullOrEmpty(normalized)) return false;
            if (normalized == AdNetworkName.Admob)    return admobAvailable;
            if (normalized == AdNetworkName.AppLovin) return applovinAvailable;
            return false;
        }

        /// <summary>
        /// Pure selection policy for <c>iaa.mediation</c> / <c>iaa.secondary_mediation</c>.
        /// Returns the (primary, secondary) network names (lower-case "admob" /
        /// "applovin", or null) that the routing should be wired with,
        /// given the requested config and the SDK availability flags.
        ///
        /// Rules, in order:
        ///   1. Normalise both names (lower-case, trim).
        ///   2. If primary == secondary → secondary dropped (dedup).
        ///   3. Each name resolves to itself when its SDK is available, else null.
        ///   4. If primary resolved to null but secondary did not → secondary
        ///      promoted to primary, secondary becomes null.
        ///
        /// This function emits no logs and has no side effects — diagnostic
        /// warnings are emitted by <see cref="CreateNetworks"/> based on the
        /// difference between input and output.
        /// </summary>
        public static (string primary, string secondary) ResolveMediationSelection(
            string primaryRaw,
            string secondaryRaw,
            bool admobAvailable,
            bool applovinAvailable)
        {
            string p = NormalizeMediationName(primaryRaw);
            string s = NormalizeMediationName(secondaryRaw);

            // Dedup — identical names collapse to single-network.
            if (!string.IsNullOrEmpty(p) && p == s)
            {
                s = null;
            }

            string resolvedPrimary   = IsAvailable(p, admobAvailable, applovinAvailable) ? p : null;
            string resolvedSecondary = IsAvailable(s, admobAvailable, applovinAvailable) ? s : null;

            // Promote — fall back to whichever SDK is actually available so the
            // game still gets ads when the configured primary is missing.
            if (resolvedPrimary == null && resolvedSecondary != null)
            {
                resolvedPrimary   = resolvedSecondary;
                resolvedSecondary = null;
            }

            return (resolvedPrimary, resolvedSecondary);
        }

        /// <summary>
        /// Logs a warning when a non-empty mediation name doesn't match any
        /// supported network. Helps surface typos in noctuagg.json instead of
        /// letting them fall through to the generic "no SDK available" error.
        /// </summary>
        private void WarnIfUnknownMediationName(string field, string raw)
        {
            _log.Debug($"{LogTag} warn_unknown_mediation_name - warn on unknown mediation name");
            string normalized = NormalizeMediationName(raw);
            if (string.IsNullOrEmpty(normalized)) return;

            if (normalized != AdNetworkName.Admob &&
                normalized != AdNetworkName.AppLovin)
            {
                _log.Warning(
                    $"Unknown iaa.{field} value '{raw}' in noctuagg.json. " +
                    $"Supported: '{AdNetworkName.Admob}', '{AdNetworkName.AppLovin}'. " +
                    "This entry will be ignored.");
            }
        }

        /// <summary>
        /// Pure factory: returns an <see cref="IAdNetwork"/> for the given mediation
        /// name, or <c>null</c> when the name is empty or the corresponding SDK is
        /// not compiled in. Used by <see cref="CreateNetworks"/> so primary/secondary
        /// selection follows the config rather than the build's SDK defines.
        /// Caller is expected to pass an already-normalised name (see
        /// <see cref="NormalizeMediationName"/>).
        /// </summary>
        private static IAdNetwork TryCreateNetwork(string mediationName)
        {
            _sLog.Debug($"{LogTag} try_create_network - try create network instance");
            if (string.IsNullOrEmpty(mediationName)) return null;

            if (mediationName == AdNetworkName.Admob)
            {
#if UNITY_ADMOB
                return new AdmobNetwork();
#else
                return null;
#endif
            }

            if (mediationName == AdNetworkName.AppLovin)
            {
#if UNITY_APPLOVIN
                return new AppLovinNetwork();
#else
                return null;
#endif
            }

            return null;
        }
    }
}
