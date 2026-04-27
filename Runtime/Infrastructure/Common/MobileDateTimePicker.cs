using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Specifies the display mode for the iOS native date/time picker.
    /// </summary>
    public enum IOSDateTimePickerMode
    {
        /// <summary>Displays hour, minute, and optionally AM/PM designation depending on the locale setting (e.g. 6 | 53 | PM).</summary>
        Time = 1,
        /// <summary>Displays month, day, and year depending on the locale setting (e.g. November | 15 | 2007).</summary>
        Date = 2,
    }

    /// <summary>
    /// Manages native mobile date picker dialogs on Android and iOS. Each picker instance is
    /// tracked by a unique integer ID and destroyed automatically when the picker is closed.
    /// The native display action is injected via <see cref="SetShowDatePickerAction"/> from the composition root.
    /// </summary>
    public class MobileDateTimePicker : MonoBehaviour
    {
        /// <summary>Callback invoked when the user changes the selected date in the picker.</summary>
        public Action<DateTime> OnDateChanged;

        /// <summary>Callback invoked when the picker dialog is closed.</summary>
        public Action<DateTime> OnPickerClosed;

        private static Dictionary<int, MobileDateTimePicker> activePickers = new Dictionary<int, MobileDateTimePicker>();
        private static Action<int, int, int, int> _showDatePickerAction;

        /// <summary>
        /// Sets the native date picker callback. Called once from the composition root.
        /// </summary>
        internal static void SetShowDatePickerAction(Action<int, int, int, int> action)
        {
            _showDatePickerAction = action;
        }

        #region PUBLIC_FUNCTIONS

        /// <summary>
        /// Creates and displays a native date picker dialog with the specified initial date.
        /// If a picker with the same ID already exists, it is destroyed and replaced.
        /// </summary>
        /// <param name="id">A unique identifier for this picker instance.</param>
        /// <param name="year">The initial year to display.</param>
        /// <param name="month">The initial month to display (1-12).</param>
        /// <param name="day">The initial day to display (1-31).</param>
        /// <param name="onChange">Optional callback invoked when the user changes the selected date.</param>
        /// <param name="onClose">Optional callback invoked when the picker is dismissed.</param>
        /// <returns>The created <see cref="MobileDateTimePicker"/> component instance.</returns>
        public static MobileDateTimePicker CreateDate(int id, int year, int month, int day, Action<DateTime> onChange = null, Action<DateTime> onClose = null)
        {
            MobileDateTimePicker dialog = new GameObject($"MobileDateTimePicker_{id}").AddComponent<MobileDateTimePicker>();
            dialog.OnDateChanged = onChange;
            dialog.OnPickerClosed = onClose;

            if (activePickers.ContainsKey(id))
            {
                Destroy(activePickers[id].gameObject);
                activePickers.Remove(id);
            }

            activePickers[id] = dialog;
            _showDatePickerAction?.Invoke(year, month, day, id);
            return dialog;
        }

        #endregion

        //--------------------------------------
        // Events
        //--------------------------------------

        string formatDate = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// This function should be called when the date changes.
        /// Note: Available in Android.
        /// </summary>
        /// <param name="time">The date string.</param>
        /// <param name="id">The unique identifier for the picker.</param>
        public static void DateChangedEvent(string time, int id)
        {
            if (activePickers.ContainsKey(id))
            {
                DateTime dt = DateTime.ParseExact(time, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                activePickers[id].OnDateChanged?.Invoke(dt);
            }
        }

        /// <summary>
        /// This function should be called when the picker closes.
        /// </summary>
        /// <param name="time">The date string.</param>
        public void PickerClosedEvent(string time)
        {
            // Extract the id from the GameObject's name.
            // Assuming the GameObject is named in the format "MobileDateTimePicker_<id>"
            string[] nameParts = gameObject.name.Split('_');
            if (nameParts.Length == 2 && int.TryParse(nameParts[1], out int id))
            {
                if (activePickers.ContainsKey(id))
                {
                    DateTime dt = DateTime.ParseExact(time, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    activePickers[id].OnPickerClosed?.Invoke(dt);

                    // Clean up the picker once closed.
                    Destroy(activePickers[id].gameObject);
                    activePickers.Remove(id);
                }
            }
        }

    }
}
