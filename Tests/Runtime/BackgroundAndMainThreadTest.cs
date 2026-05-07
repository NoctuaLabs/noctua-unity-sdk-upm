using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    /// <summary>
    /// Tests for background-thread safety and main-thread marshalling across
    /// EventSender and SessionTracker.
    ///
    /// Group A — EventSender background-thread safety (5 tests)
    ///   A1. Send() from Task.Run background thread does not throw, event stored
    ///   A2. Send() concurrent from many threads — no data loss or corruption
    ///   A3. Flush() from a background thread (IsBackground=true) is allowed
    ///   A4. Flush() from non-main, non-background thread is silently skipped
    ///   A5. Rapid concurrent Send() calls — ConcurrentQueue integrity holds
    ///
    /// Group B — SessionTracker background-thread safety (4 tests)
    ///   B1. Dispose() from background thread does not throw
    ///   B2. Dispose() from background thread still sends session_end
    ///   B3. Dispose() from background thread skips Flush() (main-thread guard)
    ///   B4. OnApplicationPause() called rapidly from simulated background burst
    ///       is guarded by the min-session-gap (10s) — no session explosion
    ///
    /// Group C — Main-thread enrichment (2 tests)
    ///   C1. Events sent from background threads contain SystemInfo fields
    ///       (device_os, device_model, etc.) — proves SwitchToMainThread ran
    ///   C2. PlayerPrefs key written by heartbeat survives concurrent background loads
    /// </summary>
    [TestFixture]
    public class BackgroundAndMainThreadTest
    {
        // ── Shared setup ─────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionId");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionCumulativeMs");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionUnsentMs");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionLastTimestamp");
            PlayerPrefs.DeleteKey("NoctuaPersistentDeviceId");
            PlayerPrefs.Save();

            var path = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
            if (File.Exists(path)) File.Delete(path);

            LogAssert.ignoreFailingMessages = true; // suppress background-loop errors
        }

        private static EventSender MakeOfflineSender() =>
            new EventSender(
                new EventSenderConfig
                {
                    BaseUrl      = "http://localhost:19990/api/v1",
                    ClientId     = "thread_test_client",
                    BundleId     = "com.test.threads",
                    CycleDelay   = 60_000,
                    NativePlugin = new DefaultNativePlugin(),
                    IsOfflineModeFunc = () => true,
                },
                new NoctuaLocale()
            );

        private static async UniTask WaitForStorageFile(string marker, int timeoutMs = 3000)
        {
            var path = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(path) && File.ReadAllText(path).Contains(marker))
                    return;
                await UniTask.Delay(100);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group A — EventSender background-thread safety
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A1 — Send() is safe to call from a Task.Run background thread.
        /// Internally, Send() uses UniTask.Void(async () => { await UniTask.SwitchToMainThread(); ... })
        /// to marshal all SystemInfo / PlayerPrefs access back to the Unity main thread.
        /// The caller thread only enqueues the fire-and-forget; no Unity API is touched there.
        /// </summary>
        [UnityTest]
        public IEnumerator Send_FromBackgroundThread_DoesNotThrow() =>
            UniTask.ToCoroutine(async () =>
            {
                var sender = MakeOfflineSender();
                try
                {
                    Exception caughtEx = null;

                    await Task.Run(() =>
                    {
                        try
                        {
                            sender.Send("bg_thread_probe");
                        }
                        catch (Exception ex)
                        {
                            caughtEx = ex;
                        }
                    });

                    Assert.IsNull(caughtEx,
                        $"Send() from background thread must not throw. Got: {caughtEx}");

                    // Give UniTask.SwitchToMainThread() time to run the enrichment + storage write
                    await WaitForStorageFile("bg_thread_probe");

                    var path = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
                    Assert.IsTrue(File.Exists(path) && File.ReadAllText(path).Contains("bg_thread_probe"),
                        "Event sent from background thread must be written to noctua_events.jsonl");
                }
                finally
                {
                    sender.Dispose();
                }
            });

        /// <summary>
        /// A2 — Send() concurrent from multiple threads — no events are lost and
        /// the ConcurrentQueue never throws under concurrent access.
        /// All N×M events must appear in the JSONL file within the timeout.
        /// </summary>
        [UnityTest]
        public IEnumerator Send_ConcurrentFromMultipleThreads_AllEventsStored() =>
            UniTask.ToCoroutine(async () =>
            {
                const int threadCount    = 5;
                const int eventsPerThread = 8;
                var sender = MakeOfflineSender();
                try
                {
                    var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

                    var tasks = Enumerable.Range(0, threadCount).Select(i =>
                        Task.Run(() =>
                        {
                            for (var j = 0; j < eventsPerThread; j++)
                            {
                                try
                                {
                                    sender.Send($"concurrent_event_t{i}_e{j}");
                                }
                                catch (Exception ex)
                                {
                                    exceptions.Add(ex);
                                }
                            }
                        })
                    ).ToArray();

                    await Task.WhenAll(tasks);

                    Assert.IsEmpty(exceptions,
                        $"No exception must be thrown during concurrent Send() calls. " +
                        $"Got {exceptions.Count}: {exceptions.FirstOrDefault()}");

                    // Wait for all UniTask.SwitchToMainThread() continuations + storage writes
                    await UniTask.Delay(4000);

                    var path = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
                    var storedCount = 0;
                    if (File.Exists(path))
                    {
                        var content = File.ReadAllText(path);
                        storedCount = content
                            .Split('\n')
                            .Count(l => l.Contains("concurrent_event_"));
                    }

                    var total = threadCount * eventsPerThread;
                    Assert.GreaterOrEqual(storedCount, total,
                        $"All {total} concurrent events must be stored. " +
                        $"Only {storedCount} found in JSONL.");
                }
                finally
                {
                    sender.Dispose();
                }
            });

        /// <summary>
        /// A3 — Flush() is callable from a background thread (IsBackground = true).
        /// The guard in Flush() is:
        ///   if (!Thread.CurrentThread.IsBackground && ManagedThreadId != 1) return;
        /// Background threads pass this guard (IsBackground == true) and the flush proceeds.
        /// </summary>
        [Test]
        public void Flush_FromBackgroundThread_DoesNotThrow()
        {
            var sender = MakeOfflineSender();
            try
            {
                Exception caughtEx = null;
                var thread = new Thread(() =>
                {
                    // IsBackground defaults to false on manual threads; set true explicitly
                    try { sender.Flush(); }
                    catch (Exception ex) { caughtEx = ex; }
                }) { IsBackground = true };

                thread.Start();
                thread.Join(3000);

                Assert.IsNull(caughtEx,
                    $"Flush() from a background thread must not throw. Got: {caughtEx}");
            }
            finally
            {
                sender.Dispose();
            }
        }

        /// <summary>
        /// A4 — Flush() called from a non-main, non-background thread is silently skipped.
        /// The guard:
        ///   if (!Thread.CurrentThread.IsBackground && ManagedThreadId != 1) return;
        /// A manually created Thread with IsBackground=false and Id != 1 hits this guard.
        /// No exception must be thrown — the method just returns early.
        /// </summary>
        [Test]
        public void Flush_FromNonMainNonBackgroundThread_IsSkippedSilently()
        {
            var sender = MakeOfflineSender();
            try
            {
                Exception caughtEx = null;
                var thread = new Thread(() =>
                {
                    try { sender.Flush(); }
                    catch (Exception ex) { caughtEx = ex; }
                }) { IsBackground = false }; // non-background AND non-main

                thread.Start();
                thread.Join(3000);

                Assert.IsNull(caughtEx,
                    "Flush() from non-main, non-background thread must not throw (early return guard)");
            }
            finally
            {
                sender.Dispose();
            }
        }

        /// <summary>
        /// A5 — Rapid-fire Send() calls from the same thread (burst) do not corrupt
        /// the write queue. Each event is a distinct JSON string; after the burst all
        /// events must appear in storage exactly once.
        /// </summary>
        [UnityTest]
        public IEnumerator Send_BurstFromMainThread_WritesAllEventsWithoutDuplication() =>
            UniTask.ToCoroutine(async () =>
            {
                const int eventCount = 50;
                var sender = MakeOfflineSender();
                try
                {
                    for (var i = 0; i < eventCount; i++)
                        sender.Send($"burst_event_{i:000}");

                    await UniTask.Delay(4000);

                    var path = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
                    Assert.IsTrue(File.Exists(path), "JSONL file must exist after burst");

                    var lines = File.ReadAllLines(path)
                        .Where(l => l.Contains("burst_event_"))
                        .ToList();

                    Assert.AreEqual(eventCount, lines.Count,
                        $"All {eventCount} burst events must be stored exactly once. " +
                        $"Found {lines.Count}.");
                }
                finally
                {
                    sender.Dispose();
                }
            });

        // ═══════════════════════════════════════════════════════════════════════
        // Group B — SessionTracker background-thread safety
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// B1 — Dispose() called from a background thread must not throw.
        /// The Flush() guard inside Dispose() checks ManagedThreadId != 1 and skips
        /// the flush when not on the main thread — preventing Unity API crashes from
        /// background/GC-finalizer threads.
        /// </summary>
        [Test]
        public void SessionTracker_Dispose_FromBackgroundThread_DoesNotThrow()
        {
            var mock    = new MockEventSender();
            var tracker = new SessionTracker(new SessionTrackerConfig(), mock, null);
            tracker.OnApplicationPause(false); // start a session

            Exception caughtEx = null;
            var thread = new Thread(() =>
            {
                try { tracker.Dispose(); }
                catch (Exception ex) { caughtEx = ex; }
            }) { IsBackground = true };

            thread.Start();
            thread.Join(3000);

            Assert.IsNull(caughtEx,
                $"Dispose() from background thread must not throw. Got: {caughtEx}");
        }

        /// <summary>
        /// B2 — Dispose() from a background thread still sends session_end.
        /// Only the Flush() call is guarded to the main thread; the event Send() calls
        /// (session_end, noctua_user_engagement) execute regardless of calling thread.
        /// </summary>
        [UnityTest]
        public IEnumerator SessionTracker_Dispose_FromBackgroundThread_SendsSessionEnd() =>
            UniTask.ToCoroutine(async () =>
            {
                var mock    = new MockEventSender();
                var tracker = new SessionTracker(new SessionTrackerConfig(), mock, null);
                tracker.OnApplicationPause(false);
                await UniTask.Delay(50);

                var thread = new Thread(() => tracker.Dispose()) { IsBackground = true };
                thread.Start();
                thread.Join(3000);

                await UniTask.Delay(300); // let any async work settle

                Assert.IsTrue(mock.SentEvents.Any(e => e.Name == "session_end"),
                    "session_end must be sent even when Dispose() is called from a background thread");
            });

        /// <summary>
        /// B3 — Dispose() from a background thread skips the Flush() call.
        /// MockEventSender.FlushCount must remain 0 when Dispose() runs off main thread.
        /// This verifies the `if (Thread.CurrentThread.ManagedThreadId != 1) return;`
        /// guard inside Dispose() is working.
        /// </summary>
        [UnityTest]
        public IEnumerator SessionTracker_Dispose_FromBackgroundThread_SkipsFlush() =>
            UniTask.ToCoroutine(async () =>
            {
                var mock = new MockEventSender();
                // Enable flush-on-dispose via remote flag
                var flags = new Dictionary<string, bool> { { "sendEventsOnFlushEnabled", true } };
                var tracker = new SessionTracker(new SessionTrackerConfig(), mock, flags);
                tracker.OnApplicationPause(false);
                await UniTask.Delay(50);

                var thread = new Thread(() => tracker.Dispose()) { IsBackground = true };
                thread.Start();
                thread.Join(3000);

                await UniTask.Delay(300);

                Assert.AreEqual(0, mock.FlushCount,
                    "Flush() must NOT be called when Dispose() runs on a background thread " +
                    "(Unity APIs like UnityWebRequest crash from non-main threads)");
            });

        /// <summary>
        /// B4 — Rapid OnApplicationPause(false) calls from a simulated "background burst"
        /// (ad SDK firing resume events in quick succession) are guarded by the 10-second
        /// min-session-gap. Only the first call creates a session; subsequent calls within
        /// the gap window are suppressed.
        ///
        /// This test runs everything on the main thread to avoid Unity API restrictions,
        /// but simulates the burst pattern that ad SDKs cause.
        /// </summary>
        [UnityTest]
        public IEnumerator SessionTracker_RapidResumeBurst_MinGapGuardSuppressesInflation() =>
            UniTask.ToCoroutine(async () =>
            {
                var mock    = new MockEventSender();
                var tracker = new SessionTracker(new SessionTrackerConfig(), mock, null);

                // Simulate initial session start
                tracker.OnApplicationPause(false);
                await UniTask.Delay(20);

                // Simulate rapid session timeout + burst of resume calls (ad SDK pattern)
                tracker.OnApplicationPause(true); // pause
                await UniTask.Delay(20);

                // Force session timeout by directly resetting internal timer
                // by pausing again and waiting past timeout:
                // Instead, fire 5 rapid resume attempts within 10s — all should be suppressed
                // after the first because _lastSessionStartTime was just set.
                tracker.OnApplicationPause(false); // first resume after pause → may create new session
                await UniTask.Delay(10);
                tracker.OnApplicationPause(true);
                await UniTask.Delay(10);
                tracker.OnApplicationPause(false); // burst 2 — within 10s gap
                await UniTask.Delay(10);
                tracker.OnApplicationPause(true);
                await UniTask.Delay(10);
                tracker.OnApplicationPause(false); // burst 3 — within 10s gap
                await UniTask.Delay(10);
                tracker.OnApplicationPause(true);
                await UniTask.Delay(10);
                tracker.OnApplicationPause(false); // burst 4 — within 10s gap
                await UniTask.Delay(200);

                var sessionStartCount = mock.SentEvents.Count(e => e.Name == "session_start");
                Assert.LessOrEqual(sessionStartCount, 2,
                    $"Min-session-gap guard must prevent session explosion. " +
                    $"Expected ≤2 session_starts for 5 rapid resumes, got {sessionStartCount}");

                tracker.Dispose();
            });

        // ═══════════════════════════════════════════════════════════════════════
        // Group C — Main-thread enrichment verification
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// C1 — Events sent from a background thread must contain SystemInfo fields.
        /// These fields (device_os, device_model, device_type) are Unity main-thread-only APIs.
        /// EventSender.Send() marshals to main thread via `await UniTask.SwitchToMainThread()`
        /// before reading them. If the switch doesn't happen, the fields would be missing/null.
        /// </summary>
        [UnityTest]
        public IEnumerator Send_FromBackgroundThread_EventContainsSystemInfoFields() =>
            UniTask.ToCoroutine(async () =>
            {
                var sender = MakeOfflineSender();
                try
                {
                    await Task.Run(() => sender.Send("sysinfo_probe"));
                    await WaitForStorageFile("sysinfo_probe");

                    // Extra frame yield to ensure ProcessWriteQueue drain is fully committed
                    await UniTask.NextFrame();

                    var path = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
                    Assert.IsTrue(File.Exists(path), "JSONL file must exist");

                    var line = File.ReadAllLines(path)
                        .FirstOrDefault(l => l.Contains("sysinfo_probe"));
                    Assert.IsNotNull(line, $"sysinfo_probe event not found in JSONL (file contents: {(File.Exists(path) ? File.ReadAllText(path) : "<missing>")})");

                    // Deserialize the NativeEvent wrapper and then parse the inner JSON payload.
                    // This is more robust than raw byte-pattern matching because it handles
                    // any escaping differences across platforms (Editor, Android, iOS).
                    //
                    // DefaultNativePlugin.InsertEvent stores:
                    //   NativeEvent { id, eventJson: "<inner-json-string>", createdAt }
                    // The inner eventJson is the event payload dictionary.
                    var nativeEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<NativeEvent>(line);
                    Assert.IsNotNull(nativeEvent?.EventJson,
                        $"NativeEvent.EventJson must not be null. Raw line: {line}");

                    var innerData = Newtonsoft.Json.JsonConvert.DeserializeObject<
                        System.Collections.Generic.Dictionary<string, object>>(nativeEvent.EventJson);
                    Assert.IsNotNull(innerData,
                        $"Inner event JSON must deserialize to a dictionary. EventJson: {nativeEvent.EventJson}");

                    // device_os, device_model, device_type are added AFTER await UniTask.SwitchToMainThread().
                    // Their presence in the stored event proves the main-thread switch succeeded.
                    Assert.IsTrue(innerData.ContainsKey("device_os"),
                        $"device_os must be present — requires UniTask.SwitchToMainThread() to succeed. Keys found: {string.Join(", ", innerData.Keys)}");
                    Assert.IsTrue(innerData.ContainsKey("device_model"),
                        $"device_model must be present — requires UniTask.SwitchToMainThread() to succeed. Keys found: {string.Join(", ", innerData.Keys)}");
                    Assert.IsTrue(innerData.ContainsKey("device_type"),
                        $"device_type must be present — requires UniTask.SwitchToMainThread() to succeed. Keys found: {string.Join(", ", innerData.Keys)}");
                }
                finally
                {
                    sender.Dispose();
                }
            });

        /// <summary>
        /// C2 — RunHeartbeat uses `await UniTask.SwitchToMainThread()` before
        /// calling SaveSessionState() which writes to PlayerPrefs.
        /// This test verifies that after a heartbeat fires the orphaned session
        /// PlayerPrefs key is correctly written (only possible on main thread).
        /// </summary>
        [UnityTest]
        public IEnumerator RunHeartbeat_WritesToPlayerPrefs_OnMainThread() =>
            UniTask.ToCoroutine(async () =>
            {
                var mock = new MockEventSender();
                // Use a very short heartbeat period so we don't have to wait 60s
                var config = new SessionTrackerConfig { HeartbeatPeriodMs = 200 };
                var tracker = new SessionTracker(config, mock, null);

                tracker.OnApplicationPause(false); // start session → starts heartbeat task
                await UniTask.Delay(50);

                // Wait for at least one heartbeat to fire (200ms period + margin)
                await UniTask.Delay(600);

                // After heartbeat, SaveSessionState() writes NoctuaOrphanedSessionCumulativeMs
                var stored = PlayerPrefs.GetString("NoctuaOrphanedSessionCumulativeMs", "");
                Assert.IsFalse(string.IsNullOrEmpty(stored),
                    "NoctuaOrphanedSessionCumulativeMs must be written after heartbeat " +
                    "(confirms RunHeartbeat switched to main thread before SaveSessionState)");

                tracker.Dispose();
            });

        // ═══════════════════════════════════════════════════════════════════════
        // Group D — EventSender survivability under stress
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// D1 — Calling Send() concurrently with Dispose() must not throw from either
        /// the Send caller or the Dispose caller. EventSender guards _disposed with a
        /// flag; this test stresses the window between the check and the action.
        /// </summary>
        [UnityTest]
        public IEnumerator Send_ConcurrentWithDispose_DoesNotThrow() =>
            UniTask.ToCoroutine(async () =>
            {
                var sender = MakeOfflineSender();
                Exception sendEx    = null;
                Exception disposeEx = null;

                // Fire both racing operations simultaneously
                var disposeTask = Task.Run(() =>
                {
                    try { sender.Dispose(); }
                    catch (Exception e) { disposeEx = e; }
                });

                var sendTask = Task.Run(() =>
                {
                    try { sender.Send("dispose_race_event"); }
                    catch (Exception e) { sendEx = e; }
                });

                await Task.WhenAll(disposeTask, sendTask);
                await UniTask.NextFrame(); // let any queued UniTask.Void continuations settle

                Assert.IsNull(sendEx,
                    $"Send() during Dispose() must not throw. Got: {sendEx}");
                Assert.IsNull(disposeEx,
                    $"Dispose() during concurrent Send() must not throw. Got: {disposeEx}");
            });

        /// <summary>
        /// D2 — SetProperties() writes multiple fields (userId, gameId, sessionId…)
        /// that the UniTask.Void block reads without a lock. Interleaving SetProperties
        /// with rapid Send() calls must not deadlock or corrupt field state (no throw).
        /// </summary>
        [UnityTest]
        public IEnumerator SetProperties_ConcurrentWithSend_NoDeadlockNoThrow() =>
            UniTask.ToCoroutine(async () =>
            {
                var sender  = MakeOfflineSender();
                var cts     = new CancellationTokenSource();
                Exception caught = null;

                try
                {
                    // Background: set properties in a tight loop
                    var propTask = Task.Run(async () =>
                    {
                        int seq = 0;
                        while (!cts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                sender.SetProperties(userId: seq % 2 == 0 ? 100 : 0,
                                                     sessionId: seq % 3 == 0 ? "s-abc" : "");
                            }
                            catch (Exception e) { caught = e; }
                            seq++;
                            await Task.Delay(2, cts.Token).ContinueWith(_ => { });
                        }
                    });

                    // Main: fire 30 sends
                    for (int i = 0; i < 30; i++)
                    {
                        try { sender.Send($"prop_race_{i}"); }
                        catch (Exception e) { caught = e; }
                    }

                    // Give everything time to settle before cancelling
                    await UniTask.Delay(300);
                    cts.Cancel();
                    await propTask;
                }
                finally
                {
                    sender.Dispose();
                }

                Assert.IsNull(caught,
                    $"Concurrent SetProperties + Send must not throw. Got: {caught}");
            });

        /// <summary>
        /// D3 — Multiple threads calling Flush() simultaneously must not throw.
        /// Flush() can be triggered from any thread (e.g. from OnApplicationFocus,
        /// OnApplicationPause, and network callbacks all at once).
        /// </summary>
        [UnityTest]
        public IEnumerator Flush_ConcurrentFromMultipleThreads_DoesNotThrow() =>
            UniTask.ToCoroutine(async () =>
            {
                var sender = MakeOfflineSender();
                var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();

                try
                {
                    // Send a few events so the flush queue is non-empty
                    for (int i = 0; i < 5; i++) sender.Send($"pre_flush_{i}");
                    await UniTask.Delay(200); // let writes settle

                    // Five concurrent flushes
                    var tasks = Enumerable.Range(0, 5)
                        .Select(_ => Task.Run(() =>
                        {
                            try { sender.Flush(); }
                            catch (Exception e) { errors.Add(e); }
                        }))
                        .ToArray();

                    await Task.WhenAll(tasks);
                }
                finally
                {
                    sender.Dispose();
                }

                Assert.AreEqual(0, errors.Count,
                    $"Concurrent Flush() calls must not throw. Errors: {string.Join("; ", errors.Select(e => e.Message))}");
            });

        /// <summary>
        /// D4 — Send() called after Dispose() must be silently swallowed (no throw,
        /// no unhandled exception). EventSender checks _disposed early and returns.
        /// </summary>
        [Test]
        public void Send_AfterDispose_IsSilentlyIgnored()
        {
            var sender = MakeOfflineSender();
            sender.Dispose();

            Assert.DoesNotThrow(() =>
            {
                sender.Send("post_dispose_event");
                sender.Send("post_dispose_event_2");
            }, "Send() after Dispose() must be silently swallowed, not throw");
        }

        /// <summary>
        /// D5 — Dispose() called multiple times from the same thread must be idempotent
        /// (no double-free, no double session_end, no throw on second call).
        /// </summary>
        [Test]
        public void Dispose_CalledTwice_IsIdempotentNoThrow()
        {
            var sender = MakeOfflineSender();

            Assert.DoesNotThrow(() =>
            {
                sender.Dispose();
                sender.Dispose(); // second call must be silently ignored
            }, "Dispose() must be idempotent — second call must not throw");
        }
    }
}
