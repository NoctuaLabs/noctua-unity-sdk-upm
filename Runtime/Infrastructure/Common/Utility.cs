using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;

using UnityEngine;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Static utility methods for validation, string parsing, translations, platform detection, and retry logic.
    /// </summary>
    public static class Utility
    {
        private static readonly ILogger _sLog = new NoctuaLogger(typeof(Utility));

        /// <summary>Localized error message for an empty email address.</summary>
        public static string errorEmailEmpty = "Email address should not be empty";

        /// <summary>Localized error message for an invalid email address format.</summary>
        public static string errorEmailNotValid = "Email address is not valid";

        /// <summary>Localized error message for an empty password.</summary>
        public static string errorPasswordEmpty = "Password should not be empty";

        /// <summary>Localized error message for a password that is too short.</summary>
        public static string errorPasswordShort = "Password is too short. Minimum 6 character";

        /// <summary>Localized error message for an empty re-enter password field.</summary>
        public static string errorRePasswordEmpty = "Re-Enter password should not be empty";

        /// <summary>Localized error message when re-entered password does not match.</summary>
        public static string errorRePasswordNotMatch = "Password is not matched with repeated password";

        /// <summary>
        /// Extension method that recursively prints all public instance fields of an object into a formatted string.
        /// </summary>
        /// <typeparam name="T">The type of the object to inspect.</typeparam>
        /// <param name="obj">The object whose fields to print.</param>
        /// <returns>A multi-line string representation of the object's fields and nested values.</returns>
        public static string PrintFields<T>(this T obj)
        {
            var sb = new System.Text.StringBuilder();
            PrintFieldsRecursive(obj, 0, sb);

            return sb.ToString();
        }

        private static void PrintFieldsRecursive(object obj, int indentLevel, System.Text.StringBuilder sb)
        {
            if (obj == null)
            {
                return;
            }

            var type = obj.GetType();

            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            {
                return;
            }

            if (type.IsArray && obj is Array array)
            {
                sb.AppendLine(new string(' ', indentLevel * 2) + $"{type.Name}:");

                foreach (var item in array)
                {
                    PrintFieldsRecursive(item, indentLevel + 1, sb);
                }

                return;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (obj is not System.Collections.IList list) return;

                sb.AppendLine(new string(' ', indentLevel * 2) + $"{type.Name}:");

                foreach (var item in list)
                {
                    PrintFieldsRecursive(item, indentLevel + 1, sb);
                }

                return;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                if (obj is not System.Collections.IDictionary dictionary) return;

                foreach (var key in dictionary.Keys)
                {
                    var value = dictionary[key];
                    sb.AppendLine(new string(' ', indentLevel * 2) + $"{key}: {value}");

                    PrintFieldsRecursive(value, indentLevel + 1, sb);
                }

                return;
            }

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);

            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                sb.AppendLine(new string(' ', indentLevel * 2) + $"{field.Name}: {value}");

                PrintFieldsRecursive(value, indentLevel + 1, sb);
            }
        }

        /// <summary>
        /// Parses a URL query string into a dictionary of key-value pairs, handling URI unescaping.
        /// </summary>
        /// <param name="queryString">The query string to parse (with or without leading '?').</param>
        /// <returns>A dictionary of decoded query parameter keys and values.</returns>
        public static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var queryParameters = new Dictionary<string, string>();
            queryString = queryString[(queryString.IndexOf('?') + 1)..];
            queryString = queryString.Split('#')[0];

            var pairs = queryString.Split('&');
            foreach (var pair in pairs)
            {
                var splitIndex = pair.IndexOf('=');

                if (splitIndex < 1 || splitIndex == pair.Length - 1)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(pair[..splitIndex]);
                var value = Uri.UnescapeDataString(pair[(splitIndex + 1)..]);
                queryParameters[key] = value;
            }

            return queryParameters;
        }

        /// <summary>
        /// Returns the resource name of the co-publisher logo for the given company, or the default Noctua logo.
        /// </summary>
        /// <param name="companyName">The company name to look up.</param>
        /// <returns>The logo resource name string.</returns>
        public static string GetCoPublisherLogo(string companyName)
        {
            var logoMap = new Dictionary<string, string>
            {
                { "OEG JSC", "OegWhiteLogo" }
            };

            return logoMap.GetValueOrDefault(companyName, "NoctuaLogoWithText");
        }

        /// <summary>
        /// Checks whether a comma-separated flags string contains the specified flag (case-insensitive).
        /// </summary>
        /// <param name="flags">A comma-separated string of flags, or null.</param>
        /// <param name="flagToCheck">The flag name to search for.</param>
        /// <returns><c>true</c> if the flag is found; otherwise <c>false</c>.</returns>
        public static bool ContainsFlag(string flags, string flagToCheck)
        {
            return flags
                    ?
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(flag => flag.Trim())
                    .Contains(flagToCheck, StringComparer.OrdinalIgnoreCase) ??
                false;
        }

        /// <summary>
        /// Parses a boolean feature flag from a dictionary, treating "true", "1", or "on" as enabled.
        /// </summary>
        /// <param name="flags">The dictionary of feature flag key-value pairs.</param>
        /// <param name="flag">The flag key to look up.</param>
        /// <returns><c>true</c> if the flag exists and its value is "true", "1", or "on"; otherwise <c>false</c>.</returns>
        public static bool ParseBooleanFeatureFlag(Dictionary<string,string> flags, string flag)
        {
            var result = false;
            if (flags != null && flags.ContainsKey(flag) && (flags[flag] == "true" || flags[flag] == "1" || flags[flag] == "on"))
            {
                result = true;
            }

            return result;
        }


        private static string GetTranslationByLanguage(string language)
        {
            // Language to translation asset mapping
            // Language format is using ISO 639-1
            return language switch
            {
                "id" => "noctua-translation.id",
                "vi" => "noctua-translation.vi",
                _ => "noctua-translation.en"
            };
        }

        /// <summary>
        /// Loads a JSON translation file from Unity Resources for the given ISO 639-1 language code.
        /// </summary>
        /// <param name="language">The ISO 639-1 language code (e.g. "en", "id", "vi").</param>
        /// <returns>A dictionary of translation keys to localized strings, or null if loading fails.</returns>
        public static Dictionary<string, string> LoadTranslations(string language)
        {
            try
            {
                TextAsset jsonFile = Resources.Load<TextAsset>(GetTranslationByLanguage(language));
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonFile.text);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves a localized string from the translations dictionary, falling back to the key itself if not found.
        /// </summary>
        /// <param name="key">The translation key to look up.</param>
        /// <param name="_translations">The translations dictionary, or null to return the key as-is.</param>
        /// <returns>The localized string, or the key if no translation exists.</returns>
        public static string GetTranslation(string key, Dictionary<string, string> _translations)
        {
            if (_translations != null && _translations.TryGetValue(key, out string localizedText))
            {
                return localizedText;
            }

            return key;
        }

        /// <summary>
        /// Applies localized translations to the static validation error message strings.
        /// </summary>
        /// <param name="translations">The translations dictionary to look up localized error messages from.</param>
        internal static void ApplyErrorTranslation(Dictionary<string, string> translations)
        {
            errorEmailEmpty = GetTranslation(LocaleTextKey.ErrorEmailEmpty.ToString(), translations);
            errorEmailNotValid = GetTranslation(LocaleTextKey.ErrorEmailNotValid.ToString(), translations);
            errorPasswordEmpty = GetTranslation(LocaleTextKey.ErrorPasswordEmpty.ToString(), translations);
            errorPasswordShort = GetTranslation(LocaleTextKey.ErrorPasswordShort.ToString(), translations);
            errorRePasswordEmpty = GetTranslation(LocaleTextKey.ErrorRePasswordEmpty.ToString(), translations);
            errorRePasswordNotMatch = GetTranslation(LocaleTextKey.ErrorRePasswordNotMatch.ToString(), translations);
        }

        /// <summary>
        /// Retries an async task with exponential backoff and jitter on <see cref="NoctuaErrorCode.Networking"/> errors.
        /// Non-networking errors are rethrown immediately without retry.
        /// </summary>
        /// <typeparam name="T">The return type of the async task.</typeparam>
        /// <param name="task">The async task factory to invoke on each attempt.</param>
        /// <param name="maxRetries">Maximum number of retry attempts before the final attempt. Defaults to 3.</param>
        /// <param name="initialDelaySeconds">Delay in seconds before the first retry. Defaults to 0.5.</param>
        /// <param name="exponent">Multiplier applied to the delay after each retry. Defaults to 2.0.</param>
        /// <param name="maxDelaySeconds">Maximum delay cap in seconds. Defaults to the delay at the last retry.</param>
        /// <returns>The result of the successful task invocation.</returns>
        public static async UniTask<T> RetryAsyncTask<T>(
            Func<UniTask<T>> task,
            int maxRetries = 3,
            double initialDelaySeconds = 0.5,
            double exponent = 2.0,
            double maxDelaySeconds = -1
        )
        {
            var random = new System.Random();
            var delay = initialDelaySeconds;
            maxDelaySeconds = maxDelaySeconds > 0 ? maxDelaySeconds : initialDelaySeconds * Math.Pow(exponent, maxRetries - 1);

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    return await task();
                }
                catch (NoctuaException e)
                {
                    if ((NoctuaErrorCode)e.ErrorCode != NoctuaErrorCode.Networking)
                    {
                        throw;
                    }

                    var delayWithJitter = ((random.NextDouble() * 0.5) + 0.75) * delay;
                    await UniTask.Delay(TimeSpan.FromSeconds(delayWithJitter));
                    delay *= exponent;

                    delay = Math.Min(delay, maxDelaySeconds);
                }
            }

            return await task();
        }

        /// <summary>
        /// Validates an email address format, supporting internationalized domain names.
        /// </summary>
        /// <param name="str">The email address string to validate.</param>
        /// <returns>An empty string if valid; otherwise a localized error message.</returns>
        public static string ValidateEmail(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return errorEmailEmpty;

            try
            {
                // Regular expression pattern to validate email address
                string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

                // Use IdnMapping class to convert Unicode domain names, if applicable
                str = Regex.Replace(str, @"(@)(.+)$", match =>
                {
                    var idn = new IdnMapping();
                    string domainName = idn.GetAscii(match.Groups[2].Value);
                    return match.Groups[1].Value + domainName;
                }, RegexOptions.None, TimeSpan.FromMilliseconds(200));

                // Return true if the email matches the pattern
                if (Regex.IsMatch(str, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250)))
                    return string.Empty;
                else
                    return errorEmailNotValid;
            }
            catch
            {
                return errorEmailNotValid;
            }
        }

        /// <summary>
        /// Validates that a password is non-empty and at least 6 characters long.
        /// </summary>
        /// <param name="str">The password string to validate.</param>
        /// <returns>An empty string if valid; otherwise a localized error message.</returns>
        public static string ValidatePassword(string str)
        {
            if (string.IsNullOrEmpty(str)) return errorPasswordEmpty;

            if (str?.Length < 6)
            {
                return errorPasswordShort;
            }

            return string.Empty;
        }

        /// <summary>
        /// Validates that the re-entered password is non-empty and matches the original password.
        /// </summary>
        /// <param name="strPassword">The original password.</param>
        /// <param name="strRePassword">The re-entered password to compare against.</param>
        /// <returns>An empty string if the passwords match; otherwise a localized error message.</returns>
        public static string ValidateReenterPassword(string strPassword, string strRePassword)
        {
            if (string.IsNullOrEmpty(strRePassword)) return errorRePasswordEmpty;

            if (!strPassword.Equals(strRePassword))
            {
                return errorRePasswordNotMatch;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the payment platform type string based on the application installer name
        /// (e.g. "playstore", "appstore", or "direct").
        /// </summary>
        /// <returns>The platform type as a string matching <see cref="PaymentType"/> values.</returns>
        public static string GetPlatformType()
        {
            return Application.installerName switch
            {
                "com.android.vending" => PaymentType.playstore.ToString(),
                "com.apple.appstore" => PaymentType.appstore.ToString(),
                _ => PaymentType.direct.ToString()
            };
        }
    }
}
