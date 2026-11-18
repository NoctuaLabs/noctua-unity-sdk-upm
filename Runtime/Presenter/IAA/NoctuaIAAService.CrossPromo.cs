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
        // --- Placeholder methods ---

        /// <summary>
        /// No-op, retained for source compatibility.
        /// <para>This used to record the last-requested format so a later failure could guess which
        /// cross-promotion to show. That guess was wrong for any failure the game did not ask for —
        /// notably a banner's auto-refresh load failure, which fires while the user is idle and was
        /// attributed to the last fullscreen request, popping an interstitial house ad out of
        /// nowhere. The failing format now travels with the failure itself
        /// (<see cref="IAdNetwork.OnAdFailedDisplayedFormat"/>), so nothing needs to be recorded.</para>
        /// <para>The cross-promotion remains a fallback: it appears only when a real ad attempt
        /// fails / has no fill / is offline, via <c>ShowCrossPromoFallback</c>. To show one on
        /// purpose, call <see cref="ShowCrossPromotion(AdPlaceholderType)"/>.</para>
        /// </summary>
        /// <param name="adType">Ignored.</param>
        public void ShowAdPlaceholder(AdPlaceholderType adType)
        {
        }

        /// <summary>
        /// Configures the size and screen anchor of the banner cross-promotion placeholder box —
        /// the house-ad equivalent of <see cref="CreateBannerViewAdAdmob"/> /
        /// <see cref="CreateBannerViewAdAppLovin(Color, MaxSdkBase.AdViewPosition)"/>. Only affects
        /// <see cref="AdPlaceholderType.Banner"/>; call any time before (or after) the banner
        /// cross-promotion shows — a running placeholder picks up the new layout immediately.
        /// Defaults to <see cref="AdPlaceholderSize.Banner"/> (320×50) at
        /// <see cref="AdPlaceholderPosition.Bottom"/> when never called, matching
        /// <c>BannerAdmob</c>'s own defaults.
        /// </summary>
        /// <param name="size">Placeholder box size in pixels (presets or a custom size).</param>
        /// <param name="position">Screen anchor (edge, corner, or center).</param>
        public void SetCrossPromoBannerLayout(AdPlaceholderSize size, AdPlaceholderPosition position)
        {
            _log.Info($"{LogTag} set_cross_promo_banner_layout - {size.Width}x{size.Height} @ {position}");
            _adPlaceholderUI?.SetBannerLayout(size, position);
        }

        /// <summary>
        /// Name of the Noctua IAA config section holding the cross-promotion creatives —
        /// <c>remote_configs.iaa.cross_promotion</c> in the init-game response (merged with the local
        /// <c>noctuagg.json</c> <c>iaa.cross_promotion</c>). A JSON object keyed by ad placement
        /// (<c>interstitial</c> / <c>rewarded</c> / <c>rewarded_interstitial</c> / <c>banner</c>),
        /// each with <c>asset_url</c>, <c>click_url</c>, <c>min_watch_seconds</c>.
        /// </summary>
        public const string CrossPromotionRemoteConfigKey = "cross_promotion";

        /// <summary>
        /// Effortless cross-promotion: the game passes ONLY the placement type — the SDK resolves the
        /// creative for that placement from the active Noctua IAA config
        /// (<c>remote_configs.iaa.cross_promotion</c> from the init-game response, merged with the
        /// local <c>noctuagg.json</c> <c>iaa.cross_promotion</c>), then caches, renders, and reports
        /// lifecycle exactly like <see cref="ShowCrossPromotion(AdPlaceholderType, CrossPromotionEntry)"/>.
        /// No URLs, config, or manual caching needed. This is the same source the automatic
        /// no-fill fallback (<see cref="ShowCrossPromoFallback"/>) uses — there is no Firebase Remote
        /// Config dependency. Fires <see cref="OnCrossPromoFailed"/> when no creative is configured for
        /// the requested placement.
        /// </summary>
        /// <param name="adType">Which placement to show (interstitial / rewarded / rewarded interstitial / banner).</param>
        public void ShowCrossPromotion(AdPlaceholderType adType)
        {
            _log.Info($"{LogTag} show_cross_promotion - remote-config cross-promotion show ({adType})");

            var crossPromotion = IAAResponse?.CrossPromotion;
            var entry = crossPromotion == null ? null : ResolveCrossPromotionEntry(crossPromotion, adType);

            if (entry == null || string.IsNullOrEmpty(entry.AssetUrl))
            {
                _log.Warning($"No cross-promotion creative for {adType} in the active Noctua IAA config " +
                             $"('{CrossPromotionRemoteConfigKey}').");
                _onCrossPromoFailed?.Invoke();
                return;
            }

            ShowCrossPromotion(adType, entry);
        }

        /// <summary>
        /// Directly shows a Noctua cross-promotion (house-ad) creative from data you pass in — no
        /// <c>noctuagg.json</c> config or remote fetch required. Intended for games running their own
        /// ad mediation that still want to display Noctua cross-promotions on demand. The SDK handles
        /// caching (memory → disk → network, write-through), rendering, the close/min-watch gate, and
        /// the <c>cross_ad_impression</c> analytics event internally — just call it with the creative.
        ///
        /// Lifecycle is reported through the dedicated <see cref="OnCrossPromoDisplayed"/> /
        /// <see cref="OnCrossPromoClicked"/> / <see cref="OnCrossPromoClosed"/> /
        /// <see cref="OnCrossPromoFailed"/> events — kept separate from the real-ad events
        /// (<see cref="OnAdDisplayed"/> etc.) so your own mediation's events never get cross-wired.
        /// This is a house-ad only: it carries NO revenue and never touches the ad-revenue pipeline.
        /// </summary>
        /// <param name="adType">Which placement format to render as (interstitial / rewarded / rewarded interstitial / banner).</param>
        /// <param name="data">The creative to show: asset URL (image or video), optional click-through URL, optional min-watch seconds.</param>
        public void ShowCrossPromotion(AdPlaceholderType adType, CrossPromotionEntry data)
        {
            _log.Info($"{LogTag} show_cross_promotion - direct cross-promotion show ({adType})");

            if (_adPlaceholderUI == null)
            {
                _log.Warning("Ad placeholder UI not initialized. Cannot show cross-promotion.");
                _onCrossPromoFailed?.Invoke();
                return;
            }
            if (data == null || string.IsNullOrEmpty(data.AssetUrl))
            {
                _log.Warning("Cross-promotion data is null or has no asset URL.");
                _onCrossPromoFailed?.Invoke();
                return;
            }

            // The banner is a separate, non-modal surface — it coexists with the full-screen
            // placeholder and never blocks it (nor vice versa).
            if (adType == AdPlaceholderType.Banner)
            {
                ShowBannerCrossPromotion(data, direct: true);
                return;
            }

            if (_crossPromoShown || _crossPromoPending)
            {
                _log.Warning("A full-screen cross-promotion is already showing or pending; ignoring duplicate request.");
                return;
            }

            _crossPromoDirect        = true;
            _hasClosedPlaceholder    = false;   // fresh request
            _crossPromoPending       = true;
            _pendingCrossPromoFormat = PlaceholderTypeToFormat(adType);
            _suppressNextCloseEvent  = false;
            _adPlaceholderUI.ShowAdPlaceholder(adType, data); // downloads-then-shows, caches internally
        }

        /// <summary>
        /// Requests the banner cross-promotion placeholder. Independent from the full-screen
        /// placeholder — no supersede, they coexist. <paramref name="direct"/> routes the lifecycle to
        /// the dedicated <c>OnCrossPromo*</c> events (public API) vs. the shared real-ad events
        /// (no-fill fallback). Returns true when a show was requested.
        /// </summary>
        private bool ShowBannerCrossPromotion(CrossPromotionEntry data, bool direct)
        {
            if (_adPlaceholderUI == null)
            {
                if (direct) _onCrossPromoFailed?.Invoke();
                return false;
            }
            if (data == null || string.IsNullOrEmpty(data.AssetUrl))
            {
                if (direct) _onCrossPromoFailed?.Invoke();
                else        _onAdNotAvailable?.Invoke(AdFormatKey.Banner);
                return false;
            }
            if (_bannerCrossPromoShown || _bannerCrossPromoPending)
            {
                _log.Warning("A banner cross-promotion is already showing or pending; ignoring duplicate request.");
                return false;
            }

            _bannerCrossPromoDirect  = direct;
            _bannerCrossPromoPending = true;
            _suppressNextBannerCloseEvent = false;
            _adPlaceholderUI.ShowBannerPlaceholder(data); // downloads-then-shows, caches internally
            return true;
        }

        /// <summary>
        /// Convenience overload of <see cref="ShowCrossPromotion(AdPlaceholderType, CrossPromotionEntry)"/>
        /// that builds the <see cref="CrossPromotionEntry"/> for you from primitive values.
        /// </summary>
        /// <param name="adType">Which placement format to render as.</param>
        /// <param name="assetUrl">CDN URL of the creative (image or video, detected by extension).</param>
        /// <param name="clickUrl">Optional click-through URL opened on tap. Null/empty = non-clickable.</param>
        /// <param name="minWatchSeconds">Optional seconds before the close button appears. 0 = closable as soon as it renders (or the video ends).</param>
        public void ShowCrossPromotion(AdPlaceholderType adType, string assetUrl,
                                       string clickUrl = null, int minWatchSeconds = 0)
            => ShowCrossPromotion(adType, new CrossPromotionEntry
            {
                AssetUrl = assetUrl, ClickUrl = clickUrl, MinWatchSeconds = minWatchSeconds
            });

        /// <summary>Resolves the per-format cross-promotion entry, or null when unset for that format.</summary>
        private static CrossPromotionEntry ResolveCrossPromotionEntry(CrossPromotionConfig config, AdPlaceholderType adType)
        {
            switch (adType)
            {
                case AdPlaceholderType.Interstitial:         return config.Interstitial;
                case AdPlaceholderType.Rewarded:             return config.Rewarded;
                case AdPlaceholderType.RewardedInterstitial: return config.RewardedInterstitial;
                case AdPlaceholderType.Banner:               return config.Banner;
                default:                                     return null;
            }
        }

        /// <summary>
        /// Closes a shown <b>full-screen</b> cross-promotion placeholder. Used when a real ad is about
        /// to display (<paramref name="force"/> = true) so the real ad takes the screen; the
        /// cross-promotion's close event is suppressed in that case because the real ad fires its own
        /// lifecycle. Does NOT touch the banner surface — use <see cref="CloseCrossPromoBanner"/> for
        /// that. No-op when no full-screen cross-promotion is showing.
        /// </summary>
        /// <param name="force">True when a real ad is taking over — suppresses the cross-promo close event.</param>
        public void CloseAdPlaceholder(bool force = false)
        {
            if (_adPlaceholderUI == null) return;
            if (!_crossPromoShown && !_crossPromoPending) return;

            if (force) _suppressNextCloseEvent = true;

            // A real ad is taking over while the cross-promo was still loading: cancel the pending
            // show so its later shown/failed callback is ignored.
            _crossPromoPending = false;

            // OnPlaceholderClosed (the UI close callback) clears _crossPromoShown and fires the close
            // event unless suppressed.
            _adPlaceholderUI.CloseAdPlaceholder();
        }

        /// <summary>
        /// Closes the <b>banner</b> cross-promotion placeholder. Independent from
        /// <see cref="CloseAdPlaceholder"/> — the full-screen surface is left alone. Use e.g. from a
        /// "remove ads" purchase, or when a real banner ad fills. No-op when the banner is not showing.
        /// </summary>
        /// <param name="force">True when a real ad / "remove ads" is taking over — suppresses the banner close event.</param>
        public void CloseCrossPromoBanner(bool force = false)
        {
            if (_adPlaceholderUI == null) return;
            if (!_bannerCrossPromoShown && !_bannerCrossPromoPending) return;

            if (force) _suppressNextBannerCloseEvent = true;
            _bannerCrossPromoPending = false;
            _adPlaceholderUI.CloseBannerPlaceholder();
        }

        /// <summary>
        /// Requests the cross-promotion placeholder as a fallback house-ad for a format with no real ad
        /// (no fill / not ready / failed display / offline). The asset loads asynchronously: the ad
        /// lifecycle (OnAdDisplayed / OnAdNotAvailable) is driven by the UI's shown/failed callbacks,
        /// not here. Returns true if a show was requested (caller should NOT fire its own failure event —
        /// the outcome arrives via OnPlaceholderShown / OnPlaceholderFailed). Returns false when no
        /// cross-promotion asset is configured for the format.
        /// </summary>
        private bool ShowCrossPromoFallback(AdPlaceholderType type)
        {
            if (_adPlaceholderUI == null) return false;

            // Banner fallback goes to the separate, non-modal banner surface — it coexists with the
            // full-screen placeholder, so it is never gated by _crossPromoShown / _crossPromoPending.
            if (type == AdPlaceholderType.Banner)
            {
                if (_bannerCrossPromoShown || _bannerCrossPromoPending) return true;
                var bannerCfg = IAAResponse?.CrossPromotion;
                var bannerEntry = bannerCfg == null ? null : ResolveCrossPromotionEntry(bannerCfg, type);
                if (bannerEntry == null || string.IsNullOrEmpty(bannerEntry.AssetUrl)) return false;
                _log.Info($"{LogTag} cross_promo_fallback - requesting banner cross-promotion (awaiting asset)");
                return ShowBannerCrossPromotion(bannerEntry, direct: false);
            }

            if (_crossPromoShown || _crossPromoPending) return true;

            // The user already dismissed the cross-promo for this ad request — a late/duplicate
            // network callback must not resurrect it. Suppress the re-show; the game already received
            // its terminal event (OnAdClosed) when the placeholder was closed. Cleared by the next
            // game-initiated Show* call.
            if (_hasClosedPlaceholder)
            {
                _log.Debug($"{LogTag} cross_promo_fallback - placeholder already dismissed for this request; suppressing re-show ({type})");
                return true;
            }

            var crossPromotion = IAAResponse?.CrossPromotion;
            var entry = crossPromotion == null ? null : ResolveCrossPromotionEntry(crossPromotion, type);
            if (entry == null || string.IsNullOrEmpty(entry.AssetUrl))
            {
                _log.Debug($"{LogTag} cross_promo_fallback - no asset for {type}, nothing to show");
                return false;
            }

            _log.Info($"{LogTag} cross_promo_fallback - requesting cross-promotion for {type} (awaiting asset)");
            _crossPromoDirect = false; // fallback always routes to the shared real-ad events
            _crossPromoPending = true;
            _pendingCrossPromoFormat = PlaceholderTypeToFormat(type);
            _suppressNextCloseEvent = false;
            _adPlaceholderUI.ShowAdPlaceholder(type, entry);
            return true;
        }

        /// <summary>
        /// Invoked when the cross-promotion asset has actually rendered. Enters the ad lifecycle by
        /// firing OnAdDisplayed (the game pauses). OnAdClosed follows on dismiss.
        /// </summary>
        private void OnPlaceholderShown()
        {
            if (!_crossPromoPending) return; // superseded (e.g. force-closed) — ignore
            _crossPromoPending = false;
            _crossPromoShown = true;

            // House-ad impression analytics. This is a cross-promotion (house-ad) impression only —
            // it carries NO revenue and is intentionally decoupled from the real IAA ad-revenue
            // pipeline (TrackAdRevenue / ProcessAdmob*Revenue). "ad_placement" is the real ad format
            // this cross-promo stood in for (interstitial / rewarded / rewarded_interstitial /
            // banner), so downstream can attribute the impression to its placement.
            var adPlacement = _pendingCrossPromoFormat ?? "";
            _adRevenueTracker?.TrackCustomEvent("cross_ad_impression", new Dictionary<string, IConvertible>
            {
                { "ad_placement", adPlacement }
            });

            if (_crossPromoDirect)
            {
                _log.Info($"{LogTag} cross_promo - direct asset shown, firing OnCrossPromoDisplayed");
                _onCrossPromoDisplayed?.Invoke();
            }
            else
            {
                _log.Info($"{LogTag} cross_promo - asset shown, firing OnAdDisplayed");
                _onAdDisplayed?.Invoke();
            }
        }

        /// <summary>
        /// Invoked when the cross-promotion asset could not be loaded/shown (not ready / offline / no
        /// cache). No ad is on screen, so report it through the existing "no ad available" path.
        /// </summary>
        private void OnPlaceholderFailed()
        {
            if (!_crossPromoPending) return; // superseded — ignore
            _crossPromoPending = false;
            _crossPromoShown = false;

            var format = _pendingCrossPromoFormat;
            _pendingCrossPromoFormat = null;

            if (_crossPromoDirect)
            {
                _crossPromoDirect = false; // terminal — next fallback routes to shared events
                _log.Info($"{LogTag} cross_promo - direct asset not ready, firing OnCrossPromoFailed ({format})");
                _onCrossPromoFailed?.Invoke();
                return;
            }

            _log.Info($"{LogTag} cross_promo - asset not ready, reporting OnAdNotAvailable ({format})");
            _onAdNotAvailable?.Invoke(format);
        }

        /// <summary>
        /// Invoked when the cross-promotion placeholder is dismissed (user close / auto-close). Mirrors
        /// a real ad's close by firing OnAdClosed so the game resumes — unless the close was forced by
        /// a real ad taking over (then the real ad's own OnAdClosed fires). Never grants a reward.
        /// </summary>
        private void OnPlaceholderClosed()
        {
            if (!_crossPromoShown) return;
            _crossPromoShown = false;
            // Block any async straggler from re-showing the placeholder for this request (cleared on
            // the next game-initiated Show*). Covers both the user-close and force-close (real ad
            // took over) paths.
            _hasClosedPlaceholder = true;

            // Terminal for a direct show either way (placeholder is gone) — capture then reset so the
            // next mediation fallback routes to the shared events.
            bool wasDirect = _crossPromoDirect;
            _crossPromoDirect = false;

            if (_suppressNextCloseEvent)
            {
                _suppressNextCloseEvent = false;
                return;
            }

            if (wasDirect)
            {
                _log.Info($"{LogTag} cross_promo - direct placeholder dismissed, firing OnCrossPromoClosed (no reward)");
                _onCrossPromoClosed?.Invoke();
            }
            else
            {
                _log.Info($"{LogTag} cross_promo - placeholder dismissed, firing OnAdClosed (no reward)");
                _onAdClosed?.Invoke();
            }
        }

        /// <summary>Invoked when the user taps the cross-promotion asset; fires the clicked event.</summary>
        private void OnPlaceholderClicked()
        {
            if (_crossPromoDirect)
            {
                _log.Debug($"{LogTag} cross_promo - direct CTA tapped, firing OnCrossPromoClicked");
                _onCrossPromoClicked?.Invoke();
            }
            else
            {
                _log.Debug($"{LogTag} cross_promo - CTA tapped, firing OnAdClicked");
                _onAdClicked?.Invoke();
            }
        }

        // ── Banner cross-promotion surface — independent lifecycle (mirrors the full-screen handlers
        //    above, but on _bannerCrossPromo* state so the two never interfere). ──────────────────

        /// <summary>The banner cross-promotion asset rendered — fires the displayed event + house-ad impression.</summary>
        private void OnBannerPlaceholderShown()
        {
            if (!_bannerCrossPromoPending) return;
            _bannerCrossPromoPending = false;
            _bannerCrossPromoShown = true;

            _adRevenueTracker?.TrackCustomEvent("cross_ad_impression", new Dictionary<string, IConvertible>
            {
                { "ad_placement", AdFormatKey.Banner }
            });

            if (_bannerCrossPromoDirect)
            {
                _log.Info($"{LogTag} cross_promo - direct banner shown, firing OnCrossPromoDisplayed");
                _onCrossPromoDisplayed?.Invoke();
            }
            else
            {
                _log.Info($"{LogTag} cross_promo - banner shown, firing OnAdDisplayed");
                _onAdDisplayed?.Invoke();
            }
        }

        /// <summary>The banner cross-promotion asset could not be loaded/shown.</summary>
        private void OnBannerPlaceholderFailed()
        {
            if (!_bannerCrossPromoPending) return;
            _bannerCrossPromoPending = false;
            _bannerCrossPromoShown = false;

            if (_bannerCrossPromoDirect)
            {
                _bannerCrossPromoDirect = false;
                _log.Info($"{LogTag} cross_promo - direct banner not ready, firing OnCrossPromoFailed");
                _onCrossPromoFailed?.Invoke();
                return;
            }

            _log.Info($"{LogTag} cross_promo - banner not ready, reporting OnAdNotAvailable (banner)");
            _onAdNotAvailable?.Invoke(AdFormatKey.Banner);
        }

        /// <summary>The banner cross-promotion was dismissed (user close / auto-close / external close).</summary>
        private void OnBannerPlaceholderClosed()
        {
            if (!_bannerCrossPromoShown) return;
            _bannerCrossPromoShown = false;

            bool wasDirect = _bannerCrossPromoDirect;
            _bannerCrossPromoDirect = false;

            if (_suppressNextBannerCloseEvent)
            {
                _suppressNextBannerCloseEvent = false;
                return;
            }

            if (wasDirect)
            {
                _log.Info($"{LogTag} cross_promo - direct banner dismissed, firing OnCrossPromoClosed (no reward)");
                _onCrossPromoClosed?.Invoke();
            }
            else
            {
                _log.Info($"{LogTag} cross_promo - banner dismissed, firing OnAdClosed (no reward)");
                _onAdClosed?.Invoke();
            }
        }

        /// <summary>The user tapped the banner cross-promotion asset (click-through).</summary>
        private void OnBannerPlaceholderClicked()
        {
            if (_bannerCrossPromoDirect)
            {
                _log.Debug($"{LogTag} cross_promo - direct banner CTA tapped, firing OnCrossPromoClicked");
                _onCrossPromoClicked?.Invoke();
            }
            else
            {
                _log.Debug($"{LogTag} cross_promo - banner CTA tapped, firing OnAdClicked");
                _onAdClicked?.Invoke();
            }
        }

        /// <summary>Maps an <see cref="AdFormatKey"/> string to its placeholder type, or null (app open has none).</summary>
        private static AdPlaceholderType? MapFormatToPlaceholderType(string format)
        {
            switch (format)
            {
                case AdFormatKey.Interstitial:         return AdPlaceholderType.Interstitial;
                case AdFormatKey.Rewarded:             return AdPlaceholderType.Rewarded;
                case AdFormatKey.RewardedInterstitial: return AdPlaceholderType.RewardedInterstitial;
                case AdFormatKey.Banner:               return AdPlaceholderType.Banner;
                default:                               return null; // app_open: no placeholder
            }
        }

        /// <summary>Maps a placeholder type back to its <see cref="AdFormatKey"/> string.</summary>
        private static string PlaceholderTypeToFormat(AdPlaceholderType type)
        {
            switch (type)
            {
                case AdPlaceholderType.Interstitial:         return AdFormatKey.Interstitial;
                case AdPlaceholderType.Rewarded:             return AdFormatKey.Rewarded;
                case AdPlaceholderType.RewardedInterstitial: return AdFormatKey.RewardedInterstitial;
                case AdPlaceholderType.Banner:               return AdFormatKey.Banner;
                default:                                     return AdFormatKey.Interstitial;
            }
        }
    }
}
