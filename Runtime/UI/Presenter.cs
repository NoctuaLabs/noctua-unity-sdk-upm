using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public abstract class Presenter<TModel> : MonoBehaviour
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(Presenter<TModel>));
        protected TModel Model;
        protected VisualElement View;
        protected NoctuaLocale Locale;

        private UIDocument _uiDoc;

        public virtual bool Visible
        {
            get => View.visible;
            set
            {
                _log.Debug(value ? $"showing {_uiDoc.visualTreeAsset.name}" : $"hiding {_uiDoc.visualTreeAsset.name}");

                View.visible = value;
            }
        }

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

        protected virtual void Attach()
        {
        }

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
            Utility.ApplyTranslations(View, GetType().Name, Locale.GetTranslations());
        }

        public class ButtonNoctua
        {
            public Button button { get; }
            public VisualElement veSpinner { get; }
            public Label labelError { get; }

            public ButtonNoctua (Button _button)
            {
                button = _button;
                labelError = button.parent.Q<Label>("ErrCode");
                veSpinner = button.parent.Q<VisualElement>("Spinner");

                if (veSpinner != null)
                {
                    if (veSpinner.childCount == 0)
                    {
                        veSpinner.Add(new Spinner(30, 30));
                    }
                }

                Clear();
            }

            public void Clear()
            {
                labelError?.AddToClassList("hide");
                ToggleLoading(false);
            }

            public void ToggleLoading(bool _isLoading)
            {
                if (_isLoading)
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

            public void Error(string _strMessage)
            {
                labelError.text = _strMessage;                
                labelError.RemoveFromClassList("hide");               
            }
        }

        public class InputFieldNoctua
        {
            public TextField textField { get; }
            public VisualElement veTextInput { get; }
            public Label labelTitle { get; }
            public Label labelError { get; }
            public string text { get { return textField.text; } }

            private UnityAction onFocusIn;
            private UnityAction onFocusOut;

            public InputFieldNoctua(TextField _textField)
            {
                textField = _textField;
                veTextInput = textField.Q("unity-text-input");
                labelTitle = textField.Q<Label>("title");
                labelError = textField.Q<Label>("error");

                textField.hideMobileInput = true;

                Clear();                
            }

            public void SetFocus(UnityAction _onFocusIn = null, UnityAction _onFocusOut = null)
            {
                onFocusIn = _onFocusIn;
                onFocusOut = _onFocusOut;

                textField.RegisterCallback<FocusInEvent>(OnFocusChange);
                textField.RegisterCallback<FocusOutEvent>(OnFocusChange);
            }

            public void OnFocusChange(FocusInEvent _event)
            {                
                onFocusIn?.Invoke();
                Reset();

                veTextInput.AddToClassList("noctua-text-input-focus");
                labelTitle.style.color = ColorModule.white;                
            }

            public void OnFocusChange(FocusOutEvent _event)
            {                
                onFocusOut?.Invoke();
                veTextInput.RemoveFromClassList("noctua-text-input-focus");
                labelTitle.style.color = ColorModule.greyInactive;                
            }

            public void Clear()
            {
                textField.value = string.Empty;
                Reset();
            }

            public void Reset()
            {
                veTextInput.RemoveFromClassList("noctua-text-input-error");
                labelError.AddToClassList("hide");
                labelTitle.style.color = ColorModule.greyInactive;
            }

            public void Error(string _strMessage)
            {
                labelError.text = _strMessage;
                veTextInput.AddToClassList("noctua-text-input-error");
                labelError.RemoveFromClassList("hide");                
                labelTitle.style.color = ColorModule.redError;
            }

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

            public void ToggleTitle(bool _isShow)
            {
                if (_isShow)
                {
                    labelTitle.RemoveFromClassList("hide");
                }
                else
                {
                    labelTitle.AddToClassList("hide");
                }
            }
        }
    }
}