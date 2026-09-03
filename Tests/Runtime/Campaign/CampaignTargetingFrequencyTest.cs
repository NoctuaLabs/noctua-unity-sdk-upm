using System;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk.LiveOpsCampaign;
using NUnit.Framework;

namespace Tests.Runtime.Campaign
{
    [TestFixture]
    public class CampaignTargetingFrequencyTest
    {
        // ---- CampaignConfig.MergeWith -----------------------------------

        [Test]
        public void Merge_NullRemote_ReturnsSelf()
        {
            var local = new CampaignConfig { SchemaVersion = 1 };
            Assert.AreSame(local, local.MergeWith(null));
        }

        [Test]
        public void Merge_UnionsById_RemoteWins_HigherSchemaWins()
        {
            var local = new CampaignConfig
            {
                SchemaVersion = 1,
                Campaigns = new List<CampaignItem>
                {
                    new CampaignItem { Id = "a", Priority = 1 },
                    new CampaignItem { Id = "b", Priority = 1 },
                },
            };
            var remote = new CampaignConfig
            {
                SchemaVersion = 3,
                Campaigns = new List<CampaignItem>
                {
                    new CampaignItem { Id = "b", Priority = 99 },
                    new CampaignItem { Id = "c", Priority = 5 },
                },
            };

            var merged = local.MergeWith(remote);

            Assert.AreEqual(3, merged.SchemaVersion);
            Assert.AreEqual(3, merged.Campaigns.Count);
            Assert.AreEqual(99, merged.Campaigns.First(c => c.Id == "b").Priority); // remote won
            Assert.IsTrue(merged.Campaigns.Any(c => c.Id == "a"));
            Assert.IsTrue(merged.Campaigns.Any(c => c.Id == "c"));
        }

        // ---- version compare -------------------------------------------

        [Test]
        public void CompareVersions_NumericSegments()
        {
            Assert.Less(CampaignManager.CompareVersions("1.2.0", "1.10.0"), 0);
            Assert.Greater(CampaignManager.CompareVersions("2.0", "1.9.9"), 0);
            Assert.AreEqual(0, CampaignManager.CompareVersions("1.0.0", "1.0"));
        }

        // ---- targeting -----------------------------------------------

        private static CampaignManager ManagerWith(CampaignItem item, FakeEnv env)
        {
            var config = new CampaignConfig
            {
                SchemaVersion = 1,
                Campaigns = new List<CampaignItem> { item },
            };
            return new CampaignManager(config, env, new CampaignFrequencyGate(
                utcNow: () => env.Now, prefs: new FakePrefsStore()));
        }

        private static CampaignItem PopupWithTargeting(CampaignTargeting t)
        {
            var item = CampaignFactory.Item("c", CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeContainer));
            item.Targeting = t;
            return item;
        }

        [Test]
        public void Targeting_TagMiss_NotEligible()
        {
            var env = new FakeEnv { Tags = { "whale" } };
            var mgr = ManagerWith(PopupWithTargeting(new CampaignTargeting { Tags = new List<string> { "newbie" } }), env);

            Assert.AreEqual(0, mgr.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);
            StringAssert.Contains("tag", mgr.LastResolutions[0].Reason);
        }

        [Test]
        public void Targeting_TagHit_Eligible()
        {
            var env = new FakeEnv { Tags = { "newbie", "d0" } };
            var mgr = ManagerWith(PopupWithTargeting(new CampaignTargeting { Tags = new List<string> { "newbie" } }), env);

            Assert.AreEqual(1, mgr.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);
        }

        [Test]
        public void Targeting_CountryMiss_NotEligible()
        {
            var env = new FakeEnv { CountryCode = "VN" };
            var mgr = ManagerWith(PopupWithTargeting(new CampaignTargeting { Countries = new List<string> { "ID" } }), env);

            Assert.AreEqual(0, mgr.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);
            StringAssert.Contains("country", mgr.LastResolutions[0].Reason);
        }

        [Test]
        public void Targeting_AppVersionRange()
        {
            var env = new FakeEnv { Version = "1.5.0" };

            var below = ManagerWith(PopupWithTargeting(new CampaignTargeting { MinAppVersion = "2.0.0" }), env);
            Assert.AreEqual(0, below.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);

            var above = ManagerWith(PopupWithTargeting(new CampaignTargeting { MaxAppVersion = "1.0.0" }), env);
            Assert.AreEqual(0, above.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);

            var within = ManagerWith(PopupWithTargeting(new CampaignTargeting { MinAppVersion = "1.0.0", MaxAppVersion = "2.0.0" }), env);
            Assert.AreEqual(1, within.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);
        }

        [Test]
        public void Schedule_Window_Enforced()
        {
            var env = new FakeEnv { Now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc) };

            var item = CampaignFactory.Item("c", CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeContainer));
            item.Schedule = new CampaignSchedule { Start = "2026-07-01T00:00:00Z", End = "2026-08-01T00:00:00Z" };

            var mgr = ManagerWith(item, env);
            Assert.AreEqual(0, mgr.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);

            env.Now = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(1, mgr.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);
        }

        // ---- frequency gate ----------------------------------------

        private static CampaignItem FreqItem(CampaignFrequency f)
        {
            var item = CampaignFactory.Item("freq", CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeContainer));
            item.Frequency = f;
            return item;
        }

        [Test]
        public void Frequency_MaxPerDay_Blocks_AfterLimit()
        {
            var now = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
            var gate = new CampaignFrequencyGate(utcNow: () => now, prefs: new FakePrefsStore());
            var item = FreqItem(new CampaignFrequency { MaxPerDay = 2 });

            Assert.IsTrue(gate.CanShow(item));
            gate.RecordShow(item);
            Assert.IsTrue(gate.CanShow(item));
            gate.RecordShow(item);
            Assert.IsFalse(gate.CanShow(item));
        }

        [Test]
        public void Frequency_Cooldown_Blocks_WithinWindow()
        {
            var now = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
            var gate = new CampaignFrequencyGate(utcNow: () => now, prefs: new FakePrefsStore());
            var item = FreqItem(new CampaignFrequency { CooldownHours = 6 });

            gate.RecordShow(item);
            Assert.IsFalse(gate.CanShow(item));

            now = now.AddHours(7);
            Assert.IsTrue(gate.CanShow(item));
        }

        [Test]
        public void Frequency_OnceEver_BlocksForever()
        {
            var now = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
            var prefs = new FakePrefsStore();
            var gate = new CampaignFrequencyGate(utcNow: () => now, prefs: prefs);
            var item = FreqItem(new CampaignFrequency { OnceEver = true });

            Assert.IsTrue(gate.CanShow(item));
            gate.RecordShow(item);
            Assert.IsFalse(gate.CanShow(item));

            // A fresh gate over the same prefs still blocks (marker persisted).
            var gate2 = new CampaignFrequencyGate(utcNow: () => now.AddDays(30), prefs: prefs);
            Assert.IsFalse(gate2.CanShow(item));
        }

        [Test]
        public void Frequency_NoConfig_AlwaysAllowed()
        {
            var gate = new CampaignFrequencyGate(prefs: new FakePrefsStore());
            var item = CampaignFactory.Item("x", CampaignItem.EngagementPurchase, null);
            Assert.IsTrue(gate.CanShow(item));
        }

        // ---- offline asset gate --------------------------------------

        private static CampaignManager OfflineManager(bool isOffline, bool assetsReady)
        {
            var item = CampaignFactory.Item("c", CampaignItem.EngagementPurchase,
                CampaignFactory.Node(CampaignNode.TypeImage,
                    new Dictionary<string, object> { { "url", "https://cdn/x.png" } }));
            var config = new CampaignConfig { SchemaVersion = 1, Campaigns = new List<CampaignItem> { item } };
            return new CampaignManager(config, new FakeEnv(),
                new CampaignFrequencyGate(prefs: new FakePrefsStore()),
                isOffline: () => isOffline,
                assetsReady: _ => assetsReady);
        }

        [Test]
        public void Offline_WithUncachedImage_NotEligible()
        {
            var mgr = OfflineManager(isOffline: true, assetsReady: false);
            Assert.AreEqual(0, mgr.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);
            StringAssert.Contains("offline", mgr.LastResolutions[0].Reason);
        }

        [Test]
        public void Offline_WithCachedImage_Eligible()
        {
            var mgr = OfflineManager(isOffline: true, assetsReady: true);
            Assert.AreEqual(1, mgr.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);
        }

        [Test]
        public void Online_AssetsNotChecked()
        {
            var mgr = OfflineManager(isOffline: false, assetsReady: false);
            Assert.AreEqual(1, mgr.GetActiveCampaigns(CampaignItem.EngagementPurchase).Count);
        }
    }
}
