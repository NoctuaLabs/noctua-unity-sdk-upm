using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using com.noctuagames.sdk.Events;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// Coverage tests for <see cref="SessionTrackerBehaviour"/> — the MonoBehaviour bridge that
    /// forwards Unity lifecycle hooks (Start, OnApplicationPause, OnDestroy) to a SessionTracker.
    ///
    /// Lifecycle methods are <c>private</c>, so we invoke them via reflection rather than relying
    /// on Unity to fire them (EditMode does not invoke lifecycle hooks for components added at runtime).
    /// </summary>
    public class SessionTrackerBehaviourTest
    {
        private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        private MockEventSender _sender;
        private SessionTrackerConfig _config;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _sender = new MockEventSender();
            _config = new SessionTrackerConfig
            {
                HeartbeatPeriodMs = 60_000,
                SessionTimeoutMs  = 60_000,
            };

            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionId");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionCumulativeMs");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionUnsentMs");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionLastTimestamp");
            PlayerPrefs.Save();

            _go = new GameObject("SessionTrackerBehaviourTest");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        private static void Invoke(SessionTrackerBehaviour b, string name, params object[] args)
        {
            typeof(SessionTrackerBehaviour).GetMethod(name, Priv).Invoke(b, args);
        }

        // ─── Property round-trip ─────────────────────────────────────────────

        [Test]
        public void SessionTracker_Property_RoundTrip()
        {
            var behaviour = _go.AddComponent<SessionTrackerBehaviour>();
            Assert.IsNull(behaviour.SessionTracker, "Default field value must be null");

            var tracker = new SessionTracker(_config, _sender);
            behaviour.SessionTracker = tracker;
            Assert.AreSame(tracker, behaviour.SessionTracker, "Public field must return the assigned instance");

            tracker.Dispose();
        }

        // ─── Start() — non-null & null tracker ───────────────────────────────

        [Test]
        public void Start_WithNonNullTracker_ResumesSession()
        {
            var tracker = new SessionTracker(_config, _sender);
            var behaviour = _go.AddComponent<SessionTrackerBehaviour>();
            behaviour.SessionTracker = tracker;

            Invoke(behaviour, "Start");

            // Start() calls OnApplicationPause(false) which begins the session
            Assert.AreEqual(1, _sender.GetEventsByName("session_start").Count,
                "Start() must delegate to OnApplicationPause(false) → session_start emitted exactly once");

            tracker.Dispose();
        }

        [Test]
        public void Start_WithNullTracker_DoesNotThrow()
        {
            var behaviour = _go.AddComponent<SessionTrackerBehaviour>();
            behaviour.SessionTracker = null;

            Assert.DoesNotThrow(() => Invoke(behaviour, "Start"),
                "Start() must be safe when SessionTracker is null (null-conditional)");
            Assert.IsEmpty(_sender.SentEvents,
                "No events should flow when the tracker is null");
        }

        // ─── OnApplicationPause(bool) — both branches × null/non-null ────────

        [Test]
        public void OnApplicationPause_True_NonNullTracker_EmitsSessionPause()
        {
            var tracker = new SessionTracker(_config, _sender);
            var behaviour = _go.AddComponent<SessionTrackerBehaviour>();
            behaviour.SessionTracker = tracker;

            // Need an active session first; reuse the bridge to start it.
            Invoke(behaviour, "Start");
            _sender.Clear();

            Invoke(behaviour, "OnApplicationPause", true);

            Assert.AreEqual(1, _sender.GetEventsByName("session_pause").Count,
                "OnApplicationPause(true) must forward to tracker and emit session_pause");

            tracker.Dispose();
        }

        [Test]
        public void OnApplicationPause_False_NonNullTracker_EmitsResume()
        {
            var tracker = new SessionTracker(_config, _sender);
            var behaviour = _go.AddComponent<SessionTrackerBehaviour>();
            behaviour.SessionTracker = tracker;

            // Start → pause first, so OnApplicationPause(false) below resumes within timeout.
            Invoke(behaviour, "Start");
            Invoke(behaviour, "OnApplicationPause", true);
            _sender.Clear();

            Invoke(behaviour, "OnApplicationPause", false);

            // Within timeout window → session_continue, not session_start
            Assert.AreEqual(1, _sender.GetEventsByName("session_continue").Count,
                "OnApplicationPause(false) within timeout must forward and emit session_continue");

            tracker.Dispose();
        }

        [Test]
        public void OnApplicationPause_NullTracker_DoesNotThrow()
        {
            var behaviour = _go.AddComponent<SessionTrackerBehaviour>();
            behaviour.SessionTracker = null;

            Assert.DoesNotThrow(() => Invoke(behaviour, "OnApplicationPause", true),
                "OnApplicationPause(true) must be safe with null tracker");
            Assert.DoesNotThrow(() => Invoke(behaviour, "OnApplicationPause", false),
                "OnApplicationPause(false) must be safe with null tracker");
            Assert.IsEmpty(_sender.SentEvents);
        }

        // ─── OnDestroy() — non-null & null tracker ───────────────────────────

        [Test]
        public void OnDestroy_WithNonNullTracker_DisposesAndEmitsSessionEnd()
        {
            var tracker = new SessionTracker(_config, _sender);
            var behaviour = _go.AddComponent<SessionTrackerBehaviour>();
            behaviour.SessionTracker = tracker;

            Invoke(behaviour, "Start");
            _sender.Clear();

            Invoke(behaviour, "OnDestroy");

            Assert.AreEqual(1, _sender.GetEventsByName("session_end").Count,
                "OnDestroy() must call tracker.Dispose() → session_end emitted");
            // Second OnDestroy should still be safe (Dispose is idempotent).
            Assert.DoesNotThrow(() => Invoke(behaviour, "OnDestroy"));
        }

        [Test]
        public void OnDestroy_WithNullTracker_DoesNotThrow()
        {
            var behaviour = _go.AddComponent<SessionTrackerBehaviour>();
            behaviour.SessionTracker = null;

            Assert.DoesNotThrow(() => Invoke(behaviour, "OnDestroy"));
            Assert.IsEmpty(_sender.SentEvents);
        }

        // ─── Threading: OnApplicationPause requires the main thread ─────────
        // OnApplicationPause → SessionTracker.OnApplicationPause →
        // SaveSessionState() → PlayerPrefs.SetString(), which is main-thread only.
        // Invoking from a background thread throws UnityException.
        // This test documents that constraint so future refactors don't accidentally
        // regress it in the other direction.

        [Test]
        public void OnApplicationPause_FromBackgroundThread_ThrowsUnityException()
        {
            var tracker = new SessionTracker(_config, _sender);
            var behaviour = _go.AddComponent<SessionTrackerBehaviour>();
            behaviour.SessionTracker = tracker;

            // Start on main thread so the tracker is in the running state.
            Invoke(behaviour, "Start");
            _sender.Clear();

            // Invoking OnApplicationPause from a background thread reaches
            // PlayerPrefs.SetString which throws UnityException — the Task fault
            // wraps it inside an AggregateException which Task.Wait re-throws.
            var task = Task.Run(() => Invoke(behaviour, "OnApplicationPause", true));
            bool settled = false;
            try { settled = task.Wait(2000); }
            catch (AggregateException) { settled = true; } // faulted task = settled
            Assert.IsTrue(settled, "Task must settle within 2 s");
            Assert.IsNotNull(task.Exception,
                "OnApplicationPause from a background thread must fault: " +
                "PlayerPrefs.SetString is main-thread only (UnityException)");

            tracker.Dispose();
        }
    }
}
