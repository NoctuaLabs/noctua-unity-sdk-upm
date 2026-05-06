using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for the PlayerPrefs helpers on the <see cref="Noctua"/> partial class
    /// (<c>Runtime/View/App/Noctua.PlayerPrefs.cs</c>).
    ///
    /// Covers:
    ///   — <c>GetPlayerPrefsKeys</c>  — non-empty, contains known integer and string keys
    ///   — <c>BackupPlayerPrefs</c>   — default values, ":int"/":string" suffixes, set values
    ///   — <c>RestorePlayerPrefs</c>  — integer restore, string restore, invalid int skipped
    ///   — Round-trip: Backup → clear prefs → Restore → verify values match
    ///
    /// PlayerPrefs state is cleared before and after every test.
    /// </summary>
    [TestFixture]
    public class NoctuaPlayerPrefsTest
    {
        // Known keys from the implementation
        private static readonly string[] KnownIntKeys =
        {
            "NoctuaFirstOpen",
            "NoctuaFirstPurchase",
            "NoctuaAccountContainer.UseFallback",
            "NativeGalleryPermission",
        };

        private static readonly string[] KnownStringKeys =
        {
            "NoctuaAccessToken",
            "NoctuaEvents",
            "NoctuaAccountContainer",
            "NoctuaPendingPurchases",
        };

        [SetUp]
        public void SetUp() => PlayerPrefs.DeleteAll();

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteAll();

        // ═══════════════════════════════════════════════════════════════════
        // GetPlayerPrefsKeys
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void GetPlayerPrefsKeys_ReturnsNonEmptyArray()
        {
            var keys = Noctua.GetPlayerPrefsKeys();

            Assert.IsNotNull(keys);
            Assert.Greater(keys.Length, 0, "GetPlayerPrefsKeys must return at least one key");
        }

        [Test]
        public void GetPlayerPrefsKeys_ContainsKnownIntegerKeys()
        {
            var keys = Noctua.GetPlayerPrefsKeys();

            foreach (var k in KnownIntKeys)
            {
                CollectionAssert.Contains(keys, k, $"Key '{k}' must be present in GetPlayerPrefsKeys");
            }
        }

        [Test]
        public void GetPlayerPrefsKeys_ContainsKnownStringKeys()
        {
            var keys = Noctua.GetPlayerPrefsKeys();

            foreach (var k in KnownStringKeys)
            {
                CollectionAssert.Contains(keys, k, $"Key '{k}' must be present in GetPlayerPrefsKeys");
            }
        }

        [Test]
        public void GetPlayerPrefsKeys_HasNoDuplicates()
        {
            var keys = Noctua.GetPlayerPrefsKeys();
            var distinct = keys.Distinct().ToArray();

            Assert.AreEqual(keys.Length, distinct.Length,
                "GetPlayerPrefsKeys must not return duplicate keys");
        }

        // ═══════════════════════════════════════════════════════════════════
        // BackupPlayerPrefs
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void BackupPlayerPrefs_ReturnsNonNullArray()
        {
            var result = Noctua.BackupPlayerPrefs();

            Assert.IsNotNull(result);
        }

        [Test]
        public void BackupPlayerPrefs_AllIntegerKeysHaveIntSuffix()
        {
            var result = Noctua.BackupPlayerPrefs();

            foreach (var k in KnownIntKeys)
            {
                var entry = result.FirstOrDefault(kv => kv.Key == $"{k}:int");
                Assert.IsFalse(entry.Equals(default(KeyValuePair<string, string>)),
                    $"Backup must include '{k}:int' entry");
            }
        }

        [Test]
        public void BackupPlayerPrefs_AllStringKeysHaveStringSuffix()
        {
            var result = Noctua.BackupPlayerPrefs();

            foreach (var k in KnownStringKeys)
            {
                var entry = result.FirstOrDefault(kv => kv.Key == $"{k}:string");
                Assert.IsFalse(entry.Equals(default(KeyValuePair<string, string>)),
                    $"Backup must include '{k}:string' entry");
            }
        }

        [Test]
        public void BackupPlayerPrefs_IntegerKey_DefaultValueIsZero()
        {
            // PlayerPrefs cleared in SetUp → default int value is 0
            var result = Noctua.BackupPlayerPrefs();

            var entry = result.First(kv => kv.Key == "NoctuaFirstOpen:int");
            Assert.AreEqual("0", entry.Value,
                "Default int value must be '0' when no prefs are set");
        }

        [Test]
        public void BackupPlayerPrefs_StringKey_DefaultValueIsEmpty()
        {
            var result = Noctua.BackupPlayerPrefs();

            var entry = result.First(kv => kv.Key == "NoctuaAccessToken:string");
            Assert.AreEqual(string.Empty, entry.Value,
                "Default string value must be empty when no prefs are set");
        }

        [Test]
        public void BackupPlayerPrefs_SetIntegerValue_BackedUpCorrectly()
        {
            PlayerPrefs.SetInt("NoctuaFirstOpen", 42);
            PlayerPrefs.Save();

            var result = Noctua.BackupPlayerPrefs();

            var entry = result.First(kv => kv.Key == "NoctuaFirstOpen:int");
            Assert.AreEqual("42", entry.Value);
        }

        [Test]
        public void BackupPlayerPrefs_SetStringValue_BackedUpCorrectly()
        {
            PlayerPrefs.SetString("NoctuaAccessToken", "my-token-xyz");
            PlayerPrefs.Save();

            var result = Noctua.BackupPlayerPrefs();

            var entry = result.First(kv => kv.Key == "NoctuaAccessToken:string");
            Assert.AreEqual("my-token-xyz", entry.Value);
        }

        [Test]
        public void BackupPlayerPrefs_NoKeyWithoutTypeSuffix()
        {
            var result = Noctua.BackupPlayerPrefs();

            foreach (var kv in result)
            {
                Assert.IsTrue(
                    kv.Key.EndsWith(":int") || kv.Key.EndsWith(":string"),
                    $"Every backup key must end with ':int' or ':string', but got '{kv.Key}'");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // RestorePlayerPrefs
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void RestorePlayerPrefs_IntegerKey_WrittenToPrefs()
        {
            var data = new[]
            {
                new KeyValuePair<string, string>("NoctuaFirstOpen:int", "7")
            };

            Noctua.RestorePlayerPrefs(data);

            Assert.AreEqual(7, PlayerPrefs.GetInt("NoctuaFirstOpen", 0));
        }

        [Test]
        public void RestorePlayerPrefs_StringKey_WrittenToPrefs()
        {
            var data = new[]
            {
                new KeyValuePair<string, string>("NoctuaAccessToken:string", "restored-token")
            };

            Noctua.RestorePlayerPrefs(data);

            Assert.AreEqual("restored-token", PlayerPrefs.GetString("NoctuaAccessToken", ""));
        }

        [Test]
        public void RestorePlayerPrefs_EmptyArray_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Noctua.RestorePlayerPrefs(new KeyValuePair<string, string>[0]));
        }

        [Test]
        public void RestorePlayerPrefs_InvalidIntValue_KeyNotWritten()
        {
            // Pre-set a known value so we can detect if it gets overwritten
            PlayerPrefs.SetInt("NoctuaFirstOpen", 99);
            PlayerPrefs.Save();

            var data = new[]
            {
                new KeyValuePair<string, string>("NoctuaFirstOpen:int", "not-a-number")
            };

            Noctua.RestorePlayerPrefs(data);

            // Should stay at 99 — the bad value must be skipped
            Assert.AreEqual(99, PlayerPrefs.GetInt("NoctuaFirstOpen", 0),
                "Invalid int value must be skipped; existing prefs value must be preserved");
        }

        [Test]
        public void RestorePlayerPrefs_MultipleKeys_AllRestored()
        {
            var data = new[]
            {
                new KeyValuePair<string, string>("NoctuaFirstOpen:int",     "3"),
                new KeyValuePair<string, string>("NoctuaFirstPurchase:int", "1"),
                new KeyValuePair<string, string>("NoctuaAccessToken:string", "tok"),
            };

            Noctua.RestorePlayerPrefs(data);

            Assert.AreEqual(3,     PlayerPrefs.GetInt("NoctuaFirstOpen",    0));
            Assert.AreEqual(1,     PlayerPrefs.GetInt("NoctuaFirstPurchase", 0));
            Assert.AreEqual("tok", PlayerPrefs.GetString("NoctuaAccessToken", ""));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Round-trip: Backup → clear → Restore → verify
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void RoundTrip_IntegerValue_PreservedAfterBackupAndRestore()
        {
            PlayerPrefs.SetInt("NoctuaFirstOpen", 5);
            PlayerPrefs.Save();

            var backup = Noctua.BackupPlayerPrefs();

            PlayerPrefs.DeleteAll();
            Assert.AreEqual(0, PlayerPrefs.GetInt("NoctuaFirstOpen", 0), "Prefs must be cleared");

            Noctua.RestorePlayerPrefs(backup);

            Assert.AreEqual(5, PlayerPrefs.GetInt("NoctuaFirstOpen", 0),
                "Integer value must survive a backup→clear→restore round-trip");
        }

        [Test]
        public void RoundTrip_StringValue_PreservedAfterBackupAndRestore()
        {
            PlayerPrefs.SetString("NoctuaAccessToken", "round-trip-token");
            PlayerPrefs.Save();

            var backup = Noctua.BackupPlayerPrefs();

            PlayerPrefs.DeleteAll();
            Assert.AreEqual("", PlayerPrefs.GetString("NoctuaAccessToken", ""), "Prefs must be cleared");

            Noctua.RestorePlayerPrefs(backup);

            Assert.AreEqual("round-trip-token", PlayerPrefs.GetString("NoctuaAccessToken", ""),
                "String value must survive a backup→clear→restore round-trip");
        }
    }
}
