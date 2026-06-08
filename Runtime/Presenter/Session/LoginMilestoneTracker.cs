using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Fires login-retention milestone events (<c>login_on_d0</c> / <c>_d1</c> / <c>_d3</c> /
    /// <c>_d7</c> / <c>_d14</c> / <c>_d30</c>) exactly once each per install.
    ///
    /// Retention semantics: a milestone fires only when the user logs in <b>on</b> day N after
    /// install — i.e. <c>floor(daysSinceInstall) == N</c>. <c>login_on_d0</c> fires for a same-day
    /// install + login. A user who skips day 7 never fires <c>login_on_d7</c>. Each milestone is
    /// one-shot, guarded by a single PlayerPrefs bitmask.
    ///
    /// State is persisted in <see cref="PlayerPrefs"/>:
    /// <list type="bullet">
    ///   <item><c>noctua.login.fired</c> — int bitmask: bit0=d1 bit1=d3 bit2=d7 bit3=d14 bit4=d30 bit5=d0</item>
    /// </list>
    ///
    /// The install date is the single source of truth written by
    /// <see cref="UserSegmentManager"/> (<see cref="UserSegmentManager.InstallTicksKey"/>).
    /// Mirrors the <see cref="AdWatchMilestoneTracker"/> pattern: the single instance is created
    /// by <c>Noctua.Initialization</c> and exposed as <see cref="Default"/>.
    /// </summary>
    public class LoginMilestoneTracker
    {
        private static readonly NoctuaLogger _log = new(typeof(LoginMilestoneTracker));

        /// <summary>Stable, greppable tag prefixed to every log line from this tracker.
        /// Search the logs for <c>[login_milestone]</c> to find all related output.</summary>
        private const string LogTag = "[login_milestone]";

        private const string FiredKey = "noctua.login.fired";

        // Event names. These are not IAA events, so they live here (not in IAAEventNames).
        public const string LoginOnD0  = "login_on_d0";
        public const string LoginOnD1  = "login_on_d1";
        public const string LoginOnD3  = "login_on_d3";
        public const string LoginOnD7  = "login_on_d7";
        public const string LoginOnD14 = "login_on_d14";
        public const string LoginOnD30 = "login_on_d30";

        // Bit positions within the "fired" bitmask. Fixed forever — existing installs have these
        // bits persisted in PlayerPrefs. Bits 0-4 are the original thresholds and must not be
        // reassigned; d0 was added later, so it takes the next free bit (5).
        private const int BitD1  = 0;
        private const int BitD3  = 1;
        private const int BitD7  = 2;
        private const int BitD14 = 3;
        private const int BitD30 = 4;
        private const int BitD0  = 5;

        // Day → (bit, eventName). Retention: a milestone fires only when daysSinceInstall == Day.
        private static readonly (int Day, int Bit, string EventName)[] Milestones =
        {
            ( 0, BitD0,  LoginOnD0  ),
            ( 1, BitD1,  LoginOnD1  ),
            ( 3, BitD3,  LoginOnD3  ),
            ( 7, BitD7,  LoginOnD7  ),
            (14, BitD14, LoginOnD14 ),
            (30, BitD30, LoginOnD30 ),
        };

        private readonly Action<string, Dictionary<string, IConvertible>> _emit;
        private readonly Func<DateTime> _utcNow;

        /// <summary>
        /// Singleton accessor — set by <c>Noctua.Initialization</c>. Callers do
        /// <c>LoginMilestoneTracker.Default?.RecordLogin()</c>. Nullable until SDK init completes.
        /// </summary>
        public static LoginMilestoneTracker Default { get; private set; }

        /// <summary>
        /// Construct with an event-emit delegate. <paramref name="utcNow"/> is a test seam — pass
        /// null in production to use <see cref="DateTime.UtcNow"/>.
        /// </summary>
        public LoginMilestoneTracker(
            Action<string, Dictionary<string, IConvertible>> emit,
            Func<DateTime> utcNow = null)
        {
            _emit = emit ?? throw new ArgumentNullException(nameof(emit));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>Install this instance as the process-wide <see cref="Default"/>. Idempotent.</summary>
        public void InstallAsDefault() => Default = this;

        /// <summary>
        /// Record one login. If it lands exactly on a milestone day (1/3/7/14/30 days since
        /// install) and that milestone has not fired yet, the matching <c>login_on_dN</c> event
        /// fires once.
        /// </summary>
        public void RecordLogin()
        {
            try
            {
                long? installTicks = UserSegmentManager.GetInstallTicks();
                if (installTicks == null)
                {
                    // Install timestamp not written yet (shouldn't happen post-init, since the
                    // composition root calls UserSegmentManager.EnsureInstallTimestamp()). Without
                    // an anchor we cannot compute the day, so skip.
                    _log.Debug($"{LogTag} skipped: install timestamp not available yet");
                    return;
                }

                var installDate = new DateTime(installTicks.Value, DateTimeKind.Utc);
                int days = (int)(_utcNow() - installDate).TotalDays;

                var firedMask = PlayerPrefs.GetInt(FiredKey, 0);

                // Always log the evaluation so it's observable that RecordLogin ran and why a
                // milestone did or didn't fire. Retention semantics: a milestone fires only on the
                // exact day N (0/1/3/7/14/30). Day 0 is a same-day install+login.
                _log.Debug($"{LogTag} evaluating: days_since_install={days}, firedMask={firedMask} " +
                           $"(milestones fire only on exact day 0/1/3/7/14/30)");

                foreach (var (day, bit, eventName) in Milestones)
                {
                    if (days != day) continue;
                    if ((firedMask & (1 << bit)) != 0) continue;

                    firedMask |= 1 << bit;
                    PlayerPrefs.SetInt(FiredKey, firedMask);

                    var payload = new Dictionary<string, IConvertible> { { "day", day } };
                    _emit(eventName, payload);
                    _log.Info($"{LogTag} fired {eventName} (days_since_install={days})");
                }

                // Progress visibility: report the nearest upcoming milestone still reachable
                // (firedMask already reflects any milestone fired on this login). For exact-day
                // retention, "reachable" = a milestone day >= today that has not fired yet; earlier
                // milestone days that were skipped can never fire.
                var next = GetNextUpcomingMilestone(days, firedMask);
                if (next.HasValue)
                {
                    _log.Debug($"{LogTag} progress: days_since_install={days}, " +
                               $"next {next.Value.EventName} on day {next.Value.Day} ({days}/{next.Value.Day})");
                }
                else
                {
                    _log.Debug($"{LogTag} progress: days_since_install={days}, all reachable milestones fired or passed");
                }

                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                _log.Error($"{LogTag} RecordLogin failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the nearest still-reachable milestone: the smallest milestone day that is
        /// <c>&gt;= currentDays</c> and whose bit is not yet set in <paramref name="firedMask"/>,
        /// or <c>null</c> if none remain. <see cref="Milestones"/> is in ascending day order, so the
        /// first match is the nearest. Milestone days already passed (without firing) are skipped —
        /// under exact-day retention they can never fire.
        /// </summary>
        private static (int Day, string EventName)? GetNextUpcomingMilestone(int currentDays, int firedMask)
        {
            foreach (var (day, bit, eventName) in Milestones)
            {
                if (day < currentDays) continue;
                if ((firedMask & (1 << bit)) != 0) continue;
                return (day, eventName);
            }
            return null;
        }

        /// <summary>Test helper — current fired bitmask.</summary>
        public static int GetFiredMask() => PlayerPrefs.GetInt(FiredKey, 0);

        /// <summary>Test helper — clear all milestone state.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(FiredKey);
            PlayerPrefs.Save();
        }
    }
}
