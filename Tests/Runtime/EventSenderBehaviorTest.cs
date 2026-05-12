using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    /// <summary>
    /// Focused unit / integration tests for <see cref="EventSender"/> behaviours that are
    /// not covered by the broader <see cref="EventTest"/> suite:
    ///
    /// Group A — Constructor validation (5 tests)
    ///   ArgumentNullException / ArgumentException for missing required config fields.
    ///
    /// Group B — PseudoUserId persistence (4 tests)
    ///   GetOrCreatePersistentDeviceId stores a UUID on first run and returns the
    ///   same value on subsequent runs (PlayerPrefs-backed across instances).
    ///
    /// Group C — SetProperties zero-sentinel semantics (6 tests)
    ///   Passing the sentinel value (0 / "" / null for isSandbox) leaves the field
    ///   unchanged; non-sentinel values update it.
    ///
    /// Group D — Flush and Dispose smoke tests (3 tests)
    ///   Flush() and Dispose() do not throw on an empty queue or after already called.
    ///
    /// Group E — Pre-persist path: first-launch events survive a simulated kill (1 test)
    ///   Verifies that events sent before _firebaseIdsFetched = true are written to
    ///   storage (jsonl file) even when the HTTP server is unreachable.
    /// </summary>
    [TestFixture]
    public class EventSenderBehaviorTest
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        private const string PseudoUserIdKey = "NoctuaPersistentDeviceId";

        /// <summary>
        /// Creates a minimal EventSender that points at a non-existent local port so
        /// no real HTTP traffic is generated. The instance must be Disposed() after use.
        /// </summary>
        private static EventSender MakeMinimalSender(bool offline = true)
        {
            return new EventSender(
                new EventSenderConfig
                {
                    BaseUrl   = "http://localhost:19881/api/v1", // nothing listening here
                    ClientId  = "unit_test_client",
                    BundleId  = "com.test.behavior",
                    CycleDelay = 60_000,                         // suppress background HTTP
                    NativePlugin = new DefaultNativePlugin(),
                    IsOfflineModeFunc = () => offline,
                },
                new NoctuaLocale()
            );
        }

        [SetUp]
        public void SetUp()
        {
            // Clean up persistent device-id key so each test starts fresh
            PlayerPrefs.DeleteKey(PseudoUserIdKey);
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionId");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionCumulativeMs");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionUnsentMs");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionLastTimestamp");
            PlayerPrefs.Save();

            // Remove leftover events file
            var path = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
            if (File.Exists(path)) File.Delete(path);

            LogAssert.ignoreFailingMessages = true; // suppress background-loop connection errors
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group A — Constructor validation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Constructor_NullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var _ = new EventSender(null, new NoctuaLocale());
            });
        }

        [Test]
        public void Constructor_NullLocale_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var _ = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl  = "http://localhost:19881/api/v1",
                        ClientId = "x",
                        BundleId = "com.x.y",
                    },
                    null
                );
            });
        }

        [Test]
        public void Constructor_EmptyBaseUrl_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var _ = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl  = "",
                        ClientId = "x",
                        BundleId = "com.x.y",
                        NativePlugin = new DefaultNativePlugin(),
                    },
                    new NoctuaLocale()
                );
            });
        }

        [Test]
        public void Constructor_EmptyClientId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var _ = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl  = "http://localhost:19881/api/v1",
                        ClientId = "",
                        BundleId = "com.x.y",
                        NativePlugin = new DefaultNativePlugin(),
                    },
                    new NoctuaLocale()
                );
            });
        }

        [Test]
        public void Constructor_EmptyBundleId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var _ = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl  = "http://localhost:19881/api/v1",
                        ClientId = "x",
                        BundleId = "",
                        NativePlugin = new DefaultNativePlugin(),
                    },
                    new NoctuaLocale()
                );
            });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group B — PseudoUserId persistence (GetOrCreatePersistentDeviceId)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void PseudoUserId_OnFirstCreate_IsNonEmpty()
        {
            var sender = MakeMinimalSender();
            try
            {
                Assert.IsFalse(string.IsNullOrEmpty(sender.PseudoUserId),
                    "PseudoUserId must be non-empty on first creation");
            }
            finally
            {
                sender.Dispose();
            }
        }

        [Test]
        public void PseudoUserId_OnFirstCreate_Is32CharHexString()
        {
            var sender = MakeMinimalSender();
            try
            {
                // Guid.ToString("N") produces exactly 32 lowercase hex chars
                var id = sender.PseudoUserId;
                Assert.AreEqual(32, id.Length,
                    $"PseudoUserId must be 32 chars (Guid.ToString(\"N\")), was {id.Length}");
                Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(id, @"^[0-9a-f]{32}$"),
                    $"PseudoUserId must be lowercase hex, was: {id}");
            }
            finally
            {
                sender.Dispose();
            }
        }

        [Test]
        public void PseudoUserId_TwoInstances_ReturnSameId()
        {
            // First instance writes the key to PlayerPrefs
            string firstId;
            var sender1 = MakeMinimalSender();
            try
            {
                firstId = sender1.PseudoUserId;
            }
            finally
            {
                sender1.Dispose();
            }

            // Second instance must read the same key — simulates app restart
            var sender2 = MakeMinimalSender();
            try
            {
                Assert.AreEqual(firstId, sender2.PseudoUserId,
                    "Second EventSender must return the SAME PseudoUserId as the first (persistent)");
            }
            finally
            {
                sender2.Dispose();
            }
        }

        [Test]
        public void PseudoUserId_PreStoredKey_IsReturnedAsIs()
        {
            // Pre-populate the key before the sender is created
            const string preStoredId = "abcdef0123456789abcdef0123456789";
            PlayerPrefs.SetString(PseudoUserIdKey, preStoredId);
            PlayerPrefs.Save();

            var sender = MakeMinimalSender();
            try
            {
                Assert.AreEqual(preStoredId, sender.PseudoUserId,
                    "Pre-stored key must be returned verbatim (no regeneration)");
            }
            finally
            {
                sender.Dispose();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group C — SetProperties zero-sentinel semantics
        // ═══════════════════════════════════════════════════════════════════════
        //
        // SetProperties uses zero / "" / null as "no-change" sentinels:
        //   if (userId != 0)    _userId = userId;
        //   if (gameId != 0)    _gameId = gameId;
        //   if (sessionId != "")  _sessionId = sessionId;
        //   if (isSandbox != null) _isSandbox = isSandbox;
        //
        // We verify this through the IsSandbox path (visible via IsSandbox_* tests
        // in EventTest) and through the event payload using the HTTP server.
        //
        // These tests use a mock-HTTP server on port 19882 to inspect payloads
        // without reusing the shared port 7777 / 7779.
        // ─────────────────────────────────────────────────────────────────────

        private HttpMockServer _propServer;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _propServer = new HttpMockServer("http://localhost:19882/api/v1/");
            _propServer.AddHandler("/events",
                _ => @"{""success"":""true"",""data"":{""message"":""ok""}}");
            _propServer.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            _propServer?.RemoveHandler("/events");
            _propServer?.Dispose();
        }

        private EventSender MakePropSender() =>
            new EventSender(
                new EventSenderConfig
                {
                    BaseUrl      = "http://localhost:19882/api/v1",
                    ClientId     = "unit_test_client",
                    BundleId     = "com.test.behavior",
                    BatchSize    = 1,
                    CycleDelay   = 100,
                    NativePlugin = new DefaultNativePlugin(),
                },
                new NoctuaLocale()
            );

        private async UniTask<Dictionary<string, object>> SendAndCapture(
            EventSender sender,
            string eventName,
            int timeoutMs = 4000)
        {
            _propServer.Requests.Clear();
            sender.Send(eventName);

            await UniTask.WhenAny(
                UniTask.Delay(timeoutMs),
                UniTask.WaitUntil(() => _propServer.Requests.Count > 0)
            );
            await UniTask.Delay(300);

            while (_propServer.Requests.TryDequeue(out var req))
            {
                var lines = req.Body.Trim().Split('\n');
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var ev = JsonConvert.DeserializeObject<Dictionary<string, object>>(line);
                    if (ev != null && ev.ContainsKey("event_name") &&
                        ev["event_name"]?.ToString() == eventName)
                        return ev;
                }
            }

            return null;
        }

        [Test]
        [Timeout(5000)]
        public async Task SetProperties_ZeroUserId_DoesNotClearPreviousValue()
        {
                var sender = MakePropSender();
                try
                {
                    sender.SetProperties(userId: 42);
                    sender.SetProperties(userId: 0); // sentinel — must NOT reset

                    var ev = await SendAndCapture(sender, "sentinel_test_user");
                    Assert.IsNotNull(ev, "No sentinel_test_user event received within timeout");
                    Assert.IsTrue(ev.ContainsKey("user_id"),
                        "user_id must still be set after SetProperties(userId: 0)");
                    Assert.AreEqual("42", ev["user_id"]?.ToString());
                }
                finally
                {
                    sender.Dispose();
                }
        }

        [Test]
        [Timeout(5000)]
        public async Task SetProperties_NullUserId_ClearsPreviousValue()
        {
                var sender = MakePropSender();
                try
                {
                    sender.SetProperties(userId: 42);
                    sender.SetProperties(userId: null); // explicit null → clear

                    var ev = await SendAndCapture(sender, "clear_test_user");
                    Assert.IsNotNull(ev, "No clear_test_user event received");
                    Assert.IsFalse(ev.ContainsKey("user_id"),
                        "user_id must be absent after SetProperties(userId: null)");
                }
                finally
                {
                    sender.Dispose();
                }
        }

        [Test]
        [Timeout(5000)]
        public async Task SetProperties_ZeroGameId_DoesNotClearPreviousValue()
        {
                var sender = MakePropSender();
                try
                {
                    sender.SetProperties(gameId: 99);
                    sender.SetProperties(gameId: 0); // sentinel

                    var ev = await SendAndCapture(sender, "sentinel_test_game");
                    Assert.IsNotNull(ev, "No sentinel_test_game event received");
                    Assert.IsTrue(ev.ContainsKey("game_id"),
                        "game_id must still be set after SetProperties(gameId: 0)");
                    Assert.AreEqual("99", ev["game_id"]?.ToString());
                }
                finally
                {
                    sender.Dispose();
                }
        }

        [Test]
        [Timeout(5000)]
        public async Task SetProperties_EmptySessionId_DoesNotClearPreviousValue()
        {
                var sender = MakePropSender();
                try
                {
                    sender.SetProperties(sessionId: "session-abc");
                    sender.SetProperties(sessionId: ""); // sentinel

                    var ev = await SendAndCapture(sender, "sentinel_test_session");
                    Assert.IsNotNull(ev, "No sentinel_test_session event received");
                    // The session_id in the payload comes from the EventSender field
                    // which should still hold "session-abc"
                    Assert.IsTrue(ev.ContainsKey("session_id"),
                        "session_id must be present after SetProperties(sessionId: \"\")");
                    Assert.AreEqual("session-abc", ev["session_id"]?.ToString());
                }
                finally
                {
                    sender.Dispose();
                }
        }

        [Test]
        [Timeout(5000)]
        public async Task SetProperties_IsSandboxFalse_UpdatesField()
        {
                var sender = MakePropSender();
                try
                {
                    // Set to true first, then explicitly set to false
                    sender.SetProperties(isSandbox: true);
                    sender.SetProperties(isSandbox: false);

                    var ev = await SendAndCapture(sender, "sandbox_false_test");
                    Assert.IsNotNull(ev, "No sandbox_false_test event received");
                    Assert.IsTrue(ev.ContainsKey("is_sandbox"),
                        "is_sandbox must be present after SetProperties(isSandbox: false)");
                    Assert.AreEqual(false, ev["is_sandbox"]);
                }
                finally
                {
                    sender.Dispose();
                }
        }

        [Test]
        [Timeout(5000)]
        public async Task SetProperties_NullIsSandbox_DoesNotClearPreviousValue()
        {
                var sender = MakePropSender();
                try
                {
                    sender.SetProperties(isSandbox: true);
                    sender.SetProperties(isSandbox: null); // sentinel

                    var ev = await SendAndCapture(sender, "sandbox_null_test");
                    Assert.IsNotNull(ev, "No sandbox_null_test event received");
                    Assert.IsTrue(ev.ContainsKey("is_sandbox"),
                        "is_sandbox must still be present after SetProperties(isSandbox: null)");
                    Assert.AreEqual(true, ev["is_sandbox"]);
                }
                finally
                {
                    sender.Dispose();
                }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group D — Flush and Dispose smoke tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Flush_OnEmptyQueue_DoesNotThrow()
        {
            var sender = MakeMinimalSender();
            try
            {
                Assert.DoesNotThrow(() => sender.Flush(),
                    "Flush() must not throw on an empty queue");
            }
            finally
            {
                sender.Dispose();
            }
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var sender = MakeMinimalSender();
            Assert.DoesNotThrow(() =>
            {
                sender.Dispose();
                sender.Dispose(); // idempotent
            });
        }

        [Test]
        public void Send_AfterDispose_DoesNotThrow()
        {
            // EventSender swallows calls after Dispose (logs only, never throws)
            var sender = MakeMinimalSender();
            sender.Dispose();
            Assert.DoesNotThrow(() => sender.Send("post_dispose_event"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group E — Pre-persist path: events persisted before Firebase fetch
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task PrePersist_EventSentBeforeFirebaseFetch_IsWrittenToStorage()
        {
                // Offline sender so no HTTP flush happens — we only test storage write
                var sender = MakeMinimalSender(offline: true);
                try
                {
                    // On the Editor platform, Firebase IDs are never fetched
                    // (_firebaseIdsFetched remains false initially in non-editor paths).
                    // In the Editor the #if UNITY_ANDROID || UNITY_IOS block is skipped so
                    // prePersisted stays false and the final persist always runs.
                    // Either way, at least one storage write must occur for this event.

                    sender.Send("pre_persist_probe");

                    // Give the UniTask.Void async block time to execute
                    await UniTask.Delay(1500);

                    var path = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
                    var exists = File.Exists(path);
                    var hasEvent = false;

                    if (exists)
                    {
                        var content = File.ReadAllText(path);
                        hasEvent = content.Contains("pre_persist_probe");
                    }

                    Assert.IsTrue(hasEvent,
                        "pre_persist_probe must be written to noctua_events.jsonl " +
                        "even when the app is in offline mode (pre-persist path)");
                }
                finally
                {
                    sender.Dispose();
                }
        }
    }
}
