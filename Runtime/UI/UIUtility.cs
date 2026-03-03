using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// UI-specific utility methods extracted from <see cref="Utility"/>.
    /// These depend on <c>UnityEngine.UIElements</c> and belong in the UI layer.
    /// </summary>
    internal static class UIUtility
    {
        private static readonly ILogger _sLog = new NoctuaLogger(typeof(UIUtility));

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
                    _sLog.Warning($"Element with name '{elementName}' not found.");
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

        public static void ApplyTranslations(VisualElement root, string uxmlName, Dictionary<string, string> translations)
        {
            ApplyTranslationsToElement(root, uxmlName, translations);
            Utility.ApplyErrorTranslation(translations);
        }

        private static void ApplyTranslationsToElement(VisualElement element, string uxmlName, Dictionary<string, string> translations)
        {
            string elementName = element.name ?? string.Empty;
            string elementType = element.GetType().Name;

            switch (element)
            {
                case Label label:
                    string labelKey = $"{uxmlName}.{elementName}.{elementType}.text";
                    string labelTranslation = Utility.GetTranslation(labelKey, translations);

                    if (labelTranslation != labelKey)
                    {
                        label.text = labelTranslation;
                    }
                    break;
                case Button button:
                    string buttonKey = $"{uxmlName}.{elementName}.{elementType}.text";
                    string buttonTranslation = Utility.GetTranslation(buttonKey, translations);

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
                    string textFieldTranslation = Utility.GetTranslation(textFieldKey, translations);
                    textField.label = textFieldTranslation;

                    Label textFieldTitle = textField.Q<Label>("title");

                    if (textFieldTitle != null) textFieldTitle.text = textFieldTranslation;

                    break;
                case DropdownField dropdownField:
                    string dropdownFieldKey = $"{uxmlName}.{elementName}.{elementType}.label";
                    string dropdownFieldTranslation = Utility.GetTranslation(dropdownFieldKey, translations);
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
    }
}
