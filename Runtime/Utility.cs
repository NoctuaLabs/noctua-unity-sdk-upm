using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using Newtonsoft.Json;

namespace com.noctuagames.sdk
{
    public static class Utility
    {
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

            foreach (var (key, value) in queryParameters)
            {
                Debug.Log($"{key}: {value}");
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

                            if (!currentValue.Equals(initialValues[name]))
                            {
                                anyChanged = true;
                                break;
                            }
                        }
                        buttonToEnable.SetEnabled(anyChanged);
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
            bool isAnyFieldEmpty = textFields.Any(textField => string.IsNullOrEmpty(textField.value));

            submitButton.SetEnabled(!isAnyFieldEmpty);
        }

        public static string GetCoPublisherLogo(string companyName)
        {
            var logoMap = new Dictionary<string, string>
            {
                { "OEG", "OegWhiteLogo" }
            };

            return logoMap.GetValueOrDefault(companyName, "NoctuaLogoWithText");
        }
        
        public static bool ContainsFlag(string flags, string flagToCheck)
        {
            return flags
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(flag => flag.Trim())
                .Contains(flagToCheck, StringComparer.OrdinalIgnoreCase);
        }

        private static string GetTranslationByLanguage(string regionCodeParam)
        {
            var language = new NoctuaLocale();
            var regionCode = string.IsNullOrEmpty(regionCodeParam) ? language.GetLanguage() : regionCodeParam.ToLower();
            return regionCode switch
            {
                "id" => "noctua-translation.id",
                _ => "noctua-translation.en"
            };
        }

        public static Dictionary<string, string> LoadTranslations(string regionCode)
        {
            TextAsset jsonFile = Resources.Load<TextAsset>(GetTranslationByLanguage(regionCode));
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonFile.text);
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
                    label.text = labelTranslation;
                    break;
                case Button button:
                    string buttonKey = $"{uxmlName}.{elementName}.{elementType}.text";
                    string buttonTranslation = GetTranslation(buttonKey, translations);

                    if(buttonTranslation != buttonKey)
                    {
                        button.text = buttonTranslation;
                    }
                    
                    // Recursively apply translations to child elements within the Button
                    foreach (var child in button.Children())
                    {
                        ApplyTranslationsToElement(child, uxmlName, translations);
                    }
                    break;
                case TextField textField:
                    string textFieldKey = $"{uxmlName}.{elementName}.{elementType}.label";
                    string textFieldTranslation = GetTranslation(textFieldKey, translations);
                    textField.label = textFieldTranslation;
                    break;
                case VisualElement visualElement:
                    // If it's a VisualElement, just log that it's a container and continue with child elements
                    Debug.Log($"Handling VisualElement: {elementName} ({elementType})");

                    // Recursively apply translations to all child elements
                    foreach (var child in visualElement.Children())
                    {
                        ApplyTranslationsToElement(child, uxmlName, translations);
                    }
                    break;
                default:
                    Debug.Log($"Unhandled element type: {elementType}");
                    break;
            }
        }
    }
}