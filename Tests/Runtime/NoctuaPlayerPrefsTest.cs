using System;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for the <c>Noctua.PlayerPrefs</c> partial class:
    ///   * <see cref="Noctua.GetPlayerPrefsKeys"/>  — returns the full list of Noctua-owned keys
    ///   * <see cref="Noctua.BackupPlayerPrefs"/>   — serialises PlayerPrefs to KV pairs
    ///   * <see cref="Noctua.RestorePlayerPrefs"/>  — deserialises KV pairs back to PlayerPrefs
    ///
    /// None of these methods access the <c>Noctua.Instance</c> singleton — they are
    /// pure PlayerPrefs operations and are safe to call in EditMode without a config file.
    /// </summary>
    [TestFixture]
    public class NoctuaPlayerPrefsTest
    {
        // Keys read/written by BackupPlayerPrefs (int-typed)
        private static readonly string[] IntKeys = new[]
        {
            "NoctuaFirstOpen",
            "NoctuaFirstPurchase",
            "NoctuaAccountContainer.UseFallback",
            "NativeGalleryPermission",
        };

        // Keys read/written by BackupPlayerPrefs (string-typed)
        private static readonly string[] StringKeys = new[]
        {
            "NoctuaWebContent.Announcement.LastShown",
            "NoctuaAccountContainer",
            "NoctuaPendingPurchases",
            "NoctuaLocaleCountry",
            "NoctuaLocaleCurrency",
            "NoctuaLocaleUserPrefsLanguage",
            "NoctuaUnpairedOrders",
            "NoctuaPurchaseHistory",
            "NoctuaEvents",
            "NoctuaAccessToken",
            "NoctuaCurrentStageLevel",
            "NoctuaCurrentStageMode",
            "NoctuaOrphanedSessionId",
            "NoctuaOrphanedSessionCumulativeMs",
            "NoctuaOrphanedSessionLastTimestamp",
        };

        [SetUp]
        public void ClearPrefs()
        {
            // Delete all Noctua keys before each test to ensure a clean slate
            foreach (var k in IntKeys)    PlayerPrefs.DeleteKey(k);
            foreach (var k in StringKeys) PlayerPrefs.DeleteKey(k);
        }

        [TearDown]
        public void RestorePrefs()
        {
            // Clean up after each test
            foreach (var k in IntKeys)    PlayerPrefs.DeleteKey(k);
            foreach (var k in StringKeys) PlayerPrefs.DeleteKey(k);
        }

        // ─── GetPlayerPrefsKeys ───────────────────────────────────────────────

        [Test]
        public void GetPlayerPrefsKeys_ReturnsNonNullArray()
        {
            var keys = Noctua.GetPlayerPrefsKeys();
            Assert.IsNotNull(keys);
        }

        [Test]
        public void GetPlayerPrefsKeys_ContainsAllIntKeys()
        {
            var keys = Noctua.GetPlayerPrefsKeys();
            foreach (var expected in IntKeys)
                Assert.Contains(expected, keys, $"Missing int key: {expected}");
        }

        [Test]
        public void GetPlayerPrefsKeys_ContainsAllStringKeys()
        {
            var keys = Noctua.GetPlayerPrefsKeys();
            foreach (var expected in StringKeys)
                Assert.Contains(expected, keys, $"Missing string key: {expected}");
        }

        [Test]
        public void GetPlayerPrefsKeys_CountMatchesKnownTotal()
        {
            var keys = Noctua.GetPlayerPrefsKeys();
            int expectedCount = IntKeys.Length + StringKeys.Length;
            Assert.AreEqual(expectedCount, keys.Length,
                $"Expected {expectedCount} keys, got {keys.Length}");
        }

        [Test]
        public void GetPlayerPrefsKeys_NoDuplicates()
        {
            var keys = Noctua.GetPlayerPrefsKeys();
            var distinct = keys.Distinct().Count();
            Assert.AreEqual(keys.Length, distinct, "GetPlayerPrefsKeys should not contain duplicates");
        }

        // ─── BackupPlayerPrefs — default state ────────────────────────────────

        [Test]
        public void BackupPlayerPrefs_ReturnsNonNullArray()
        {
            var backup = Noctua.BackupPlayerPrefs();
            Assert.IsNotNull(backup);
        }

        [Test]
        public void BackupPlayerPrefs_CountEqualsIntPlusStringKeys()
        {
            var backup = Noctua.BackupPlayerPrefs();
            int expected = IntKeys.Length + StringKeys.Length;
            Assert.AreEqual(expected, backup.Length,
                $"Expected {expected} KV pairs from backup, got {backup.Length}");
        }

        [Test]
        public void BackupPlayerPrefs_IntKeys_HaveIntSuffix()
        {
            var backup = Noctua.BackupPlayerPrefs();
            foreach (var intKey in IntKeys)
            {
                var pair = backup.FirstOrDefault(kv => kv.Key.StartsWith(intKey + ":"));
                Assert.AreNotEqual(default(KeyValuePair<string, string>), pair,
                    $"Expected backup entry for int key '{intKey}'");
                StringAssert.EndsWith(":int", pair.Key,
                    $"Int key '{intKey}' should be backed up with ':int' suffix");
            }
        }

        [Test]
        public void BackupPlayerPrefs_StringKeys_HaveStringSuffix()
        {
            var backup = Noctua.BackupPlayerPrefs();
            foreach (var strKey in StringKeys)
            {
                var pair = backup.FirstOrDefault(kv => kv.Key.StartsWith(strKey + ":"));
                Assert.AreNotEqual(default(KeyValuePair<string, string>), pair,
                    $"Expected backup entry for string key '{strKey}'");
                StringAssert.EndsWith(":string", pair.Key,
                    $"String key '{strKey}' should be backed up with ':string' suffix");
            }
        }

        [Test]
        public void BackupPlayerPrefs_DefaultIntValues_AreZero()
        {
            // No int prefs set — defaults should all be "0"
            var backup = Noctua.BackupPlayerPrefs();
            foreach (var intKey in IntKeys)
            {
                var pair = backup.First(kv => kv.Key == $"{intKey}:int");
                Assert.AreEqual("0", pair.Value,
                    $"Default int value for '{intKey}' should be '0'");
            }
        }

        [Test]
        public void BackupPlayerPrefs_DefaultStringValues_AreEmpty()
        {
            // No string prefs set — defaults should all be ""
            var backup = Noctua.BackupPlayerPrefs();
            foreach (var strKey in StringKeys)
            {
                var pair = backup.First(kv => kv.Key == $"{strKey}:string");
                Assert.AreEqual("", pair.Value,
                    $"Default string value for '{strKey}' should be empty");
            }
        }

        [Test]
        public void BackupPlayerPrefs_SetIntPref_ReflectedInBackup()
        {
            const string key = "NoctuaFirstOpen";
            PlayerPrefs.SetInt(key, 42);

            var backup = Noctua.BackupPlayerPrefs();
            var pair = backup.First(kv => kv.Key == $"{key}:int");
            Assert.AreEqual("42", pair.Value,
                $"Backup should reflect PlayerPrefs.SetInt({key}, 42)");
        }

        [Test]
        public void BackupPlayerPrefs_SetStringPref_ReflectedInBackup()
        {
            const string key = "NoctuaAccessToken";
            PlayerPrefs.SetString(key, "tok-abc");

            var backup = Noctua.BackupPlayerPrefs();
            var pair = backup.First(kv => kv.Key == $"{key}:string");
            Assert.AreEqual("tok-abc", pair.Value,
                $"Backup should reflect PlayerPrefs.SetString({key}, 'tok-abc')");
        }

        // ─── RestorePlayerPrefs ───────────────────────────────────────────────

        [Test]
        public void RestorePlayerPrefs_EmptyArray_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Noctua.RestorePlayerPrefs(Array.Empty<KeyValuePair<string, string>>()));
        }

        [Test]
        public void RestorePlayerPrefs_IntPair_SetsIntPref()
        {
            var pairs = new[] { new KeyValuePair<string, string>("NoctuaFirstOpen:int", "7") };

            Noctua.RestorePlayerPrefs(pairs);

            Assert.AreEqual(7, PlayerPrefs.GetInt("NoctuaFirstOpen", 0));
        }

        [Test]
        public void RestorePlayerPrefs_StringPair_SetsStringPref()
        {
            var pairs = new[] { new KeyValuePair<string, string>("NoctuaAccessToken:string", "restored-token") };

            Noctua.RestorePlayerPrefs(pairs);

            Assert.AreEqual("restored-token", PlayerPrefs.GetString("NoctuaAccessToken", ""));
        }

        [Test]
        public void RestorePlayerPrefs_MultipleIntPairs_AllRestored()
        {
            var pairs = new[]
            {
                new KeyValuePair<string, string>("NoctuaFirstOpen:int", "1"),
                new KeyValuePair<string, string>("NoctuaFirstPurchase:int", "2"),
            };

            Noctua.RestorePlayerPrefs(pairs);

            Assert.AreEqual(1, PlayerPrefs.GetInt("NoctuaFirstOpen",    0));
            Assert.AreEqual(2, PlayerPrefs.GetInt("NoctuaFirstPurchase", 0));
        }

        [Test]
        public void RestorePlayerPrefs_NonIntValueForIntKey_SkipsKey()
        {
            // "not_a_number" fails int.TryParse — the key should not be set
            const string key = "NoctuaFirstOpen";
            PlayerPrefs.DeleteKey(key);
            var pairs = new[] { new KeyValuePair<string, string>($"{key}:int", "not_a_number") };

            Noctua.RestorePlayerPrefs(pairs);

            Assert.AreEqual(0, PlayerPrefs.GetInt(key, 0),
                "Non-parseable int value must not overwrite the key");
        }

        // ─── Round-trip: Backup → clear → Restore ────────────────────────────

        [Test]
        public void BackupAndRestore_RoundTrip_PreservesIntValues()
        {
            // Set some values
            PlayerPrefs.SetInt("NoctuaFirstOpen",    99);
            PlayerPrefs.SetInt("NoctuaFirstPurchase", 3);

            // Backup
            var backup = Noctua.BackupPlayerPrefs();

            // Clear
            PlayerPrefs.DeleteKey("NoctuaFirstOpen");
            PlayerPrefs.DeleteKey("NoctuaFirstPurchase");

            // Restore
            Noctua.RestorePlayerPrefs(backup);

            // Verify
            Assert.AreEqual(99, PlayerPrefs.GetInt("NoctuaFirstOpen",    -1));
            Assert.AreEqual(3,  PlayerPrefs.GetInt("NoctuaFirstPurchase", -1));
        }

        [Test]
        public void BackupAndRestore_RoundTrip_PreservesStringValues()
        {
            const string tokenKey = "NoctuaAccessToken";
            const string tokenVal = "round-trip-token-xyz";

            PlayerPrefs.SetString(tokenKey, tokenVal);

            var backup = Noctua.BackupPlayerPrefs();
            PlayerPrefs.DeleteKey(tokenKey);

            Noctua.RestorePlayerPrefs(backup);

            Assert.AreEqual(tokenVal, PlayerPrefs.GetString(tokenKey, ""),
                "Restore must recover the original string value");
        }
    }
}
