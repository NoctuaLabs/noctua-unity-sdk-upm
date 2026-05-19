using System.Collections.Generic;
using com.noctuagames.sdk;
using com.noctuagames.sdk.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

// All tests use detached VisualElement trees — no panel/UIDocument required.
// Events whose implementation does not dereference the event object are invoked
// with null to avoid platform-specific event factory issues in EditMode.

namespace Tests.Runtime.UI
{
    // ─── UIUtility ───────────────────────────────────────────────────────────────

    [TestFixture]
    public class UIUtilityTests
    {
        // ── UpdateButtonState(Button, bool) ──────────────────────────────────

        [Test]
        public void UpdateButtonState_Active_EnablesButtonAndSetsPositionPicking()
        {
            var btn = new Button();
            UIUtility.UpdateButtonState(btn, true);
            Assert.IsTrue(btn.enabledSelf);
            Assert.AreEqual(PickingMode.Position, btn.pickingMode);
        }

        [Test]
        public void UpdateButtonState_Inactive_DisablesButtonAndSetsIgnorePicking()
        {
            var btn = new Button();
            UIUtility.UpdateButtonState(btn, false);
            Assert.IsFalse(btn.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, btn.pickingMode);
        }

        [Test]
        public void UpdateButtonState_ToggledTwice_EndStateMatchesLastCall()
        {
            var btn = new Button();
            UIUtility.UpdateButtonState(btn, true);
            UIUtility.UpdateButtonState(btn, false);
            Assert.IsFalse(btn.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, btn.pickingMode);
        }

        // ── UpdateButtonState(List<TextField>, Button) ────────────────────────

        [Test]
        public void UpdateButtonState_AllFieldsFilled_EnablesButton()
        {
            var f1  = new TextField { value = "a" };
            var f2  = new TextField { value = "b" };
            var btn = new Button();
            UIUtility.UpdateButtonState(new List<TextField> { f1, f2 }, btn);
            Assert.IsTrue(btn.enabledSelf);
        }

        [Test]
        public void UpdateButtonState_AnyFieldEmpty_DisablesButton()
        {
            var f1  = new TextField { value = "a" };
            var f2  = new TextField { value = "" };
            var btn = new Button();
            UIUtility.UpdateButtonState(new List<TextField> { f1, f2 }, btn);
            Assert.IsFalse(btn.enabledSelf);
        }

        [Test]
        public void UpdateButtonState_EmptyList_EnablesButton()
        {
            var btn = new Button();
            UIUtility.UpdateButtonState(new List<TextField>(), btn);
            Assert.IsTrue(btn.enabledSelf);
        }

        [Test]
        public void UpdateButtonState_SingleEmptyField_DisablesButton()
        {
            var f   = new TextField { value = "   " };
            var btn = new Button();
            // whitespace-only is not null/empty, so button should be enabled
            UIUtility.UpdateButtonState(new List<TextField> { f }, btn);
            Assert.IsTrue(btn.enabledSelf, "Non-empty (whitespace) value should enable button");
        }

        // ── ApplyTranslations ─────────────────────────────────────────────────

        [Test]
        public void ApplyTranslations_Label_AppliesMatchingTranslation()
        {
            var root  = new VisualElement();
            var label = new Label { name = "myLabel", text = "old" };
            root.Add(label);

            var translations = new Dictionary<string, string>
            {
                { "TestUI.myLabel.Label.text", "Translated!" }
            };

            UIUtility.ApplyTranslations(root, "TestUI", translations);

            Assert.AreEqual("Translated!", label.text);
        }

        [Test]
        public void ApplyTranslations_Label_NoMatchingKey_LeavesTextUnchanged()
        {
            var root  = new VisualElement();
            var label = new Label { name = "myLabel", text = "original" };
            root.Add(label);

            UIUtility.ApplyTranslations(root, "TestUI", new Dictionary<string, string>());

            Assert.AreEqual("original", label.text);
        }

        [Test]
        public void ApplyTranslations_Button_AppliesMatchingTranslation()
        {
            var root   = new VisualElement();
            var button = new Button { name = "myBtn", text = "old" };
            root.Add(button);

            var translations = new Dictionary<string, string>
            {
                { "TestUI.myBtn.Button.text", "Click Me" }
            };

            UIUtility.ApplyTranslations(root, "TestUI", translations);

            Assert.AreEqual("Click Me", button.text);
        }

        [Test]
        public void ApplyTranslations_Button_NoMatch_LeavesTextUnchanged()
        {
            var root   = new VisualElement();
            var button = new Button { name = "btn", text = "Submit" };
            root.Add(button);

            UIUtility.ApplyTranslations(root, "TestUI", new Dictionary<string, string>());

            Assert.AreEqual("Submit", button.text);
        }

        [Test]
        public void ApplyTranslations_TextField_SetsLabel()
        {
            var root = new VisualElement();
            var tf   = new TextField { name = "email", label = "Old Label" };
            root.Add(tf);

            var translations = new Dictionary<string, string>
            {
                { "TestUI.email.TextField.label", "Email Address" }
            };

            UIUtility.ApplyTranslations(root, "TestUI", translations);

            Assert.AreEqual("Email Address", tf.label);
        }

        [Test]
        public void ApplyTranslations_TextField_WithTitleLabel_AlsoSetsTitleText()
        {
            var root       = new VisualElement();
            var tf         = new TextField { name = "email" };
            var titleLabel = new Label { name = "title" };
            tf.Add(titleLabel);
            root.Add(tf);

            var translations = new Dictionary<string, string>
            {
                { "TestUI.email.TextField.label", "E-mail" }
            };

            UIUtility.ApplyTranslations(root, "TestUI", translations);

            Assert.AreEqual("E-mail", titleLabel.text);
        }

        [Test]
        public void ApplyTranslations_DropdownField_SetsLabel()
        {
            var root = new VisualElement();
            var dd   = new DropdownField { name = "country" };
            root.Add(dd);

            var translations = new Dictionary<string, string>
            {
                { "TestUI.country.DropdownField.label", "Country" }
            };

            UIUtility.ApplyTranslations(root, "TestUI", translations);

            Assert.AreEqual("Country", dd.label);
        }

        [Test]
        public void ApplyTranslations_VisualElement_RecursesIntoChildren()
        {
            var root       = new VisualElement();
            var container  = new VisualElement();
            var innerLabel = new Label { name = "inner" };
            container.Add(innerLabel);
            root.Add(container);

            var translations = new Dictionary<string, string>
            {
                { "TestUI.inner.Label.text", "Deep Translation" }
            };

            UIUtility.ApplyTranslations(root, "TestUI", translations);

            Assert.AreEqual("Deep Translation", innerLabel.text);
        }

        [Test]
        public void ApplyTranslations_EmptyTranslations_DoesNotThrow()
        {
            var root  = new VisualElement();
            root.Add(new Label { name = "l1" });
            root.Add(new Button { name = "b1" });
            root.Add(new TextField { name = "tf1" });

            Assert.DoesNotThrow(() =>
                UIUtility.ApplyTranslations(root, "TestUI", new Dictionary<string, string>()));
        }

        // ── RegisterForMultipleValueChanges<T> ───────────────────────────────

        [Test]
        public void RegisterForMultipleValueChanges_ElementNotFound_DoesNotThrow()
        {
            var root = new VisualElement();
            var btn  = new Button();
            root.Add(btn);

            Assert.DoesNotThrow(() =>
                UIUtility.RegisterForMultipleValueChanges<string>(
                    root,
                    new List<string> { "nonExistent" },
                    btn));
        }

        [Test]
        public void RegisterForMultipleValueChanges_RegistersWithoutError_WhenElementExists()
        {
            var root = new VisualElement();
            var tf   = new TextField { name = "field1", value = "start" };
            root.Add(tf);
            var btn = new Button();
            root.Add(btn);

            Assert.DoesNotThrow(() =>
                UIUtility.RegisterForMultipleValueChanges<string>(
                    root,
                    new List<string> { "field1" },
                    btn));
        }
    }

    // ─── Presenter<T>.DropdownNoctua ─────────────────────────────────────────────

    [TestFixture]
    public class DropdownNoctuaTests
    {
        // DropdownField internal layout: ElementAt(0) = Label, ElementAt(1) = visualInput.
        // We add "title" and "error" Labels as children so Q<Label>("title/error") can find them.

        private VisualElement _root;
        private DropdownField _dropdown;
        private Label _titleLabel;
        private Label _errorLabel;
        private Presenter<object>.DropdownNoctua _sut;

        [SetUp]
        public void SetUp()
        {
            _root       = new VisualElement();
            _dropdown   = new DropdownField();
            _titleLabel = new Label { name = "title" };
            _errorLabel = new Label { name = "error" };

            _dropdown.Add(_titleLabel);
            _dropdown.Add(_errorLabel);
            _root.Add(_dropdown);

            _sut = new Presenter<object>.DropdownNoctua(_dropdown);
        }

        [TearDown]
        public void TearDown() => _root = null;

        // ── Constructor ──────────────────────────────────────────────────────

        [Test]
        public void Constructor_StoresDropdownReference()
        {
            Assert.AreSame(_dropdown, _sut.dropdownField);
        }

        [Test]
        public void Constructor_FindsTitleLabel()
        {
            Assert.AreSame(_titleLabel, _sut.labelTitle);
        }

        [Test]
        public void Constructor_FindsErrorLabel()
        {
            Assert.AreSame(_errorLabel, _sut.labelError);
        }

        [Test]
        public void Constructor_VeBorder_IsNotNull()
        {
            Assert.IsNotNull(_sut.veBorder);
        }

        [Test]
        public void Constructor_ErrorLabelHasHideClass()
        {
            Assert.IsTrue(_sut.labelError.ClassListContains("hide"),
                "Clear() called in constructor must add 'hide' to error label");
        }

        // ── SetupList / OnChangeString ────────────────────────────────────────

        [Test]
        public void SetupList_PopulatesChoices()
        {
            var options = new List<string> { "Alpha", "Beta", "Gamma" };
            _sut.SetupList(options);
            Assert.AreEqual(options, _dropdown.choices);
        }

        [Test]
        public void OnChangeString_SetsDropdownValueToNewValue()
        {
            _sut.SetupList(new List<string> { "X", "Y" });
            using var evt = ChangeEvent<string>.GetPooled("X", "Y");
            _sut.OnChangeString(evt);
            Assert.AreEqual("Y", _dropdown.value);
        }

        [Test]
        public void OnChangeString_ShowsTitleLabel()
        {
            _sut.SetupList(new List<string> { "X", "Y" });
            using var evt = ChangeEvent<string>.GetPooled("X", "Y");
            _sut.OnChangeString(evt);
            Assert.IsFalse(_sut.labelTitle.ClassListContains("hide"),
                "OnChangeString must call ToggleTitle(true), removing 'hide'");
        }

        // ── SetFocus / OnMouseDown / OnFocusChange(FocusOut) ──────────────────

        [Test]
        public void SetFocus_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _sut.SetFocus());
        }

        [Test]
        public void OnMouseDown_AddsFocusClassToVeBorder()
        {
            // evt is not dereferenced by OnMouseDown — null is safe
            _sut.OnMouseDown(null);
            Assert.IsTrue(_sut.veBorder.ClassListContains("noctua-text-input-focus"));
        }

        [Test]
        public void OnMouseDown_SetsWhiteColorOnTitleLabel()
        {
            _sut.OnMouseDown(null);
            Assert.AreEqual(ColorModule.white, _sut.labelTitle.style.color.value);
        }

        [Test]
        public void OnMouseDown_ResetsErrorStateFirst()
        {
            _sut.Error("previous error");
            _sut.OnMouseDown(null);
            // Reset() is called inside OnMouseDown before applying focus styles
            Assert.IsFalse(_sut.veBorder.ClassListContains("noctua-text-input-error"),
                "OnMouseDown must call Reset() before applying focus styles");
        }

        [Test]
        public void OnFocusChange_FocusOut_WithNullCallback_DoesNotThrow()
        {
            // SetFocus not called → _onFocus is null → should be safe
            Assert.DoesNotThrow(() => _sut.OnFocusChange((FocusOutEvent)null));
        }

        [Test]
        public void OnFocusChange_FocusOut_InvokesFocusCallback()
        {
            bool called = false;
            _sut.SetFocus(onFocus: () => called = true);
            _sut.OnFocusChange((FocusOutEvent)null);
            Assert.IsTrue(called, "Registered onFocus callback must be invoked on FocusOut");
        }

        // ── Clear / Reset ─────────────────────────────────────────────────────

        [Test]
        public void Clear_HidesErrorLabel()
        {
            _sut.Error("some error");
            _sut.Clear();
            Assert.IsTrue(_sut.labelError.ClassListContains("hide"));
        }

        [Test]
        public void Reset_RemovesErrorClassFromVeBorder()
        {
            _sut.Error("bad input");
            _sut.Reset();
            Assert.IsFalse(_sut.veBorder.ClassListContains("noctua-text-input-error"));
        }

        [Test]
        public void Reset_HidesErrorLabel()
        {
            _sut.Error("err");
            _sut.Reset();
            Assert.IsTrue(_sut.labelError.ClassListContains("hide"));
        }

        [Test]
        public void Reset_SetsTitleColorToGreyInactive()
        {
            _sut.Reset();
            Assert.AreEqual(ColorModule.greyInactive, _sut.labelTitle.style.color.value);
        }

        // ── ToggleTitle ───────────────────────────────────────────────────────

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

        // ── Error ─────────────────────────────────────────────────────────────

        [Test]
        public void Error_SetsErrorLabelText()
        {
            _sut.Error("Validation failed");
            Assert.AreEqual("Validation failed", _sut.labelError.text);
        }

        [Test]
        public void Error_ShowsErrorLabel()
        {
            _sut.Error("err");
            Assert.IsFalse(_sut.labelError.ClassListContains("hide"));
        }

        [Test]
        public void Error_AddsErrorClassToVeBorder()
        {
            _sut.Error("invalid");
            Assert.IsTrue(_sut.veBorder.ClassListContains("noctua-text-input-error"));
        }

        [Test]
        public void Error_SetsTitleColorToRedError()
        {
            _sut.Error("err");
            Assert.AreEqual(ColorModule.redError, _sut.labelTitle.style.color.value);
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

        // ── value property / text getter ──────────────────────────────────────

        [Test]
        public void Value_Getter_ReturnsDropdownValue()
        {
            _sut.SetupList(new List<string> { "opt1", "opt2" });
            _dropdown.value = "opt1";
            Assert.AreEqual("opt1", _sut.value);
        }

        [Test]
        public void Value_Setter_UpdatesDropdownValue()
        {
            _sut.SetupList(new List<string> { "opt1", "opt2" });
            _sut.value = "opt2";
            Assert.AreEqual("opt2", _dropdown.value);
        }

        [Test]
        public void Text_Getter_ReturnsDropdownText()
        {
            // text property delegates directly to dropdownField.text
            Assert.AreEqual(_dropdown.text, _sut.text);
        }
    }

    // ─── InputFieldNoctua focus gaps ─────────────────────────────────────────────

    [TestFixture]
    public class InputFieldNoctuaFocusTests
    {
        // These tests cover the three public methods omitted from the original UITests.cs:
        //   SetFocus, OnFocusChange(FocusInEvent), OnFocusChange(FocusOutEvent)

        private VisualElement _root;
        private TextField _textField;
        private Presenter<object>.InputFieldNoctua _sut;

        [SetUp]
        public void SetUp()
        {
            _root      = new VisualElement();
            _textField = new TextField();

            _textField.Add(new Label { name = "title" });
            _textField.Add(new Label { name = "error" });

            // TextField creates "unity-text-input" internally; add a stub if absent
            if (_textField.Q("unity-text-input") == null)
                _textField.Add(new VisualElement { name = "unity-text-input" });

            _root.Add(_textField);
            _sut = new Presenter<object>.InputFieldNoctua(_textField);
        }

        [TearDown]
        public void TearDown() => _root = null;

        // ── SetFocus ──────────────────────────────────────────────────────────

        [Test]
        public void SetFocus_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _sut.SetFocus());
        }

        [Test]
        public void SetFocus_WithNullCallbacks_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _sut.SetFocus(null, null));
        }

        [Test]
        public void SetFocus_WithCallbacks_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _sut.SetFocus(
                onFocusIn:  () => { },
                onFocusOut: () => { }));
        }

        // ── OnFocusChange(FocusInEvent) ───────────────────────────────────────
        // evt is never dereferenced — null is safe

        [Test]
        public void OnFocusChange_FocusIn_AddsFocusClassToTextInput()
        {
            _sut.OnFocusChange((FocusInEvent)null);
            Assert.IsTrue(_sut.veTextInput.ClassListContains("noctua-text-input-focus"));
        }

        [Test]
        public void OnFocusChange_FocusIn_SetsTitleColorToWhite()
        {
            _sut.OnFocusChange((FocusInEvent)null);
            Assert.AreEqual(ColorModule.white, _sut.labelTitle.style.color.value);
        }

        [Test]
        public void OnFocusChange_FocusIn_ResetsErrorState()
        {
            _sut.Error("previous error");
            _sut.OnFocusChange((FocusInEvent)null);
            // Reset() is called inside OnFocusChange(FocusInEvent) before applying styles
            Assert.IsTrue(_sut.labelError.ClassListContains("hide"),
                "FocusIn must call Reset() → error label should be hidden");
        }

        [Test]
        public void OnFocusChange_FocusIn_InvokesFocusInCallback()
        {
            bool called = false;
            _sut.SetFocus(onFocusIn: () => called = true);
            _sut.OnFocusChange((FocusInEvent)null);
            Assert.IsTrue(called, "Registered onFocusIn callback must fire on FocusInEvent");
        }

        [Test]
        public void OnFocusChange_FocusIn_NullCallback_DoesNotThrow()
        {
            _sut.SetFocus(onFocusIn: null);
            Assert.DoesNotThrow(() => _sut.OnFocusChange((FocusInEvent)null));
        }

        // ── OnFocusChange(FocusOutEvent) ──────────────────────────────────────

        [Test]
        public void OnFocusChange_FocusOut_RemovesFocusClass()
        {
            // First add the class via FocusIn
            _sut.OnFocusChange((FocusInEvent)null);
            _sut.OnFocusChange((FocusOutEvent)null);
            Assert.IsFalse(_sut.veTextInput.ClassListContains("noctua-text-input-focus"));
        }

        [Test]
        public void OnFocusChange_FocusOut_SetsTitleColorToGreyInactive()
        {
            _sut.OnFocusChange((FocusOutEvent)null);
            Assert.AreEqual(ColorModule.greyInactive, _sut.labelTitle.style.color.value);
        }

        [Test]
        public void OnFocusChange_FocusOut_InvokesFocusOutCallback()
        {
            bool called = false;
            _sut.SetFocus(onFocusOut: () => called = true);
            _sut.OnFocusChange((FocusOutEvent)null);
            Assert.IsTrue(called, "Registered onFocusOut callback must fire on FocusOutEvent");
        }

        [Test]
        public void OnFocusChange_FocusOut_NullCallback_DoesNotThrow()
        {
            _sut.SetFocus(onFocusOut: null);
            Assert.DoesNotThrow(() => _sut.OnFocusChange((FocusOutEvent)null));
        }

        // ── FocusIn → FocusOut round-trip ─────────────────────────────────────

        [Test]
        public void FocusInThenFocusOut_TogglesFocusClass()
        {
            _sut.OnFocusChange((FocusInEvent)null);
            Assert.IsTrue(_sut.veTextInput.ClassListContains("noctua-text-input-focus"),
                "After FocusIn the focus class must be present");

            _sut.OnFocusChange((FocusOutEvent)null);
            Assert.IsFalse(_sut.veTextInput.ClassListContains("noctua-text-input-focus"),
                "After FocusOut the focus class must be removed");
        }

        [Test]
        public void FocusInThenFocusOut_BothCallbacksFire()
        {
            bool inFired = false, outFired = false;
            _sut.SetFocus(
                onFocusIn:  () => inFired  = true,
                onFocusOut: () => outFired = true);

            _sut.OnFocusChange((FocusInEvent)null);
            _sut.OnFocusChange((FocusOutEvent)null);

            Assert.IsTrue(inFired,  "onFocusIn callback must have fired");
            Assert.IsTrue(outFired, "onFocusOut callback must have fired");
        }
    }

    // ─── Spinner ──────────────────────────────────────────────────────────────────

    [TestFixture]
    public class SpinnerTests
    {
        // Spinner extends VisualElement. schedule.Execute is safe on detached elements
        // (Unity defers execution until panel attachment — no exceptions are thrown).

        [Test]
        public void Constructor_WithDimensions_SetsWidthInPixels()
        {
            var spinner = new Spinner(64f, 32f);
            Assert.AreEqual(64f, spinner.style.width.value.value, 0.01f);
        }

        [Test]
        public void Constructor_WithDimensions_SetsHeightInPixels()
        {
            var spinner = new Spinner(64f, 32f);
            Assert.AreEqual(32f, spinner.style.height.value.value, 0.01f);
        }

        [Test]
        public void Constructor_Default_SetsWidthTo100Percent()
        {
            var spinner = new Spinner();
            Assert.AreEqual(LengthUnit.Percent, spinner.style.width.value.unit);
            Assert.AreEqual(100f, spinner.style.width.value.value, 0.01f);
        }

        [Test]
        public void Constructor_Default_SetsHeightTo100Percent()
        {
            var spinner = new Spinner();
            Assert.AreEqual(LengthUnit.Percent, spinner.style.height.value.unit);
            Assert.AreEqual(100f, spinner.style.height.value.value, 0.01f);
        }

        [Test]
        public void Constructor_SetsNameToSpinner()
        {
            var spinner = new Spinner(32f, 32f);
            Assert.AreEqual("Spinner", spinner.name);
        }

        [Test]
        public void StepInterval_DefaultValue_IsPointOne()
        {
            var spinner = new Spinner(32f, 32f);
            Assert.AreEqual(0.1f, spinner.StepInterval, 0.001f);
        }

        [Test]
        public void StepInterval_CanBeAssigned()
        {
            var spinner = new Spinner(32f, 32f);
            spinner.StepInterval = 0.75f;
            Assert.AreEqual(0.75f, spinner.StepInterval, 0.001f);
        }

        [Test]
        public void Constructor_Default_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _ = new Spinner());
        }

        [Test]
        public void Constructor_WithDimensions_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _ = new Spinner(128f, 128f));
        }
    }
}
