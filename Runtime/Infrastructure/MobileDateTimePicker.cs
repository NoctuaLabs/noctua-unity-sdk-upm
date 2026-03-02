using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace com.noctuagames.sdk
{
    public enum IOSDateTimePickerMode
    {
        Time = 1, // Displays hour, minute, and optionally AM/PM designation depending on the locale setting (e.g. 6 | 53 | PM)
        Date = 2, // Displays month, day, and year depending on the locale setting (e.g. November | 15 | 2007)
    }

    public class MobileDateTimePicker : MonoBehaviour
    {
        public Action<DateTime> OnDateChanged;
        public Action<DateTime> OnPickerClosed;

        private static Dictionary<int, MobileDateTimePicker> activePickers = new Dictionary<int, MobileDateTimePicker>();

        #region PUBLIC_FUNCTIONS

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
            Noctua.ShowDatePicker(year, month, day, id);
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
