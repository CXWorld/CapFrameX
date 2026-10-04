using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CapFrameX.View.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Localization
{
    /// <summary>
    /// Tab headers do not grow with long translations: past their MaxWidth the text ends in an
    /// ellipsis, and only then does the header show its full text as a tool tip.
    /// </summary>
    [STATestClass]
    public class TabHeaderTrimmingTest
    {
        private const string LongHeader = "Распределение времени кадра";

        [TestMethod]
        public void TextBlock_NarrowerThanItsText_IsTrimmed()
        {
            var text = TrimmingText(LongHeader);
            Layout(text, 60);

            Assert.IsTrue(TrimmedTextToolTip.IsTrimmed(text));
        }

        [TestMethod]
        public void TextBlock_WideEnoughForItsText_IsNotTrimmed()
        {
            var text = TrimmingText(LongHeader);
            Layout(text, 1000);

            Assert.IsFalse(TrimmedTextToolTip.IsTrimmed(text));
        }

        [TestMethod]
        public void HiddenOrNonTrimmingTextBlocks_DoNotCount()
        {
            var reserve = TrimmingText(LongHeader);
            reserve.Visibility = Visibility.Hidden;
            var clipped = new TextBlock { Text = LongHeader };
            Layout(reserve, 60);
            Layout(clipped, 60);

            Assert.IsFalse(TrimmedTextToolTip.IsTrimmed(reserve));
            Assert.IsFalse(TrimmedTextToolTip.IsTrimmed(clipped));
        }

        [TestMethod]
        public void ToolTip_OpensOnlyWhileTheHeaderIsTrimmed()
        {
            var header = Header(LongHeader);
            TrimmedTextToolTip.SetIsEnabled(header, true);

            header.MaxWidth = 60;
            Layout(header, 60);
            Assert.IsTrue(TrimmedTextToolTip.HasTrimmedText(header));
            Assert.IsFalse(OpenToolTip(header), "A trimmed header must show its full text.");

            header.MaxWidth = double.PositiveInfinity;
            Layout(header, 1000);
            Assert.IsFalse(TrimmedTextToolTip.HasTrimmedText(header));
            Assert.IsTrue(OpenToolTip(header), "A header that fits must not repeat itself as a tool tip.");
        }

        // List cells use it too: over a text that fits, the list's own tool tip (the drag and drop
        // hint of the overlay entries) must show instead of nothing.
        [TestMethod]
        public void ToolTip_GivesWayToTheAncestorsWhileTheTextFits()
        {
            var cell = TrimmingText(LongHeader);
            cell.ToolTip = LongHeader;
            TrimmedTextToolTip.SetIsEnabled(cell, true);

            Layout(cell, 60);
            EnterWithMouse(cell);
            Assert.IsTrue(ToolTipService.GetIsEnabled(cell), "A trimmed text must show its full text.");

            Layout(cell, 1000);
            EnterWithMouse(cell);
            Assert.IsFalse(ToolTipService.GetIsEnabled(cell), "A text that fits must leave the tool tip to its ancestors.");
        }

        private static TextBlock TrimmingText(string text)
            => new TextBlock { Text = text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };

        // The layout of CxTabHeaderTemplate: a hidden semibold copy reserves the width of the
        // selected text, the visible text trims.
        private static Border Header(string text)
        {
            var grid = new Grid { Margin = new Thickness(12, 0, 12, 0) };
            grid.Children.Add(new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Visibility = Visibility.Hidden });
            var visible = TrimmingText(text);
            visible.HorizontalAlignment = HorizontalAlignment.Center;
            grid.Children.Add(visible);
            return new Border { Child = grid, ToolTip = text };
        }

        private static void Layout(FrameworkElement element, double width)
        {
            element.Measure(new Size(width, 25));
            element.Arrange(new Rect(0, 0, Math.Min(width, element.DesiredSize.Width), 25));
        }

        private static void EnterWithMouse(UIElement element)
            => element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });

        /// <returns>Whether the tool tip was cancelled.</returns>
        private static bool OpenToolTip(FrameworkElement element)
        {
            // ToolTipEventArgs has no public constructor; WPF raises it when the pointer rests.
            var args = (ToolTipEventArgs)Activator.CreateInstance(typeof(ToolTipEventArgs),
                BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { true }, null);
            element.RaiseEvent(args);
            return args.Handled;
        }
    }
}
