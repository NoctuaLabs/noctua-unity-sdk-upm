using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;

using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace com.noctuagames.sdk
{
    public static class Utility
    {
        public static string errorEmailEmpty = "Email address should not be empty";
        public static string errorEmailNotValid = "Email address is not valid";
        public static string errorPasswordEmpty = "Password should not be empty";
        public static string errorPasswordShort = "Password is too short. Minimum 6 character";
        public static string errorRePasswordEmpty = "Re-Enter password should not be empty";
        public static string errorRePasswordNotMatch = "Password is not matched with repeated password";

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

        public static void RegisterForMultipleValueChanges<T>(
            VisualElement root,
            List<string> elementNames,
            Button buttonToEnable)
        {
            Dictionary<string, T> initialValues = new Dictionary<string, T>();

            foreach (var elementName in elementNames)
            {
                var element = root.Q<BindableElement>(elementName);

                if (element != null)
                {
                    var initialValue = (element as INotifyValueChanged<T>).value;
                    initialValues[elementName] = initialValue;

                    element.RegisterCallback<ChangeEvent<T>>(evt =>
                    {
                        bool anyChanged = false;
                        foreach (var name in elementNames)
                        {
                            var currentElement = root.Q<BindableElement>(name);
                            var currentValue = (currentElement as INotifyValueChanged<T>).value;

                            if (currentValue == null && initialValues[name] == null)
                            {
                                continue;
                            }

                            if (currentValue == null && initialValues[name] != null)
                            {
                                anyChanged = true;
                                break;
                            }

                            if (currentValue != null && initialValues[name] == null)
                            {
                                anyChanged = true;
                                break;
                            }

                            if (!currentValue.Equals(initialValues[name]))
                            {
                                anyChanged = true;
                                break;
                            }
                        }
                        UpdateButtonState(buttonToEnable, anyChanged);
                    });
                }
                else
                {
                    Debug.LogWarning($"Element with name '{elementName}' not found.");
                }
            }
        }

        public static void UpdateButtonState(List<TextField> textFields, Button submitButton)
        {
            UpdateButtonState(submitButton, !textFields.Any(textField => string.IsNullOrEmpty(textField.value)));
        }

        public static void UpdateButtonState(Button _submitButton, bool _isActive)
        {
            _submitButton.SetEnabled(_isActive);
            _submitButton.pickingMode = _isActive ? PickingMode.Position : PickingMode.Ignore;
        }

        public static string GetCoPublisherLogo(string companyName)
        {
            var logoMap = new Dictionary<string, string>
            {
                { "OEG JSC", "OegWhiteLogo" }
            };

            return logoMap.GetValueOrDefault(companyName, "NoctuaLogoWithText");
        }

        public static bool ContainsFlag(string flags, string flagToCheck)
        {
            return flags
                    ?
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(flag => flag.Trim())
                    .Contains(flagToCheck, StringComparer.OrdinalIgnoreCase) ??
                false;
        }

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

        public static string GetTranslation(string key, Dictionary<string, string> _translations)
        {
            if (_translations != null && _translations.TryGetValue(key, out string localizedText))
            {
                return localizedText;
            }

            return key;
        }

        public static void ApplyTranslations(VisualElement root, string uxmlName, Dictionary<string, string> translations)
        {
            ApplyTranslationsToElement(root, uxmlName, translations);
            ApplyErrorTranslation(translations);
        }

        private static void ApplyTranslationsToElement(VisualElement element, string uxmlName, Dictionary<string, string> translations)
        {
            string elementName = element.name ?? string.Empty;
            string elementType = element.GetType().Name;

            switch (element)
            {
                case Label label:
                    string labelKey = $"{uxmlName}.{elementName}.{elementType}.text";
                    string labelTranslation = GetTranslation(labelKey, translations);

                    if (labelTranslation != labelKey)
                    {
                        label.text = labelTranslation;
                    }
                    break;
                case Button button:
                    string buttonKey = $"{uxmlName}.{elementName}.{elementType}.text";
                    string buttonTranslation = GetTranslation(buttonKey, translations);

                    if (buttonTranslation != buttonKey)
                    {
                        button.text = buttonTranslation;
                    }

                    foreach (var child in button.Children())
                    {
                        ApplyTranslationsToElement(child, uxmlName, translations);
                    }
                    break;
                case TextField textField:
                    string textFieldKey = $"{uxmlName}.{elementName}.{elementType}.label";
                    string textFieldTranslation = GetTranslation(textFieldKey, translations);
                    textField.label = textFieldTranslation;

                    Label textFieldTitle = textField.Q<Label>("title");

                    if (textFieldTitle != null) textFieldTitle.text = textFieldTranslation;

                    break;
                case DropdownField dropdownField:
                    string dropdownFieldKey = $"{uxmlName}.{elementName}.{elementType}.label";
                    string dropdownFieldTranslation = GetTranslation(dropdownFieldKey, translations);
                    dropdownField.label = dropdownFieldTranslation;

                    Label dropdownTitle = dropdownField.Q<Label>("title");

                    if (dropdownTitle != null) dropdownTitle.text = dropdownFieldTranslation;

                    break;
                case VisualElement visualElement:
                    foreach (var child in visualElement.Children())
                    {
                        ApplyTranslationsToElement(child, uxmlName, translations);
                    }
                    break;
                default:
                    break;
            }
        }

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

        private static void ApplyErrorTranslation(Dictionary<string, string> translations)
        {            
            errorEmailEmpty = GetTranslation(LocaleTextKey.ErrorEmailEmpty.ToString(), translations);
            errorEmailNotValid = GetTranslation(LocaleTextKey.ErrorEmailNotValid.ToString(), translations);
            errorPasswordEmpty = GetTranslation(LocaleTextKey.ErrorPasswordEmpty.ToString(), translations);
            errorPasswordShort = GetTranslation(LocaleTextKey.ErrorPasswordShort.ToString(), translations);
            errorRePasswordEmpty = GetTranslation(LocaleTextKey.ErrorRePasswordEmpty.ToString(), translations);
            errorRePasswordNotMatch = GetTranslation(LocaleTextKey.ErrorRePasswordNotMatch.ToString(), translations);            
        }

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

        public static string ValidatePassword(string str)
        {
            if (string.IsNullOrEmpty(str)) return errorPasswordEmpty;

            if (str?.Length < 6)
            {
                return errorPasswordShort;
            }

            return string.Empty;
        }

        public static string ValidateReenterPassword(string strPassword, string strRePassword)
        {
            if (string.IsNullOrEmpty(strRePassword)) return errorRePasswordEmpty;

            if (!strPassword.Equals(strRePassword))
            {
                return errorRePasswordNotMatch;
            }

            return string.Empty;
        }

        public static string GetStoreName()
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
