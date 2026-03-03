using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    public partial class Noctua
    {
        private static readonly ILogger _sLog = new NoctuaLogger(typeof(Noctua));
        /// <summary>
        /// Backup selected PlayerPrefs keys into a key/value array for export/backup.
        /// Keys that are integers are suffixed with ":int", strings with ":string".
        /// </summary>
        /// <returns>Array of key/value pairs representing backed up PlayerPrefs.</returns>
        public static KeyValuePair<string, string>[] BackupPlayerPrefs()
        {
            KeyValuePair<string, string>[] keyValueArray = new KeyValuePair<string, string>[] { };

            var IntegerKeys = new string[] {
                "NoctuaFirstOpen",
                "NoctuaAccountContainer.UseFallback",
                "NativeGalleryPermission",
            };

            var StringKeys = new string[] {
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
            };

            foreach (var key in IntegerKeys)
            {
                var value = PlayerPrefs.GetInt(key, 0).ToString();
                _sLog.Debug($"Backing up playerPrefs {key}:{value}");
                Array.Resize(ref keyValueArray, keyValueArray.Length + 1);
                keyValueArray[keyValueArray.Length - 1] = new KeyValuePair<string, string>(
                    $"{key}:int",
                    value
                );
            }

            foreach (var key in StringKeys)
            {
                var value = PlayerPrefs.GetString(key, string.Empty);
                _sLog.Debug($"Backing up playerPrefs {key}:{value}");
                Array.Resize(ref keyValueArray, keyValueArray.Length + 1);
                keyValueArray[keyValueArray.Length - 1] = new KeyValuePair<string, string>(
                    $"{key}:string",
                    value
                );
            }

            return keyValueArray;
        }

        /// <summary>
        /// Restore PlayerPrefs from an array previously produced by <see cref="BackupPlayerPrefs"/>.
        /// </summary>
        /// <param name="keyValues">Array of key/value pairs containing PlayerPrefs data. Keys must have type suffix (":int" or ":string").</param>
        public static void RestorePlayerPrefs(KeyValuePair<string, string>[] keyValues)
        {
            foreach (var keyValue in keyValues)
            {
                var parts = keyValue.Key.Split(':');
                var key = parts[0];
                var type = parts[1];

                if (type == "int")
                {
                    if (int.TryParse(keyValue.Value, out int value))
                    {
                        _sLog.Debug($"Restoring playerPrefs {key}:{keyValue.Value}");
                        PlayerPrefs.SetInt(key, value);
                    }
                }
                else if (type == "string")
                {
                    _sLog.Debug($"Restoring playerPrefs {key}:{keyValue.Value}");
                    PlayerPrefs.SetString(key, keyValue.Value);
                }
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Returns an array of PlayerPrefs keys used by Noctua.
        /// </summary>
        /// <returns>Array of keys.</returns>
        public static string[] GetPlayerPrefsKeys()
        {
            return new string[] {
                // Integer
                "NoctuaFirstOpen",
                "NoctuaAccountContainer.UseFallback",
                "NativeGalleryPermission",
                // String
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
            };
        }
    }
}
