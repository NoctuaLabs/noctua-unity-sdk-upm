#if UNITY_EDITOR
using System.Collections.Generic;

namespace com.noctuagames.sdk.Editor
{
    /// <summary>
    /// Single source of truth for the Noctua-curated AppLovin MAX + AdMob
    /// mediation adapter catalog. Both the Integration Manager UI
    /// (install / uninstall flows) and the auto-fix stabilizer
    /// (<c>NoctuaAdapterStabilizer</c>) consume these entries so they
    /// never drift out of sync.
    ///
    /// When AppLovin or Google retag a version on the UPM registry, update
    /// the value here and every consumer (manifest heals, install buttons,
    /// UI status checks) picks it up on the next Editor load.
    ///
    /// Keep versions verified against the underlying registries:
    ///   MAX adapters  → <c>unity.packages.applovin.com</c>
    ///   AdMob adapters → <c>package.openupm.com</c>
    /// </summary>
    public static class NoctuaAdapterCatalog
    {
        /// <summary>AppLovin MAX core SDK + AdMob core SDK UPM pins.</summary>
        public static readonly Dictionary<string, (string packageName, string version)> IaaProviders = new()
        {
            { "AdMob",    ("com.google.ads.mobile",      "11.0.0") }, // wraps GMA iOS 13.0.0 + Android 25.0.0
            { "AppLovin", ("com.applovin.mediation.ads", "8.6.2")  }, // wraps MAX SDK 13.6.2
        };

        /// <summary>
        /// AppLovin MAX mediation-adapter UPM catalog.
        /// </summary>
        ///
        /// ⚠  **Cross-catalog version-alignment rules** — when both a MAX
        /// adapter and an AdMob adapter for the SAME network ship in a
        /// consumer's game, CocoaPods will exact-pin both adapters' native
        /// SDK dependencies. If MAX and AdMob pull different native SDK
        /// versions, `pod install` fails with:
        ///
        ///     "CocoaPods could not find compatible versions for pod X"
        ///
        /// Before bumping any MAX adapter below, verify the AdMob counterpart
        /// in <see cref="AdmobAdapters"/> wraps the same underlying native
        /// SDK version. Known-aligned pairs (verified against pod dep graphs):
        ///
        ///   Network            | MAX version           | AdMob version | Shared native pod
        ///   -------------------|-----------------------|---------------|-----------------------
        ///   ByteDance / Pangle | 7.9.0.6.0             | 5.9.1         | Ads-Global = 7.9.0.6
        ///   Maio               | 2.1.6.0               | 3.1.6         | MaioSDK-v2 = 2.1.6 *
        ///                                                                 (*see Maio note below)
        ///   Facebook           | 6.21.1.0              | 3.18.3        | FBAudienceNetwork
        ///   IronSource         | 9.3.0.0.0             | 4.4.1         | IronSourceSDK 9.3.0.0
        ///   Chartboost         | 9.11.0.0              | 4.11.2        | ChartboostSDK 9.11.0
        ///   Vungle / LiftOff   | 7.7.1.0               | 5.7.1         | VungleAdsSDK 7.7.1
        ///   Mintegral          | 8.0.8.0.0             | 2.0.6         | MintegralAdSDK 8.0.8.0
        ///   Fyber / DT Exchange| 8.4.5.0               | 3.5.6         | Fyber_Marketplace_SDK
        ///   PubMatic           | 4.12.0.0              | 1.5.0         | OpenWrapSDK-XCFramework
        ///   BidMachine         | 3.5.1.0.0             | 1.0.2         | BidMachine 3.5.1
        ///   LINE               | 3.0.1.0               | 2.0.2         | FiveAd
        ///
        /// Networks NOT installable from both catalogs (must pick one):
        ///   • InMobi, Moloco — vendored-framework duplication (Xcode
        ///     "Multiple commands produce" error). Handled by
        ///     <c>EmbedFrameworksDeduper</c> + hard-conflict detection.
        public static readonly Dictionary<string, (string androidPkg, string androidVer, string iosPkg, string iosVer)> MaxAdapters = new()
        {
            // Tier 1
            { "Google / AdMob",        ("com.applovin.mediation.adapters.google.android",           "25010000.0.0",  "com.applovin.mediation.adapters.google.ios",           "13020000.0.0") },
            { "Google Ad Manager",     ("com.applovin.mediation.adapters.googleadmanager.android",  "25010000.0.0",  "com.applovin.mediation.adapters.googleadmanager.ios",  "13020000.0.0") },
            { "Meta Audience Network", ("com.applovin.mediation.adapters.facebook.android",         "6210000.0.0",   "com.applovin.mediation.adapters.facebook.ios",         "6210100.0.0")  },
            { "IronSource",            ("com.applovin.mediation.adapters.ironsource.android",       "904000000.0.0", "com.applovin.mediation.adapters.ironsource.ios",       "903000000.0.0")},
            { "Unity Ads",             ("com.applovin.mediation.adapters.unityads.android",         "4170000.0.0",   "com.applovin.mediation.adapters.unityads.ios",         "4170000.0.0")  },
            // Tier 2
            { "Vungle / LiftOff",      ("com.applovin.mediation.adapters.vungle.android",           "7070100.0.0",   "com.applovin.mediation.adapters.vungle.ios",           "7070100.0.0")  },
            { "Chartboost",            ("com.applovin.mediation.adapters.chartboost.android",       "9110100.0.0",   "com.applovin.mediation.adapters.chartboost.ios",       "9110000.0.0")  },
            { "InMobi",                ("com.applovin.mediation.adapters.inmobi.android",           "11020000.0.0",  "com.applovin.mediation.adapters.inmobi.ios",           "11010100.0.0") },
            { "Mintegral",             ("com.applovin.mediation.adapters.mintegral.android",        "17011100.0.0",  "com.applovin.mediation.adapters.mintegral.ios",        "800080000.0.0")},
            // ByteDance / Pangle — asymmetric versions because the two
            // platforms have INDEPENDENT adapter trajectories:
            //
            //   iOS  MAX ByteDance 7.9.0.6.0 (709000600.0.0) → Ads-Global 7.9.0.6
            //   iOS  AdMob Pangle 5.9.1                      → Ads-Global 7.9.0.6 (via GoogleMobileAdsMediationPangle 7.9.0.6.0)
            //
            //   Android MAX ByteDance 7.9.1.3.0 (709010300.0.0) — latest
            //   Android AdMob Pangle  5.9.1 — Gradle resolves independently
            //
            // iOS uses CocoaPods which exact-pin-conflicts if MAX and AdMob
            // disagree on the underlying `Ads-Global` pod version, so the two
            // catalogs MUST line up. On Android, Gradle resolves transitively
            // without exact-pin conflicts, so each catalog can stay on its
            // own latest version without causing build failures.
            //
            // When bumping iOS, verify the new MAX version pulls the same
            // `Ads-Global` pod as the current AdMob Pangle version — see
            // the adapter's iOS podspec on unity.packages.applovin.com.
            { "ByteDance / Pangle",    ("com.applovin.mediation.adapters.bytedance.android",        "709010300.0.0", "com.applovin.mediation.adapters.bytedance.ios",        "709000600.0.0")},
            { "BidMachine",            ("com.applovin.mediation.adapters.bidmachine.android",       "3060100.0.0",   "com.applovin.mediation.adapters.bidmachine.ios",       "305010000.0.0")},
            // Tier 3
            { "Yandex",                ("com.applovin.mediation.adapters.yandex.android",           "7180500.0.0",   "com.applovin.mediation.adapters.yandex.ios",           "7180400.0.0")  },
            { "Fyber / DT Exchange",   ("com.applovin.mediation.adapters.fyber.android",            "8040400.0.0",   "com.applovin.mediation.adapters.fyber.ios",            "8040500.0.0")  },
            { "Smaato",                ("com.applovin.mediation.adapters.smaato.android",           "23000100.0.0",  "com.applovin.mediation.adapters.smaato.ios",           "23000100.0.0") },
            { "Verve",                 ("com.applovin.mediation.adapters.verve.android",            "3070100.0.0",   "com.applovin.mediation.adapters.verve.ios",            "3070100.0.0")  },
            { "HyprMX",                ("com.applovin.mediation.adapters.hyprmx.android",           "6040203.0.0",   "com.applovin.mediation.adapters.hyprmx.ios",           "604020000.0.0")},
            { "LINE",                  ("com.applovin.mediation.adapters.line.android",             "300000010.0.0", "com.applovin.mediation.adapters.line.ios",             "3000100.0.0")  },
            { "Moloco",                ("com.applovin.mediation.adapters.moloco.android",           "4070000.0.0",   "com.applovin.mediation.adapters.moloco.ios",           "4040100.0.0")  },
            { "PubMatic",              ("com.applovin.mediation.adapters.pubmatic.android",         "5000000.0.0",   "com.applovin.mediation.adapters.pubmatic.ios",         "4120000.0.0")  },
            { "Ogury Presage",         ("com.applovin.mediation.adapters.ogurypresage.android",     "6020200.0.0",   "com.applovin.mediation.adapters.ogurypresage.ios",     "5020100.0.0")  },
            { "MobileFuse",            ("com.applovin.mediation.adapters.mobilefuse.android",       "1110000.0.0",   "com.applovin.mediation.adapters.mobilefuse.ios",       "1110000.0.0")  },
            { "BigO Ads",              ("com.applovin.mediation.adapters.bigoads.android",          "5070100.0.0",   "com.applovin.mediation.adapters.bigoads.ios",          "5010200.0.0")  },
            { "Maio",                  ("com.applovin.mediation.adapters.maio.android",             "2000400.0.0",   "com.applovin.mediation.adapters.maio.ios",             "2010600.0.0")  },
        };

        /// <summary>AdMob mediation-adapter UPM catalog.</summary>
        public static readonly Dictionary<string, (string pkg, string ver)> AdmobAdapters = new()
        {
            { "AppLovin",                ("com.google.ads.mobile.mediation.applovin",            "8.7.1")  },
            { "Unity Ads",               ("com.google.ads.mobile.mediation.unity",               "3.17.0") },
            { "IronSource / LevelPlay",  ("com.google.ads.mobile.mediation.ironsource",          "4.4.1")  },
            { "Chartboost",              ("com.google.ads.mobile.mediation.chartboost",          "4.11.2") },
            { "Meta Audience Network",   ("com.google.ads.mobile.mediation.metaaudiencenetwork", "3.18.3") },
            { "Liftoff / Vungle",        ("com.google.ads.mobile.mediation.liftoffmonetize",     "5.7.1")  },
            { "Pangle / ByteDance",      ("com.google.ads.mobile.mediation.pangle",              "5.9.1")  },
            { "Mintegral",               ("com.google.ads.mobile.mediation.mintegral",           "2.0.6")  },
            { "DT Exchange / Fyber",     ("com.google.ads.mobile.mediation.dtexchange",          "3.5.6")  },
            { "InMobi",                  ("com.google.ads.mobile.mediation.inmobi",              "5.0.2")  },
            { "myTarget",                ("com.google.ads.mobile.mediation.mytarget",            "3.35.0") },
            { "Moloco",                  ("com.google.ads.mobile.mediation.moloco",              "3.4.1")  },
            { "PubMatic",                ("com.google.ads.mobile.mediation.pubmatic",            "1.5.0")  },
            { "BidMachine",              ("com.google.ads.mobile.mediation.bidmachine",          "1.0.2")  },
            { "LINE",                    ("com.google.ads.mobile.mediation.line",                "2.0.2")  },
            { "Maio",                    ("com.google.ads.mobile.mediation.maio",                "3.1.6")  },
            { "i-mobile",                ("com.google.ads.mobile.mediation.imobile",             "1.3.9")  },
        };

        /// <summary>
        /// Packages the stabilizer should FORCE to the catalog version on
        /// every auto-run, regardless of the pin currently in
        /// <c>Packages/manifest.json</c>. Listed here because the upstream
        /// registries for these packages retag frequently, leaving old pins
        /// pointing at unpublished releases. Force-healing to the catalog
        /// avoids chicken-and-egg UPM resolution errors.
        /// </summary>
        public static IEnumerable<(string pkg, string catalogVer)> ForceHealTargets()
        {
            // ByteDance/Pangle iOS + Android (AppLovin unpublishes old retags
            // — e.g. iOS 709000000.0.0 was replaced by 709010100.0.0).
            if (MaxAdapters.TryGetValue("ByteDance / Pangle", out var bd))
            {
                yield return (bd.iosPkg,     bd.iosVer);
                yield return (bd.androidPkg, bd.androidVer);
            }
            // AdMob Maio: the old 3.0.1 pin pulls in GMA ~> 12.0 and breaks
            // CocoaPods when the AppLovin Google adapter (GMA = 13.2.0) is
            // also installed. 3.1.6 resolves the GMA conflict. Note: Maio is
            // mutually exclusive across catalogs (MaioSDK-v2 = 2.1.6 vs 2.2.1) —
            // users must keep only ONE Maio adapter installed regardless of
            // version. Enforced by CocoaPodsConflictFixer mutuallyExclusive=true.
            if (AdmobAdapters.TryGetValue("Maio", out var maio))
            {
                yield return (maio.pkg, maio.ver);
            }
        }
    }
}
#endif
