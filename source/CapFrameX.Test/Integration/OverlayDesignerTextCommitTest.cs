using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignerTextCommitTest
    {
        [TestMethod]
        public void TypingDoesNotUpdateTheDesignAndEnterCreatesOneUndoStep()
        {
            using var host = new EditorHost();
            var editor = host.Editor;
            var label = host.Field("SelectedTile.Label");
            string before = editor.GetTemplateJson();
            int changes = 0;
            int pendingChanges = 0;
            bool pendingObserved = false;
            editor.TemplateChanged += (_, _) => changes++;
            editor.PendingTextEditsChanged += (_, _) =>
            {
                pendingChanges++;
                pendingObserved |= editor.HasPendingTextEdits;
            };
            label.Focus();
            var focusedBefore = FocusManager.GetFocusedElement(host.Window);

            Type(label, "Frame");
            Settle(400);
            Type(label, "Framerate complete");
            Settle(400);

            Assert.AreEqual("Original FPS", editor.SelectedElement.Label);
            Assert.AreEqual(before, editor.GetTemplateJson(), "The 250 ms preview timer must not consume uncommitted text.");
            Assert.AreEqual(0, changes, "Typing must not publish a new overlay template.");
            Assert.IsTrue(editor.HasPendingTextEdits);
            Assert.IsTrue(pendingChanges > 0, "The profile host must be told about unsaved text without changing the design.");
            Assert.IsTrue(pendingObserved, "The pending notification must run after the binding exposes its draft state.");
            Assert.AreSame(label, focusedBefore, "The input must have logical focus for the Enter regression.");

            Assert.IsTrue(Enter(label).Handled);
            Settle(400);

            Assert.AreSame(focusedBefore, FocusManager.GetFocusedElement(host.Window), "Enter must commit while retaining input focus.");
            Assert.AreEqual("Framerate complete", editor.SelectedElement.Label);
            Assert.AreEqual(1, changes);
            Assert.IsFalse(editor.HasPendingTextEdits);
            var undo = (Button)editor.FindName("UndoButton");
            Assert.IsTrue(undo.IsEnabled);
            undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();
            Assert.AreEqual(before, editor.GetTemplateJson(), "One undo must restore the complete field edit.");
            Assert.IsFalse(undo.IsEnabled, "Typing pauses must not create intermediate undo entries.");
        }

        [TestMethod]
        public void MovingFocusCommitsThePreviousField()
        {
            using var host = new EditorHost();
            var label = host.Field("SelectedTile.Label");
            var unit = host.Field("SelectedTile.Unit");
            label.Focus();
            Assert.AreSame(label, FocusManager.GetFocusedElement(host.Window));
            Type(label, "Focus committed");
            Assert.AreEqual("Original FPS", host.Editor.SelectedElement.Label);

            unit.Focus();
            Settle(400);

            Assert.AreSame(unit, FocusManager.GetFocusedElement(host.Window));
            Assert.AreEqual("Focus committed", host.Editor.SelectedElement.Label);
            Assert.IsFalse(host.Editor.HasPendingTextEdits);
            Assert.IsTrue(host.Editor.TryCommitEdits());
        }

        [TestMethod]
        public void IncompleteNumbersRemainPendingUntilCommitAndCanBeCorrected()
        {
            using var host = new EditorHost();
            var editor = host.Editor;
            var width = host.Field("SelectedFrameWidth");
            double original = editor.SelectedElement.FrameWidth.Value;
            Type(width, "not-a-number");
            Settle(400);

            Assert.IsFalse(Validation.GetHasError(width), "Validation must not interrupt an unfinished number.");
            Assert.AreEqual(original, editor.SelectedElement.FrameWidth.Value);
            Assert.IsTrue(editor.HasPendingTextEdits);
            Assert.IsTrue(Enter(width).Handled);
            Assert.IsTrue(Validation.GetHasError(width));
            Assert.IsFalse(editor.TryCommitEdits());
            Assert.AreEqual(original, editor.SelectedElement.FrameWidth.Value);

            Type(width, "256");
            Assert.IsTrue(editor.TryCommitEdits(), "Saving must flush a valid pending correction without another focus change.");
            Assert.IsFalse(Validation.GetHasError(width));
            Assert.AreEqual(256.0, editor.SelectedElement.FrameWidth.Value);
            Assert.IsFalse(editor.HasPendingTextEdits);
        }

        [TestMethod]
        public void ChangingTheSelectedTileCommitsItsTextBeforeRebinding()
        {
            using var host = new EditorHost();
            var first = host.Editor.CurrentElements[0];
            var second = host.Editor.CurrentElements[1];
            Type(host.Field("SelectedTile.Label"), "First tile edited");

            Assert.IsTrue(host.Editor.SelectElement(second));
            Settle();

            Assert.AreEqual("First tile edited", first.Label);
            Assert.AreEqual("Original CPU", second.Label, "Pending text must never leak into the newly selected tile.");
            Assert.AreSame(second, host.Editor.SelectedElement);
            Assert.AreEqual("Original CPU", host.Field("SelectedTile.Label").Text);
            Assert.IsFalse(host.Editor.HasPendingTextEdits);
        }

        [TestMethod]
        public void InvalidPendingInputPreventsSelectingAnotherTile()
        {
            using var host = new EditorHost();
            var first = host.Editor.CurrentElements[0];
            var second = host.Editor.CurrentElements[1];
            var width = host.Field("SelectedFrameWidth");
            Type(width, "-");

            Assert.IsFalse(host.Editor.SelectElement(second));
            Assert.AreSame(first, host.Editor.SelectedElement);
            Assert.AreEqual("-", width.Text, "A rejected selection must keep the user's unfinished input available to correct.");
            Assert.IsTrue(Validation.GetHasError(width));
            Type(width, "240");
            Assert.IsTrue(host.Editor.SelectElement(second));
            Assert.AreSame(second, host.Editor.SelectedElement);
        }

        [TestMethod]
        public void MultilineTextUsesEnterToCommitAndLeavesShiftEnterForNewlines()
        {
            using var host = new EditorHost();
            Assert.IsTrue(host.Editor.SelectElement(host.Editor.CurrentElements[2]));
            Settle();
            var text = host.Field("SelectedTile.Text");
            Assert.IsTrue(text.AcceptsReturn);
            Type(text, "First line");

            Assert.IsFalse(Enter(text, ModifierKeys.Shift).Handled, "Shift+Enter must reach the TextBox's newline handler.");
            Assert.AreEqual("Original text", host.Editor.SelectedElement.Text);
            Assert.IsTrue(host.Editor.HasPendingTextEdits);
            Type(text, "First line\r\nSecond line");
            Assert.IsTrue(Enter(text).Handled, "Plain Enter must not add another newline.");
            Assert.AreEqual("First line\r\nSecond line", host.Editor.SelectedElement.Text);
            Assert.IsFalse(host.Editor.HasPendingTextEdits);
        }

        [TestMethod]
        public void EditableFontPickerDefersTypedTextAndCommitsOnEnter()
        {
            using var host = new EditorHost();
            var typography = (Expander)host.Editor.FindName("TypographyExpander");
            typography.IsExpanded = true;
            Settle(450);
            host.Window.UpdateLayout();
            var font = LogicalDescendants<ComboBox>(typography).Single(combo =>
                combo.GetBindingExpression(ComboBox.TextProperty)?.ParentBinding.Path?.Path == "SelectedTile.Family");
            font.ApplyTemplate();
            var input = (TextBox)font.Template.FindName("PART_EditableTextBox", font);
            Assert.IsNotNull(input);
            string before = host.Editor.GetTemplateJson();

            Type(input, "Consolas");
            Settle(400);

            Assert.AreEqual("Consolas", font.Text);
            Assert.IsNull(host.Editor.SelectedElement.Family);
            Assert.AreEqual(before, host.Editor.GetTemplateJson());
            Assert.IsTrue(host.Editor.HasPendingTextEdits);
            Assert.IsTrue(Enter(input).Handled);
            Assert.AreEqual("Consolas", host.Editor.SelectedElement.Family);
            Assert.IsFalse(host.Editor.HasPendingTextEdits);
        }

        [TestMethod]
        public void SourceSearchStillFiltersImmediatelyWithoutDirtyingTheDesign()
        {
            using var host = new EditorHost();
            var sourcePicker = (ComboBox)host.Editor.FindName("InspectorSourcePicker");
            string before = host.Editor.GetTemplateJson();
            Assert.AreEqual(2, sourcePicker.Items.Count);

            Type(host.Field("Search"), "CPU");
            Settle();

            Assert.AreEqual(1, sourcePicker.Items.Count, "Source search must remain responsive without Enter or focus loss.");
            Assert.AreEqual("cpuLoad", ((OsdTelemetrySource)sourcePicker.Items[0]).Key);
            Assert.IsFalse(host.Editor.HasPendingTextEdits);
            Assert.AreEqual(before, host.Editor.GetTemplateJson());
        }

        private static void Type(TextBox field, string value)
        {
            // Change only the real input target. Explicit UpdateSource would bypass
            // the LostFocus behavior these tests need to exercise.
            field.SetCurrentValue(TextBox.TextProperty, value);
        }

        private static KeyEventArgs Enter(TextBox field, ModifierKeys modifiers = ModifierKeys.None)
        {
            var source = PresentationSource.FromVisual(field);
            Assert.IsNotNull(source, "Keyboard tests require a realized control in an actual WPF presentation source.");
            var args = new KeyEventArgs(new TestKeyboardDevice(modifiers), source, Environment.TickCount, Key.Enter)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            field.RaiseEvent(args);
            return args;
        }

        private static void Settle(int milliseconds = 1)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
            {
                Interval = TimeSpan.FromMilliseconds(milliseconds)
            };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        private static IEnumerable<T> LogicalDescendants<T>(DependencyObject parent) where T : DependencyObject
        {
            foreach (object item in LogicalTreeHelper.GetChildren(parent))
            {
                if (item is not DependencyObject child) continue;
                if (child is T match) yield return match;
                foreach (T descendant in LogicalDescendants<T>(child)) yield return descendant;
            }
        }

        private sealed class TestKeyboardDevice : KeyboardDevice
        {
            private readonly ModifierKeys _modifiers;

            public TestKeyboardDevice(ModifierKeys modifiers) : base(InputManager.Current)
            {
                _modifiers = modifiers;
            }

            protected override KeyStates GetKeyStatesFromSystem(Key key)
            {
                return (key == Key.LeftShift || key == Key.RightShift) && (_modifiers & ModifierKeys.Shift) != 0
                    ? KeyStates.Down : KeyStates.None;
            }
        }

        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; }
            public Window Window { get; }
            public TabControl Inspector => (TabControl)Editor.FindName("InspectorTabs");

            public EditorHost()
            {
                var host = new Border();
                TransitionAssist.SetDisableTransitions(host, true);
                host.Resources.MergedDictionaries.Add(new CustomColorTheme
                {
                    BaseTheme = BaseTheme.Dark,
                    PrimaryColor = Color.FromRgb(2, 113, 249),
                    SecondaryColor = Colors.Lime
                });
                foreach (string resource in new[]
                {
                    "MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml",
                    "CapFrameX.View;component/Styles/CxTextFields.xaml"
                })
                {
                    host.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/" + resource)
                    });
                }
                Editor = new OsdEditorControl();
                var document = new OsdDesignDocument { Width = 640, CanvasHeight = 480, IsCanvas = true };
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "metric", Label = "Original FPS", Key = "fps", Unit = "FPS",
                    X = 0, Y = 0, FrameWidth = 240, FrameHeight = 100
                });
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "metric", Label = "Original CPU", Key = "cpuLoad", Unit = "%",
                    X = 250, Y = 0, FrameWidth = 240, FrameHeight = 100
                });
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "text", Label = "Note", Text = "Original text",
                    X = 0, Y = 120, FrameWidth = 400, FrameHeight = 100
                });
                Editor.SetTemplateJson(document.ToJson());
                Editor.Sources = new[]
                {
                    new OsdTelemetrySource { Key = "fps", Name = "Framerate", Category = "Performance", Unit = "FPS" },
                    new OsdTelemetrySource { Key = "cpuLoad", Name = "CPU load", Category = "CPU", Unit = "%" }
                };
                host.Child = Editor;
                Window = new Window
                {
                    Content = host, Width = 1280, Height = 820,
                    WindowStyle = WindowStyle.None, ShowActivated = false,
                    ShowInTaskbar = false, Left = -10000, Top = -10000
                };
                Window.Show();
                Settle();
                Window.UpdateLayout();
                Editor.ResetHistory();
            }

            public TextBox Field(string path)
            {
                return LogicalDescendants<TextBox>(Inspector).Single(field =>
                    field.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path == path);
            }

            public void Dispose()
            {
                Window.Close();
                Editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Window.Content = null;
            }
        }
    }
}
