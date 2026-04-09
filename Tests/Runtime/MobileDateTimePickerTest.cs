using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace com.noctuagames.sdk.Tests
{
    /// <summary>
    /// Tests for <see cref="MobileDateTimePicker"/>.
    ///
    /// <see cref="MobileDateTimePicker"/> is a <see cref="MonoBehaviour"/> whose native display
    /// action (<see cref="MobileDateTimePicker.SetShowDatePickerAction"/>) is injected from the
    /// composition root at runtime.  The static event-callback methods
    /// <see cref="MobileDateTimePicker.DateChangedEvent"/> (called by the Android bridge) and
    /// <see cref="MobileDateTimePicker.PickerClosedEvent"/> (called by the iOS bridge) can be
    /// exercised without a native device by creating a <see cref="GameObject"/> in a Play Mode test.
    ///
    /// Tests that require a real native date picker dialog are marked [Ignore].
    ///
    /// NOTE: Each test uses a unique picker ID to avoid collisions with the shared static
    /// <c>activePickers</c> dictionary.
    /// </summary>
    [TestFixture]
    public class MobileDateTimePickerTest
    {
        // ─── DateChangedEvent ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator DateChangedEvent_ValidDateString_InvokesOnDateChangedCallback()
        {
            // Use ID 1001 — unique to this test to avoid cross-test static state collision
            DateTime received = DateTime.MinValue;
            MobileDateTimePicker picker = MobileDateTimePicker.CreateDate(
                id:       1001,
                year:     2024,
                month:    6,
                day:      15,
                onChange: dt => received = dt,
                onClose:  null
            );

            yield return null; // let Unity process the new GameObject

            // Simulate Android native bridge calling back into Unity
            MobileDateTimePicker.DateChangedEvent("2024-08-20 10:30:00", 1001);

            Assert.AreEqual(new DateTime(2024, 8, 20, 10, 30, 0), received,
                "DateChangedEvent should parse the date string and invoke OnDateChanged");

            // Clean up via PickerClosedEvent — removes from activePickers and destroys GameObject
            picker.PickerClosedEvent("2024-08-20 10:30:00");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DateChangedEvent_UnknownId_DoesNotThrow()
        {
            // ID 9999 has no registered picker — should be a safe no-op
            Assert.DoesNotThrow(() =>
                MobileDateTimePicker.DateChangedEvent("2024-01-01 00:00:00", 9999)
            );
            yield return null;
        }

        // ─── PickerClosedEvent ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator PickerClosedEvent_ValidDateString_InvokesOnPickerClosedCallback()
        {
            // Use ID 1002 — unique to this test
            DateTime closed = DateTime.MinValue;
            MobileDateTimePicker picker = MobileDateTimePicker.CreateDate(
                id:       1002,
                year:     2023,
                month:    1,
                day:      1,
                onChange: null,
                onClose:  dt => closed = dt
            );

            yield return null;

            // Simulate iOS native bridge calling PickerClosedEvent on the instance.
            // PickerClosedEvent also removes the entry from activePickers and destroys the GameObject.
            picker.PickerClosedEvent("2023-03-25 09:00:00");

            Assert.AreEqual(new DateTime(2023, 3, 25, 9, 0, 0), closed,
                "PickerClosedEvent should parse the date string and invoke OnPickerClosed");

            yield return null; // let deferred Destroy() complete
        }

        [UnityTest]
        public IEnumerator CreateDate_DuplicateId_DestroysOldPickerAndRegistersNew()
        {
            // Use ID 1003 — unique to this test
            int firstCallCount  = 0;
            int secondCallCount = 0;

            MobileDateTimePicker.CreateDate(
                id: 1003, year: 2024, month: 1, day: 1,
                onChange: _ => firstCallCount++
            );

            yield return null;

            // Creating a second picker with the same ID should replace the first
            MobileDateTimePicker second = MobileDateTimePicker.CreateDate(
                id: 1003, year: 2024, month: 6, day: 15,
                onChange: _ => secondCallCount++
            );

            yield return null;

            // Fire event — should go to the second picker only
            MobileDateTimePicker.DateChangedEvent("2024-07-04 00:00:00", 1003);

            Assert.AreEqual(0, firstCallCount,  "First (replaced) picker must NOT receive events");
            Assert.AreEqual(1, secondCallCount, "Second (active) picker must receive the event");

            // Clean up via PickerClosedEvent
            second.PickerClosedEvent("2024-07-04 00:00:00");
            yield return null;
        }

        // ─── Native picker (Ignore) ────────────────────────────────────────────

        [Test]
        [Ignore("Requires native iOS/Android date picker dialog. Run on device in integration suite.")]
        public void CreateDate_OnDevice_ShowsNativeDatePicker() { }

        [Test]
        [Ignore("Requires native iOS/Android date picker dialog. Run on device in integration suite.")]
        public void PickerClosed_OnDevice_FiresOnPickerClosedAndDestroysGameObject() { }
    }
}
