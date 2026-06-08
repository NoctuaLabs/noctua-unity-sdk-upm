using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="LoginMilestoneTracker"/>.
    ///
    /// Covers:
    ///   — Constructor rejects null emit delegate
    ///   — Retention semantics: fires only when login lands exactly on day N since install
    ///   — Does not fire on non-milestone days
    ///   — Each milestone fires at most once (bitmask dedup), even across instances
    ///   — Missing install timestamp → no fire
    ///   — <c>InstallAsDefault</c> sets the static <c>Default</c> property
    ///
    /// State is cleared before and after each test via <c>LoginMilestoneTracker.Reset</c> and the
    /// install timestamp PlayerPrefs key.
    /// </summary>
    [TestFixture]
    public class LoginMilestoneTrackerTest
    {
        private List<string> _emittedEvents;

        // A fixed, deterministic install instant (UTC) used as day 0 for the clock seam.
        private static readonly DateTime InstallInstant = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp()
        {
            _emittedEvents = new List<string>();
            LoginMilestoneTracker.Reset();
            PlayerPrefs.DeleteKey(UserSegmentManager.InstallTicksKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            LoginMilestoneTracker.Reset();
            PlayerPrefs.DeleteKey(UserSegmentManager.InstallTicksKey);
            PlayerPrefs.Save();
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private void SeedInstall(DateTime installUtc)
        {
            PlayerPrefs.SetString(UserSegmentManager.InstallTicksKey, installUtc.Ticks.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>Tracker whose "now" is install + <paramref name="daysSinceInstall"/> days
        /// (plus 2h so we sit safely inside that day rather than on the boundary).</summary>
        private LoginMilestoneTracker TrackerOnDay(int daysSinceInstall)
        {
            DateTime now = InstallInstant.AddDays(daysSinceInstall).AddHours(2);
            return new LoginMilestoneTracker((name, _) => _emittedEvents.Add(name), () => now);
        }

        // ── tests ────────────────────────────────────────────────────────────

        [Test]
        public void Constructor_NullEmit_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LoginMilestoneTracker(null));
        }

        [Test]
        public void RecordLogin_OnDay1_FiresLoginOnD1Only()
        {
            SeedInstall(InstallInstant);
            TrackerOnDay(1).RecordLogin();

            Assert.Contains(LoginMilestoneTracker.LoginOnD1, _emittedEvents);
            Assert.AreEqual(1, _emittedEvents.Count, "Only the d1 milestone should fire on day 1");
        }

        [Test]
        public void RecordLogin_OnDay7_FiresLoginOnD7Only()
        {
            SeedInstall(InstallInstant);
            TrackerOnDay(7).RecordLogin();

            Assert.Contains(LoginMilestoneTracker.LoginOnD7, _emittedEvents);
            Assert.IsFalse(_emittedEvents.Contains(LoginMilestoneTracker.LoginOnD1),
                "d1 must not fire retroactively on day 7 (retention, not threshold)");
            Assert.AreEqual(1, _emittedEvents.Count);
        }

        [Test]
        public void RecordLogin_OnDay30_FiresLoginOnD30()
        {
            SeedInstall(InstallInstant);
            TrackerOnDay(30).RecordLogin();

            Assert.Contains(LoginMilestoneTracker.LoginOnD30, _emittedEvents);
            Assert.AreEqual(1, _emittedEvents.Count);
        }

        [Test]
        public void RecordLogin_OnNonMilestoneDay_FiresNothing()
        {
            SeedInstall(InstallInstant);
            TrackerOnDay(6).RecordLogin(); // 6 is not in {1,3,7,14,30}

            Assert.AreEqual(0, _emittedEvents.Count, "No milestone should fire on a non-milestone day");
        }

        [Test]
        public void RecordLogin_OnDay0_FiresLoginOnD0()
        {
            SeedInstall(InstallInstant);
            TrackerOnDay(0).RecordLogin(); // same-day install + login

            Assert.Contains(LoginMilestoneTracker.LoginOnD0, _emittedEvents);
            Assert.AreEqual(1, _emittedEvents.Count, "Only the d0 milestone should fire on day 0");
        }

        [Test]
        public void RecordLogin_SameDayTwice_FiresOnce()
        {
            SeedInstall(InstallInstant);
            var tracker = TrackerOnDay(3);

            tracker.RecordLogin();
            tracker.RecordLogin();

            Assert.AreEqual(1, _emittedEvents.FindAll(e => e == LoginMilestoneTracker.LoginOnD3).Count,
                "d3 milestone must fire at most once even with multiple logins on day 3");
        }

        [Test]
        public void RecordLogin_FiredMaskPersists_PreventsRefiringAcrossInstances()
        {
            SeedInstall(InstallInstant);

            TrackerOnDay(14).RecordLogin();
            Assert.Contains(LoginMilestoneTracker.LoginOnD14, _emittedEvents);

            // New instance, same day 14 — must not re-fire (bitmask persisted in PlayerPrefs).
            var events2 = new List<string>();
            DateTime now = InstallInstant.AddDays(14).AddHours(5);
            new LoginMilestoneTracker((name, _) => events2.Add(name), () => now).RecordLogin();

            Assert.IsFalse(events2.Contains(LoginMilestoneTracker.LoginOnD14),
                "d14 must not re-fire when its bit is already set in PlayerPrefs");
        }

        [Test]
        public void RecordLogin_NoInstallTimestamp_FiresNothing()
        {
            // Do not seed the install key.
            TrackerOnDay(7).RecordLogin();

            Assert.AreEqual(0, _emittedEvents.Count,
                "Without an install timestamp, no milestone can be evaluated");
        }

        [Test]
        public void RecordLogin_PayloadContainsDay()
        {
            SeedInstall(InstallInstant);
            var captured = new Dictionary<string, Dictionary<string, IConvertible>>();
            new LoginMilestoneTracker(
                (name, payload) => captured[name] = payload,
                () => InstallInstant.AddDays(7).AddHours(1)
            ).RecordLogin();

            Assert.IsTrue(captured.ContainsKey(LoginMilestoneTracker.LoginOnD7));
            Assert.AreEqual(7, Convert.ToInt32(captured[LoginMilestoneTracker.LoginOnD7]["day"]));
        }

        [Test]
        public void InstallAsDefault_SetsDotDefaultProperty()
        {
            var tracker = new LoginMilestoneTracker((name, _) => _emittedEvents.Add(name));
            tracker.InstallAsDefault();
            Assert.AreSame(tracker, LoginMilestoneTracker.Default);
        }
    }
}
