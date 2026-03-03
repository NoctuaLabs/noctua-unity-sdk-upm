using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Abstract base class for all UI presenters in the Noctua SDK.
    /// Provides model binding, view loading from UXML resources, localization support, and keyboard-aware layout adjustments.
    /// </summary>
    /// <typeparam name="TModel">The type of the model this presenter is bound to.</typeparam>
    public abstract class Presenter<TModel> : MonoBehaviour
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(Presenter<TModel>));

        /// <summary>The data model bound to this presenter.</summary>
        protected TModel Model;

        /// <summary>The root visual element of this presenter's UI document.</summary>
        protected VisualElement View;

        /// <summary>The locale provider used for translations.</summary>
        protected NoctuaLocale Locale;

        /// <summary>The dialog panel visual element, used to apply keyboard-aware CSS classes.</summary>
        protected VisualElement panelVE;

        private UIDocument _uiDoc;

        /// <summary>
        /// Gets or sets the visibility of this presenter's UI.
        /// </summary>
        public virtual bool Visible
        {
            get => View.visible;
            set
            {
                _log.Debug(value ? $"showing {_uiDoc.visualTreeAsset.name}" : $"hiding {_uiDoc.visualTreeAsset.name}");

                View.visible = value;
            }
        }

        /// <summary>
        /// Initializes the presenter by loading the UXML view, binding the model, and subscribing to language changes.
        /// </summary>
        /// <param name="model">The model to bind to this presenter.</param>
        /// <param name="panelSettings">The UI Toolkit panel settings for the UIDocument.</param>
        /// <param name="locale">The locale provider for translations.</param>
        public void Init(TModel model, PanelSettings panelSettings, NoctuaLocale locale)
        {
            LoadView(panelSettings);
            SetModel(model);
            Locale = locale;
            Locale.OnLanguageChanged += OnLanguageChanged;
        }

        private void SetModel(TModel model)
        {
            if (Model is not null)
            {
                Detach();
            }

            Model = model;

            if (Model is not null)
            {
                Attach();
            }
        }


        protected virtual void Update()
        {
            if (panelVE == null) return;

            if (TouchScreenKeyboard.visible && !panelVE.ClassListContains("dialog-box-keyboard-shown"))
            {
                panelVE.AddToClassList("dialog-box-keyboard-shown");
            }

            if (!TouchScreenKeyboard.visible && panelVE.ClassListContains("dialog-box-keyboard-shown"))
            {
                panelVE.RemoveFromClassList("dialog-box-keyboard-shown");
            }
        }

        /// <summary>
        /// Called when a new model is assigned. Override to subscribe to model events.
        /// </summary>
        protected virtual void Attach()
        {
        }

        /// <summary>
        /// Called before the model is replaced. Override to unsubscribe from model events.
        /// </summary>
        protected virtual void Detach()
        {
        }

        private void LoadView(PanelSettings panelSettings)
        {
            var viewResourceName = GetType().Name.Replace("Presenter", "");

            _uiDoc = gameObject.AddComponent<UIDocument>();

            var visualTreeAsset = Resources.Load<VisualTreeAsset>(viewResourceName);

            if (visualTreeAsset is null)
            {
                _log.Error($"View not found for {viewResourceName}");
            }

            _uiDoc.visualTreeAsset = visualTreeAsset ?? throw new ArgumentNullException(nameof(visualTreeAsset));
            View = _uiDoc.rootVisualElement;
            View.visible = false;

            _uiDoc.panelSettings = panelSettings ?? throw new ArgumentNullException(nameof(panelSettings));
        }

        private void OnLanguageChanged(string language)
        {
            UIUtility.ApplyTranslations(View, GetType().Name, Locale.GetTranslations());
        }

        protected virtual void OnDestroy()
        {
            if (Locale != null)
            {
                Locale.OnLanguageChanged -= OnLanguageChanged;
            }
        }

        /// <summary>
        /// Wrapper around a UI Toolkit <see cref="Button"/> that provides built-in loading spinner and error label support.
        /// </summary>
        public class ButtonNoctua
        {
            /// <summary>The underlying UI Toolkit button element.</summary>
            public Button button { get; }

            /// <summary>The spinner visual element shown during loading state.</summary>
            public VisualElement veSpinner { get; }

            /// <summary>The error label displayed below the button when an error occurs.</summary>
            public Label labelError { get; }

            /// <summary>
            /// Initializes a new ButtonNoctua by locating the sibling spinner and error label elements.
            /// </summary>
            /// <param name="button">The UI Toolkit button to wrap.</param>
            public ButtonNoctua(Button button)
            {
                this.button = button;
                labelError = this.button.parent.Q<Label>("ErrCode");
                veSpinner = this.button.parent.Q<VisualElement>("Spinner");

                if (veSpinner != null)
                {
                    if (veSpinner.childCount == 0)
                    {
                        veSpinner.Add(new Spinner(30, 30));
                    }
                }

                Clear();
            }

            /// <summary>
            /// Resets the button to its default state by hiding the error label and stopping the loading spinner.
            /// </summary>
            public void Clear()
            {
                labelError?.AddToClassList("hide");
                ToggleLoading(false);
            }

            /// <summary>
            /// Toggles between the button and loading spinner visibility.
            /// </summary>
            /// <param name="isLoading"><c>true</c> to show spinner and hide button; <c>false</c> for the reverse.</param>
            public void ToggleLoading(bool isLoading)
            {
                if (isLoading)
                {
                    button.AddToClassList("hide");
                    veSpinner.RemoveFromClassList("hide");
                }
                else
                {
                    button.RemoveFromClassList("hide");
                    veSpinner.AddToClassList("hide");
                }
            }

            /// <summary>
            /// Displays an error message below the button.
            /// </summary>
            /// <param name="strMessage">The error message text to display.</param>
            public void Error(string strMessage)
            {
                labelError.text = strMessage;
                labelError.RemoveFromClassList("hide");
            }
        }

        /// <summary>
        /// Wrapper around a UI Toolkit <see cref="TextField"/> that provides focus styling, floating title labels, and error display.
        /// </summary>
        public class InputFieldNoctua
        {
            /// <summary>The underlying UI Toolkit text field element.</summary>
            public TextField textField { get; }

            /// <summary>The inner text input visual element used for border styling.</summary>
            public VisualElement veTextInput { get; }

            /// <summary>The floating title label above the input field.</summary>
            public Label labelTitle { get; }

            /// <summary>The error label displayed when validation fails.</summary>
            public Label labelError { get; }

            /// <summary>Gets the current text value of the input field.</summary>
            public string text { get { return textField.text; } }

            private UnityAction _onFocusIn;
            private UnityAction _onFocusOut;

            /// <summary>
            /// Initializes a new InputFieldNoctua by locating the inner text input, title, and error label elements.
            /// </summary>
            /// <param name="textField">The UI Toolkit text field to wrap.</param>
            public InputFieldNoctua(TextField textField)
            {
                this.textField = textField;
                veTextInput = this.textField.Q("unity-text-input");
                labelTitle = this.textField.Q<Label>("title");
                labelError = this.textField.Q<Label>("error");

                this.textField.hideMobileInput = true;

                Clear();
            }

            /// <summary>
            /// Registers focus-in and focus-out callbacks for the text field.
            /// </summary>
            /// <param name="onFocusIn">Optional callback invoked when the field gains focus.</param>
            /// <param name="onFocusOut">Optional callback invoked when the field loses focus.</param>
            public void SetFocus(UnityAction onFocusIn = null, UnityAction onFocusOut = null)
            {
                this._onFocusIn = onFocusIn;
                this._onFocusOut = onFocusOut;

                textField.RegisterCallback<FocusInEvent>(OnFocusChange);
                textField.RegisterCallback<FocusOutEvent>(OnFocusChange);
            }

            /// <summary>
            /// Handles focus-in by applying the focused style and resetting error state.
            /// </summary>
            public void OnFocusChange(FocusInEvent evt)
            {
                _onFocusIn?.Invoke();
                Reset();

                veTextInput.AddToClassList("noctua-text-input-focus");
                labelTitle.style.color = ColorModule.white;
            }

            /// <summary>
            /// Handles focus-out by removing the focused style.
            /// </summary>
            public void OnFocusChange(FocusOutEvent evt)
            {
                _onFocusOut?.Invoke();
                veTextInput.RemoveFromClassList("noctua-text-input-focus");
                labelTitle.style.color = ColorModule.greyInactive;
            }

            /// <summary>
            /// Clears the text field value and resets all visual states.
            /// </summary>
            public void Clear()
            {
                textField.value = string.Empty;
                Reset();
            }

            /// <summary>
            /// Resets the error state and border styling without clearing the text value.
            /// </summary>
            public void Reset()
            {
                veTextInput.RemoveFromClassList("noctua-text-input-error");
                labelError.AddToClassList("hide");
                labelTitle.style.color = ColorModule.greyInactive;
            }

            /// <summary>
            /// Displays an error message and applies error border styling to the input field.
            /// </summary>
            /// <param name="strMessage">The error message text to display.</param>
            public void Error(string strMessage)
            {
                labelError.text = strMessage;
                veTextInput.AddToClassList("noctua-text-input-error");
                labelError.RemoveFromClassList("hide");
                labelTitle.style.color = ColorModule.redError;
            }

            /// <summary>
            /// Adjusts the floating title label visibility based on whether the field has content.
            /// </summary>
            public void AdjustLabel()
            {
                if (string.IsNullOrEmpty(textField.value))
                {
                    textField.labelElement.style.display = DisplayStyle.Flex;
                    ToggleTitle(false);
                }
                else
                {
                    textField.labelElement.style.display = DisplayStyle.None;
                    ToggleTitle(true);
                }
            }

            /// <summary>
            /// Shows or hides the floating title label.
            /// </summary>
            /// <param name="isShow"><c>true</c> to show the title; <c>false</c> to hide it.</param>
            public void ToggleTitle(bool isShow)
            {
                if (isShow)
                {
                    labelTitle.RemoveFromClassList("hide");
                }
                else
                {
                    labelTitle.AddToClassList("hide");
                }
            }
        }

        /// <summary>
        /// Wrapper around a UI Toolkit <see cref="DropdownField"/> that provides focus styling, title labels, and error display.
        /// </summary>
        public class DropdownNoctua
        {
            /// <summary>The underlying UI Toolkit dropdown field element.</summary>
            public DropdownField dropdownField { get; }

            /// <summary>The border visual element used for focus and error styling.</summary>
            public VisualElement veBorder { get; }

            /// <summary>The floating title label above the dropdown.</summary>
            public Label labelTitle { get; }

            /// <summary>The error label displayed when validation fails.</summary>
            public Label labelError { get; }

            /// <summary>Gets the current display text of the dropdown.</summary>
            public string text { get { return dropdownField.text; } }

            private UnityAction _onFocus;            

            /// <summary>Gets or sets the currently selected dropdown value.</summary>
            public string value
            {
                get
                {
                    return dropdownField.value;
                }

                set
                {
                    dropdownField.value = value;
                }
            }

            /// <summary>
            /// Initializes a new DropdownNoctua by locating the border, title, and error label elements.
            /// </summary>
            /// <param name="dropdownField">The UI Toolkit dropdown field to wrap.</param>
            public DropdownNoctua(DropdownField dropdownField)
            {
                this.dropdownField = dropdownField;
                veBorder = this.dropdownField.ElementAt(1);
                labelTitle = this.dropdownField.Q<Label>("title");
                labelError = this.dropdownField.Q<Label>("error");

                Clear();
            }

            /// <summary>
            /// Populates the dropdown with the given options and registers a change callback.
            /// </summary>
            /// <param name="listOptions">The list of option strings to display.</param>
            public void SetupList(List<string> listOptions)
            {
                dropdownField.choices = listOptions;

                dropdownField.RegisterCallback<ChangeEvent<string>>(OnChangeString);
            }

            /// <summary>
            /// Handles value change events by updating the selected value and showing the title label.
            /// </summary>
            public void OnChangeString(ChangeEvent<string> evt)
            {
                dropdownField.value = evt.newValue;
                dropdownField.labelElement.style.display = DisplayStyle.None;
                ToggleTitle(true);
            }

            /// <summary>
            /// Resets the dropdown to its default visual state.
            /// </summary>
            public void Clear()
            {
                Reset();
            }

            /// <summary>
            /// Removes error styling and hides the error label.
            /// </summary>
            public void Reset()
            {
                veBorder.RemoveFromClassList("noctua-text-input-error");
                labelError.AddToClassList("hide");
                labelTitle.style.color = ColorModule.greyInactive;
            }

            /// <summary>
            /// Registers a focus callback for the dropdown.
            /// </summary>
            /// <param name="onFocus">Optional callback invoked on focus events.</param>
            public void SetFocus(UnityAction onFocus = null)
            {
                this._onFocus = onFocus;                

                dropdownField.RegisterCallback<MouseDownEvent>(OnMouseDown);
                dropdownField.RegisterCallback<FocusOutEvent>(OnFocusChange);
            }

            /// <summary>
            /// Handles mouse-down by applying the focused border style.
            /// </summary>
            public void OnMouseDown(MouseDownEvent evt)
            {
                //_onFocus?.Invoke();
                Reset();

                veBorder.AddToClassList("noctua-text-input-focus");
                labelTitle.style.color = ColorModule.white;
            }

            /// <summary>
            /// Handles focus-out by invoking the registered focus callback.
            /// </summary>
            public void OnFocusChange(FocusOutEvent evt)
            {
                _onFocus?.Invoke();
                //veBorder.RemoveFromClassList("noctua-text-input-focus");
                //labelTitle.style.color = ColorModule.greyInactive;
            }

            /// <summary>
            /// Shows or hides the floating title label.
            /// </summary>
            /// <param name="isShow"><c>true</c> to show the title; <c>false</c> to hide it.</param>
            public void ToggleTitle(bool isShow)
            {
                if (isShow)
                {
                    labelTitle.RemoveFromClassList("hide");
                }
                else
                {
                    labelTitle.AddToClassList("hide");
                }
            }

            /// <summary>
            /// Displays an error message and applies error border styling to the dropdown.
            /// </summary>
            /// <param name="strMessage">The error message text to display.</param>
            public void Error(string strMessage)
            {
                labelError.text = strMessage;
                veBorder.AddToClassList("noctua-text-input-error");
                labelError.RemoveFromClassList("hide");
                labelTitle.style.color = ColorModule.redError;
            }
        }
    }
}