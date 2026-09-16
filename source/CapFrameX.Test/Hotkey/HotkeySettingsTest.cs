using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CapFrameX.Hotkey;
using CapFrameX.MVVM.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Hotkey
{
    [STATestClass]
    public class HotkeySettingsTest
    {
        [TestMethod]
        public void EmptySetting_DisablesHotkeyWithoutRestoringDefault()
        {
            Assert.IsTrue(CXHotkey.IsValidSetting(string.Empty));
            Assert.IsFalse(CXHotkey.IsValidHotkey(string.Empty));
            Assert.IsNull(CXHotkey.Create(string.Empty.Split('+'), Key.F11));
            Assert.IsNull(CXHotkey.Create(string.Empty.Split('+'), Key.O, ModifierKeys.Alt));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow(" ")]
        [DataRow("Alt+NotAKey")]
        [DataRow("Control+")]
        [DataRow("Not set")]
        public void InvalidSetting_IsNotAcceptedAsDisabled(string value)
        {
            Assert.IsFalse(CXHotkey.IsValidSetting(value));
        }

        [DataTestMethod]
        [DataRow("F11")]
        [DataRow("Alt+O")]
        [DataRow("Control+Shift+F9")]
        public void AssignedSetting_PreservesKeyCombination(string value)
        {
            Assert.IsTrue(CXHotkey.IsValidSetting(value));
            Assert.AreEqual(value, CXHotkey.Create(value.Split('+'), Key.F12).ToString());
        }

        [TestMethod]
        public void UnassignedBinding_ShowsNotSetWithoutValidationErrorAndCanBeReassigned()
        {
            var source = new ContentControl { Content = new CXHotkey(Key.F11) };
            var textBox = new TextBox();
            var binding = new Binding(nameof(ContentControl.Content))
            {
                Source = source,
                Mode = BindingMode.OneWay,
                TargetNullValue = HotkeyValidationRule.UnsetText
            };
            binding.ValidationRules.Add(new HotkeyValidationRule { ValidatesOnTargetUpdated = true });
            BindingOperations.SetBinding(textBox, TextBox.TextProperty, binding);

            Assert.AreEqual("F11", textBox.Text);
            Assert.IsFalse(Validation.GetHasError(textBox));

            source.Content = null;
            Assert.AreEqual("Not set", textBox.Text);
            Assert.IsFalse(Validation.GetHasError(textBox));

            source.Content = new CXHotkey(Key.O, ModifierKeys.Alt);
            Assert.AreEqual("Alt+O", textBox.Text);
            Assert.IsFalse(Validation.GetHasError(textBox));

            Assert.IsFalse(new HotkeyValidationRule().Validate("Alt+NotAKey", CultureInfo.InvariantCulture).IsValid);
            BindingOperations.ClearAllBindings(textBox);
        }
    }
}
