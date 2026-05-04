using com.noctuagames.sdk;
using com.noctuagames.sdk.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

// Official Unity UI Toolkit test pattern (Unity 2021.3+):
//   - Plain [Test] + detached VisualElement root for class-list, hierarchy, query, value tests.
//   - resolvedStyle / event-dispatch tests require [UnityTest] + UIDocument (not needed here).
//   - No public UIToolkitTestHelpers base class exists; standard NUnit assertions apply.
//   - Q<>(), ClassListContains(), .text, .value all work on detached elements.
//   - See: Unity Test Framework docs, com.unity.ui.builder/Tests for reference.

namespace Tests.Runtime.UI
{
    // ─── ColorModule ────────────────────────────────────────────────────────────

    [TestFixture]
    public class ColorModuleTests
    {
        [Test]
        public void White_IsUnityWhite()
        {
            Assert.AreEqual(Color.white, ColorModule.white);
        }

        [Test]
        public void BlueButton_HasExpectedRgb()
        {
            var c = ColorModule.blueButton;
            Assert.That(c.r, Is.EqualTo(0.2313726f).Within(0.0001f));
            Assert.That(c.g, Is.EqualTo(0.509804f) .Within(0.0001f));
            Assert.That(c.b, Is.EqualTo(0.9647059f).Within(0.0001f));
            Assert.That(c.a, Is.EqualTo(1.0f)      .Within(0.0001f));
        }

        [Test]
        public void GreyInactive_HasExpectedRgb()
        {
            var c = ColorModule.greyInactive;
            Assert.That(c.r, Is.EqualTo(0.4862745f).Within(0.0001f));
            Assert.That(c.g, Is.EqualTo(0.4941176f).Within(0.0001f));
            Assert.That(c.b, Is.EqualTo(0.5058824f).Within(0.0001f));
            Assert.That(c.a, Is.EqualTo(1.0f)      .Within(0.0001f));
        }

        [Test]
        public void RedError_HasExpectedRgb()
        {
            var c = ColorModule.redError;
            Assert.That(c.r, Is.EqualTo(0.6862745f).Within(0.0001f));
            Assert.That(c.g, Is.EqualTo(0.1098039f).Within(0.0001f));
            Assert.That(c.b, Is.EqualTo(0.2117647f).Within(0.0001f));
            Assert.That(c.a, Is.EqualTo(1.0f)      .Within(0.0001f));
        }

        [Test]
        public void GreenSuccess_HasExpectedRgb()
        {
            var c = ColorModule.greenSuccess;
            Assert.That(c.r, Is.EqualTo(0.09019608f).Within(0.0001f));
            Assert.That(c.g, Is.EqualTo(0.6392157f) .Within(0.0001f));
            Assert.That(c.b, Is.EqualTo(0.2901961f) .Within(0.0001f));
            Assert.That(c.a, Is.EqualTo(1.0f)       .Within(0.0001f));
        }

        [Test]
        public void RedFailed_HasExpectedRgb()
        {
            var c = ColorModule.redFailed;
            Assert.That(c.r, Is.EqualTo(0.7882353f).Within(0.0001f));
            Assert.That(c.g, Is.EqualTo(0.3058824f).Within(0.0001f));
            Assert.That(c.b, Is.EqualTo(0.3058824f).Within(0.0001f));
            Assert.That(c.a, Is.EqualTo(1.0f)      .Within(0.0001f));
        }

        [Test]
        public void AllColors_AlphaIsOne()
        {
            Assert.AreEqual(1f, ColorModule.white.a);
            Assert.AreEqual(1f, ColorModule.blueButton.a);
            Assert.AreEqual(1f, ColorModule.greyInactive.a);
            Assert.AreEqual(1f, ColorModule.redError.a);
            Assert.AreEqual(1f, ColorModule.greenSuccess.a);
            Assert.AreEqual(1f, ColorModule.redFailed.a);
        }
    }

    // ─── Presenter<T>.ButtonNoctua ───────────────────────────────────────────────

    [TestFixture]
    public class ButtonNoctuaTests
    {
        // Shared detached root — the official pattern for EditMode VisualElement tests.
        private VisualElement _root;
        private Button _button;
        private Label _errLabel;
        private VisualElement _spinnerVE;
        private Presenter<object>.ButtonNoctua _sut;

        [SetUp]
        public void SetUp()
        {
            _root = new VisualElement();

            // ButtonNoctua queries `button.parent` for siblings named "ErrCode" / "Spinner".
            var parent   = new VisualElement();
            _button      = new Button();
            _errLabel    = new Label { name = "ErrCode" };
            _spinnerVE   = new VisualElement { name = "Spinner" };

            parent.Add(_button);
            parent.Add(_errLabel);
            parent.Add(_spinnerVE);
            _root.Add(parent);

            _sut = new Presenter<object>.ButtonNoctua(_button);
        }

        [TearDown]
        public void TearDown()
        {
            _root = null;
        }

        [Test]
        public void Constructor_SetsButtonReference()
        {
            Assert.AreSame(_button, _sut.button);
        }

        [Test]
        public void Constructor_FindsErrorLabel()
        {
            Assert.AreSame(_errLabel, _sut.labelError);
        }

        [Test]
        public void Constructor_FindsSpinnerVE()
        {
            Assert.AreSame(_spinnerVE, _sut.veSpinner);
        }

        [Test]
        public void AfterConstruction_ErrorLabel_HasHideClass()
        {
            Assert.IsTrue(_sut.labelError.ClassListContains("hide"));
        }

        [Test]
        public void ToggleLoading_True_HidesButtonAndShowsSpinner()
        {
            _sut.ToggleLoading(true);

            Assert.IsTrue(_button.ClassListContains("hide"),    "button should be hidden");
            Assert.IsFalse(_spinnerVE.ClassListContains("hide"), "spinner should be visible");
        }

        [Test]
        public void ToggleLoading_False_ShowsButtonAndHidesSpinner()
        {
            _sut.ToggleLoading(true);
            _sut.ToggleLoading(false);

            Assert.IsFalse(_button.ClassListContains("hide"),  "button should be visible");
            Assert.IsTrue(_spinnerVE.ClassListContains("hide"), "spinner should be hidden");
        }

        [Test]
        public void ToggleLoading_Idempotent_SameFlagTwice()
        {
            _sut.ToggleLoading(true);
            _sut.ToggleLoading(true);
            Assert.IsTrue(_button.ClassListContains("hide"));

            _sut.ToggleLoading(false);
            _sut.ToggleLoading(false);
            Assert.IsFalse(_button.ClassListContains("hide"));
        }

        [Test]
        public void Error_SetsLabelTextAndRemovesHide()
        {
            _sut.Error("Network timeout");

            Assert.AreEqual("Network timeout", _sut.labelError.text);
            Assert.IsFalse(_sut.labelError.ClassListContains("hide"));
        }

        [Test]
        public void Clear_AfterError_HidesLabel()
        {
            _sut.Error("oops");
            _sut.Clear();

            Assert.IsTrue(_sut.labelError.ClassListContains("hide"));
        }

        [Test]
        public void Error_ThenClear_ThenError_ShowsNewMessage()
        {
            _sut.Error("first");
            _sut.Clear();
            _sut.Error("second");

            Assert.AreEqual("second", _sut.labelError.text);
            Assert.IsFalse(_sut.labelError.ClassListContains("hide"));
        }
    }

    // ─── Presenter<T>.InputFieldNoctua ──────────────────────────────────────────

    [TestFixture]
    public class InputFieldNoctuaTests
    {
        private VisualElement _root;
        private TextField _textField;
        private Label _titleLabel;
        private Label _errorLabel;
        private Presenter<object>.InputFieldNoctua _sut;

        [SetUp]
        public void SetUp()
        {
            _root = new VisualElement();

            _textField  = new TextField();
            _titleLabel = new Label { name = "title" };
            _errorLabel = new Label { name = "error" };

            _textField.Add(_titleLabel);
            _textField.Add(_errorLabel);

            // TextField creates "unity-text-input" internally; add a stub only if absent.
            if (_textField.Q("unity-text-input") == null)
                _textField.Add(new VisualElement { name = "unity-text-input" });

            _root.Add(_textField);

            _sut = new Presenter<object>.InputFieldNoctua(_textField);
        }

        [TearDown]
        public void TearDown()
        {
            _root = null;
        }

        [Test]
        public void Constructor_ExposesTextFieldReference()
        {
            Assert.AreSame(_textField, _sut.textField);
        }

        [Test]
        public void Text_ReflectsTextFieldValue()
        {
            _textField.value = "hello";
            Assert.AreEqual("hello", _sut.text);
        }

        [Test]
        public void Clear_EmptiesValue()
        {
            _textField.value = "existing text";
            _sut.Clear();
            Assert.AreEqual(string.Empty, _sut.text);
        }

        [Test]
        public void ToggleTitle_True_RemovesHideClass()
        {
            _sut.labelTitle.AddToClassList("hide");
            _sut.ToggleTitle(true);
            Assert.IsFalse(_sut.labelTitle.ClassListContains("hide"));
        }

        [Test]
        public void ToggleTitle_False_AddsHideClass()
        {
            _sut.labelTitle.RemoveFromClassList("hide");
            _sut.ToggleTitle(false);
            Assert.IsTrue(_sut.labelTitle.ClassListContains("hide"));
        }

        [Test]
        public void AdjustLabel_WhenEmpty_HidesTitleLabel()
        {
            _textField.value = string.Empty;
            _sut.ToggleTitle(true);
            _sut.AdjustLabel();
            Assert.IsTrue(_sut.labelTitle.ClassListContains("hide"));
        }

        [Test]
        public void AdjustLabel_WhenNonEmpty_ShowsTitleLabel()
        {
            _textField.value = "text";
            _sut.ToggleTitle(false);
            _sut.AdjustLabel();
            Assert.IsFalse(_sut.labelTitle.ClassListContains("hide"));
        }

        [Test]
        public void Error_SetsTextAndShowsErrorLabel()
        {
            _sut.Error("Required field");
            Assert.AreEqual("Required field", _sut.labelError.text);
            Assert.IsFalse(_sut.labelError.ClassListContains("hide"));
        }

        [Test]
        public void Error_AddsErrorCssClassToTextInput()
        {
            _sut.Error("bad input");
            Assert.IsTrue(_sut.veTextInput.ClassListContains("noctua-text-input-error"));
        }

        [Test]
        public void Reset_HidesErrorLabel()
        {
            _sut.Error("some error");
            _sut.Reset();
            Assert.IsTrue(_sut.labelError.ClassListContains("hide"));
        }

        [Test]
        public void Reset_RemovesErrorCssClassFromTextInput()
        {
            _sut.Error("bad input");
            _sut.Reset();
            Assert.IsFalse(_sut.veTextInput.ClassListContains("noctua-text-input-error"));
        }
    }
}
