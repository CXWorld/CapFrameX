using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignerInspectorLayoutTest
    {
        [TestMethod]
        [DataRow("de-DE", "0,5", 0.5)]
        [DataRow("de-DE", "0.5", 0.5)]
        [DataRow("de-DE", "0.001", 0.001)]
        [DataRow("en-US", "0,5", 0.5)]
        [DataRow("en-US", "0.5", 0.5)]
        public void DecimalBindingAcceptsBothSeparatorsAndClearingRestoresInheritance(string cultureName, string input, double expected)
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            OsdEditorControl editor = null;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                var host = CreateHost(BaseTheme.Dark);
                editor = CreateNumericEditor();
                host.Child = editor;
                Arrange(host, 1280, 820);
                var inspector = (TabControl)editor.FindName("InspectorTabs");
                Descendants<Expander>(inspector).Single(expander => Equals(expander.Header, "Number formatting / second source")).IsExpanded = true;
                Arrange(host, 1280, 820);
                var multiplier = Field(inspector, "SelectedTile.ValueScale");
                Assert.IsNull(editor.SelectedElement.ValueScale, "Initial numeric bindings must not create an override.");
                Assert.IsTrue(string.Equals(cultureName, editor.Language.IetfLanguageTag, StringComparison.OrdinalIgnoreCase));
                SetText(multiplier, input);
                Assert.IsFalse(Validation.GetHasError(multiplier));
                Assert.AreEqual(expected, editor.SelectedElement.ValueScale.Value, 0.0000001,
                    "A decimal separator was interpreted as a thousands separator.");
                Assert.IsTrue(editor.TryCommitEdits());
                multiplier.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                Assert.AreEqual(expected.ToString(CultureInfo.CurrentCulture), multiplier.Text,
                    "Numeric fields should display in the current locale.");
                SetText(multiplier, string.Empty);
                Assert.IsNull(editor.SelectedElement.ValueScale);
                Assert.IsFalse(Validation.GetHasError(multiplier));
                Assert.IsTrue(editor.TryCommitEdits(), "Clearing an optional numeric override must keep editing available.");
            }
            finally
            {
                editor?.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestMethod]
        public void AmbiguousNumbersRemainInvalidAndIntegerFieldsRejectFractions()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            var host = CreateHost(BaseTheme.Dark);
            OsdEditorControl editor = null;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                editor = CreateNumericEditor();
                host.Child = editor;
                Arrange(host, 1280, 820);
                var inspector = (TabControl)editor.FindName("InspectorTabs");
                Descendants<Expander>(inspector).Single(expander => Equals(expander.Header, "Number formatting / second source")).IsExpanded = true;
                Arrange(host, 1280, 820);
                var multiplier = Field(inspector, "SelectedTile.ValueScale");
                SetText(multiplier, "0,5");
                foreach (string input in new[] { "1,2.3", "1.234,5", "1,234.5", "1 234", "not-a-number", "NaN", "Infinity" })
                {
                    SetText(multiplier, input);
                    Assert.IsTrue(Validation.GetHasError(multiplier), $"Ambiguous/non-finite input '{input}' was accepted.");
                    Assert.AreEqual(0.5, editor.SelectedElement.ValueScale.Value, 0.0000001);
                    Assert.IsFalse(editor.TryCommitEdits(), "Invalid text must retain the last valid model value.");
                }
                SetText(multiplier, string.Empty);
                var digits = Field(inspector, "SelectedTile.SecondaryDigits");
                SetText(digits, "2");
                Assert.AreEqual(2, editor.SelectedElement.SecondaryDigits);
                foreach (string input in new[] { "2,5", "2.5", "1.000" })
                {
                    SetText(digits, input);
                    Assert.IsTrue(Validation.GetHasError(digits), "Integer fields must not accept decimal/thousands notation.");
                    Assert.AreEqual(2, editor.SelectedElement.SecondaryDigits);
                }
                SetText(digits, string.Empty);
                Assert.IsNull(editor.SelectedElement.SecondaryDigits);
                Assert.IsTrue(editor.TryCommitEdits(), "Valid corrections must clear field validation and resume editing.");
                var width = Field(inspector, "SelectedFrameWidth");
                double oldWidth = editor.SelectedElement.FrameWidth.Value;
                SetText(width, string.Empty);
                Assert.IsTrue(Validation.GetHasError(width), "A required numeric field must visibly reject empty input.");
                Assert.AreEqual(oldWidth, editor.SelectedElement.FrameWidth.Value);
                Assert.IsFalse(editor.TryCommitEdits(), "Empty required input must not silently allow gestures to discard it.");
                SetText(width, oldWidth.ToString(CultureInfo.CurrentCulture));
                Assert.IsTrue(editor.TryCommitEdits());
            }
            finally
            {
                editor?.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                host.Child = null;
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestMethod]
        [DataRow(BaseTheme.Dark, 1080, 560)]
        [DataRow(BaseTheme.Dark, 1280, 820)]
        [DataRow(BaseTheme.Light, 1080, 560)]
        [DataRow(BaseTheme.Light, 1280, 820)]
        public void InheritedFieldsAndColorActionsKeepStableSeparateBounds(BaseTheme theme, int width, int height)
        {
            var host = CreateHost(theme);
            var editor = new OsdEditorControl();
            editor.SetResourceReference(Control.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
            editor.SetResourceReference(Control.ForegroundProperty, "MaterialDesign.Brush.Foreground");
            editor.SetResourceReference(OsdEditorControl.SurfaceBrushProperty, "MaterialDesign.Brush.TextBox.FilledBackground");
            editor.SetResourceReference(OsdEditorControl.AccentBrushProperty, "MaterialDesign.Brush.Primary");
            var document = new OsdDesignDocument { Width = 640, CanvasHeight = 480, IsCanvas = true };
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "metric", Label = "Framerate", Key = "fps", Unit = "FPS",
                X = 0, Y = 0, FrameWidth = 240, FrameHeight = 100
            });
            editor.SetTemplateJson(document.ToJson());
            string beforeLayout = editor.GetTemplateJson();
            host.Child = editor;
            var window = new Window
            {
                Content = host, Width = width, Height = height, WindowStyle = WindowStyle.None,
                ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000
            };
            try
            {
                window.Show();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                Arrange(host, width, height);
                var inspector = (TabControl)editor.FindName("InspectorTabs");
                // Also test the narrow sidebar reachable through its resize handle.
                var sidebar = (Border)inspector.Parent;
                var columns = (Grid)sidebar.Parent;
                columns.ColumnDefinitions[4].Width = new GridLength(width == 1080 ? 270 : 340);
                foreach (string name in new[] { "TypographyExpander", "FrameSpacingExpander", "TileStyleExpander" })
                {
                    ((Expander)editor.FindName(name)).IsExpanded = true;
                }
                SettlePresentation();
                Arrange(host, width, height);

                var background = (TextBox)editor.FindName("TileStyleBackgroundColor");
                var accent = (TextBox)editor.FindName("TileStyleValueColor");
                var reset = (Button)editor.FindName("ResetTileColorsButton");
                var padding = Field(inspector, "SelectedTile.Padding");
                var labelSize = Field(inspector, "SelectedTile.LabelSize");
                var unitSize = Field(inspector, "SelectedTile.UnitSize");
                var label = Field(inspector, "SelectedTile.Label");
                var horizontalFields = new[]
                {
                    Field(inspector, "SelectedTile.X"), Field(inspector, "SelectedTile.Y"),
                    Field(inspector, "SelectedFrameWidth"), Field(inspector, "SelectedFrameHeight"),
                    background, accent, padding, labelSize, unitSize, label
                };
                Assert.AreEqual(string.Empty, background.Text, "Inherited colors must remain unset.");
                Assert.AreEqual(string.Empty, padding.Text, "Inherited padding must remain unset.");
                Assert.AreEqual(beforeLayout, editor.GetTemplateJson(), "Laying out inherited fields must not create design overrides.");
                foreach (var field in horizontalFields) AssertTextStartsUnderLabel(field);

                foreach (var field in new[] { background, accent, padding, labelSize, unitSize, label })
                {
                    string path = BindingOperations.GetBindingExpression(field, TextBox.TextProperty)?.ParentBinding.Path?.Path;
                    Assert.IsTrue(field.ActualHeight >= 32, $"Input {path} collapsed: {LayoutChain(field)}");
                    Assert.IsTrue(field.ActualWidth >= 100, $"Input {path} does not fill its inspector row: {LayoutChain(field)}.");
                    Assert.AreEqual(VerticalAlignment.Center, field.VerticalContentAlignment,
                        "Mixed host/local alignments must not shift field text between inherited and explicit values.");
                    AssertVisualsStayInside(field);
                    var underline = (Border)field.Template.FindName("Underline", field);
                    Assert.IsNotNull(underline, $"Input {path} is missing its underline.");
                    Assert.AreEqual(field.ActualWidth, underline.ActualWidth, 0.5,
                        $"Input {path} underline must fill the entire field even when its inherited value is empty.");
                }
                Assert.AreEqual(label.ActualHeight, labelSize.ActualHeight, 0.5,
                    "An inherited numeric field must align like a populated text field.");
                AssertColorRows(inspector, background, accent, reset);

                double emptyHeight = background.ActualHeight;
                editor.SelectedElement.BackgroundColor = "#11223344";
                editor.SelectedElement.ValueColor = "#3399FF";
                editor.SelectedElement.Padding = 8;
                editor.SelectedElement.LabelSize = 13;
                Arrange(host, width, height);
                Assert.AreEqual(emptyHeight, background.ActualHeight, 0.5,
                    "Entering an explicit color must not move the following action.");
                AssertVisualsStayInside(background);
                AssertColorRows(inspector, background, accent, reset);
                foreach (var field in horizontalFields) AssertTextStartsUnderLabel(field);

                var labelColor = Field(inspector, "TileStyle[labelColor].Value");
                var borderColor = Field(inspector, "TileStyle[borderColor].Value");
                SetText(labelColor, "#55AA88");
                SetText(borderColor, "#112233");
                Assert.IsTrue(editor.TryCommitEdits(), "Explicit optional colors must be editable through their bindings.");
                SetText(labelColor, string.Empty);
                SetText(borderColor, string.Empty);
                SetText(padding, string.Empty);
                SetText(labelSize, string.Empty);
                Assert.IsNull(editor.SelectedElement.LabelColor, "Clearing an optional label color must restore inheritance.");
                Assert.IsNull(editor.SelectedElement.BorderColor, "Clearing an optional border color must restore inheritance.");
                Assert.IsNull(editor.SelectedElement.Padding, "Clearing optional padding must restore inheritance.");
                Assert.IsNull(editor.SelectedElement.LabelSize, "Clearing optional label size must restore inheritance.");
                Assert.IsTrue(editor.TryCommitEdits(), "Clearing inherited fields must not leave a validation error that blocks gestures.");

                reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Arrange(host, width, height);
                Assert.IsNull(editor.SelectedElement.BackgroundColor);
                Assert.IsNull(editor.SelectedElement.ValueColor);
                Assert.AreEqual(string.Empty, background.Text);
                Assert.AreEqual(emptyHeight, background.ActualHeight, 0.5,
                    "Returning to inherited colors must keep the field and reset action in separate rows.");
                AssertColorRows(inspector, background, accent, reset);
                Assert.AreEqual(9, background.MaxLength, "Removing a visual counter must preserve the color input limit.");
                Assert.IsNotNull(background.Template.FindName("PART_ContentHost", background),
                    "The compact field must retain the WPF text editing host.");

                inspector.SelectedItem = inspector.Items.Cast<TabItem>().Single(tab => Equals(tab.Header, "Layout"));
                Arrange(host, width, height);
                AssertTextStartsUnderLabel(Field(inspector, "Document.Width"));
                AssertTextStartsUnderLabel(Field(inspector, "Document.GridSize"));
                AssertTextStartsUnderLabel(Field(inspector, "Document.CanvasHeight"));
            }
            finally
            {
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                window.Close();
                host.Child = null;
            }
        }

        private static Border CreateHost(BaseTheme theme)
        {
            var host = new Border();
            // The assertions describe the settled form, not MaterialDesign's animated
            // zero-height expander transition, which requires a presentation clock.
            TransitionAssist.SetDisableTransitions(host, true);
            host.Resources.MergedDictionaries.Add(new CustomColorTheme
            {
                BaseTheme = theme,
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
            return host;
        }

        private static OsdEditorControl CreateNumericEditor()
        {
            var editor = new OsdEditorControl();
            var document = new OsdDesignDocument { Width = 640, CanvasHeight = 480, IsCanvas = true };
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "metric", Label = "Clock", Key = "cpuClock", Unit = "MHz",
                X = 0, Y = 0, FrameWidth = 240, FrameHeight = 100
            });
            editor.SetTemplateJson(document.ToJson());
            return editor;
        }

        private static TextBox Field(DependencyObject inspector, string path)
        {
            return LogicalDescendants<TextBox>(inspector).Single(field =>
                BindingOperations.GetBindingExpression(field, TextBox.TextProperty)?.ParentBinding.Path?.Path == path);
        }

        private static void SetText(TextBox field, string text)
        {
            field.SetCurrentValue(TextBox.TextProperty, text);
            field.GetBindingExpression(TextBox.TextProperty).UpdateSource();
        }

        private static void AssertVisualsStayInside(TextBox field)
        {
            Rect content = VisualTreeHelper.GetDescendantBounds(field);
            Assert.IsTrue(content.IsEmpty || content.Bottom <= field.ActualHeight + 0.5,
                $"Field content/counter extends below its measured input: {content.Bottom:F1} > {field.ActualHeight:F1}.");
        }

        private static void AssertTextStartsUnderLabel(TextBox field)
        {
            FrameworkElement row = field;
            while (row.Parent is not StackPanel && row.Parent is FrameworkElement parent) row = parent;
            var form = row.Parent as StackPanel;
            Assert.IsNotNull(form, "Every inspector input must have a labelled form row.");
            int index = form.Children.IndexOf(row);
            Assert.IsTrue(index > 0);
            var label = form.Children[index - 1] as TextBlock;
            Assert.IsNotNull(label, "The preceding form element must be the input's label.");
            Rect caret = field.GetRectFromCharacterIndex(0);
            Assert.IsFalse(caret.IsEmpty, "Measure the rendered text origin, including the WPF text view's internal inset.");
            double textLeft = field.TransformToAncestor(form).Transform(caret.TopLeft).X;
            double labelLeft = label.TransformToAncestor(form).Transform(new Point(label.Padding.Left, 0)).X;
            Assert.AreEqual(labelLeft, textLeft, 0.5,
                $"'{label.Text}' and its value start at different horizontal positions ({labelLeft:F1} vs {textLeft:F1}).");
            var contentHost = (ScrollViewer)field.Template.FindName("PART_ContentHost", field);
            Assert.IsTrue(contentHost.TransformToAncestor(field).TransformBounds(new Rect(contentHost.RenderSize)).Right <= field.ActualWidth + 0.5,
                "Aligning the text origin must not extend the scrolling text host beyond the right field edge.");
        }

        private static void AssertColorRows(FrameworkElement inspector, TextBox background, TextBox accent, Button reset)
        {
            // Inspect the shared form coordinates, before the host expander's animated
            // transform. A zero presentation-clock scale must not look like overlapping rows.
            var form = (FrameworkElement)reset.Parent;
            Rect backgroundBounds = Bounds(background, form);
            Rect accentBounds = Bounds(accent, form);
            Rect resetBounds = Bounds(reset, form);
            Assert.IsTrue(backgroundBounds.Top >= accentBounds.Bottom + 4, "The color fields overlap.");
            Assert.IsTrue(resetBounds.Top >= backgroundBounds.Bottom + 12 - 0.5,
                "Reset colors overlaps the background field or its reserved spacing.");
            foreach (TextBox field in new[] { background, accent })
            {
                var swatch = ((Grid)field.Parent).Children.OfType<Button>().Single();
                Assert.AreEqual(Bounds(field, form).Top, Bounds(swatch, form).Top, 0.5,
                    "The input and color swatch must share one row baseline.");
                Assert.AreEqual(field.ActualHeight, swatch.ActualHeight, 0.5);
            }
        }

        private static Rect Bounds(FrameworkElement element, Visual relativeTo)
            => element.TransformToAncestor(relativeTo).TransformBounds(new Rect(element.RenderSize));

        private static string LayoutChain(FrameworkElement element)
        {
            var details = new List<string>();
            while (element != null && details.Count < 12)
            {
                details.Add($"{element.GetType().Name}: {element.Visibility} {element.RenderSize} transform={element.RenderTransform} layout={element.LayoutTransform} valid={element.IsMeasureValid}/{element.IsArrangeValid}");
                element = (VisualTreeHelper.GetParent(element) ?? element.Parent) as FrameworkElement;
            }
            return string.Join("; ", details);
        }

        private static void Arrange(FrameworkElement host, int width, int height)
        {
            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();
            // MaterialDesign binds its expander presenter visibility to IsExpanded.
            // Realize that queued binding before inspecting the expanded visual tree.
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();
            // A disconnected MaterialDesign Expander can retain its deferred content
            // outside the visual tree despite IsExpanded=true. Arrange the same form
            // at its real sidebar width; do not substitute controls or theme templates.
            foreach (var expander in LogicalDescendants<Expander>(host).Where(item => PresentationSource.FromVisual(host) == null && item.IsExpanded && item.ActualWidth > 0))
            {
                if (expander.Content is not FrameworkElement content) continue;
                double available = Math.Max(0, expander.ActualWidth - expander.Padding.Left - expander.Padding.Right);
                content.InvalidateMeasure();
                content.InvalidateArrange();
                content.Measure(new Size(available, double.PositiveInfinity));
                content.Arrange(new Rect(0, 0, available, content.DesiredSize.Height));
                content.UpdateLayout();
            }
        }

        private static void SettlePresentation()
        {
            // The MaterialDesign expander animates a layout transform even when
            // TransitionAssist is disabled. Let its real presentation clock complete.
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
            {
                Interval = TimeSpan.FromMilliseconds(450)
            };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match) yield return match;
                foreach (T descendant in Descendants<T>(child)) yield return descendant;
            }
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
    }
}
