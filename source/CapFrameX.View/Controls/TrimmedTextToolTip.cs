using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.View.Controls
{
    /// <summary>
    /// Opens an element's tool tip only while a text block inside it cuts its text off with
    /// <see cref="TextBlock.TextTrimming"/>. Tab headers use it with their header as the tool tip:
    /// a translation longer than the header ends in an ellipsis and the tool tip shows all of it,
    /// while a header that fits does not repeat itself on hover. List cells use it the same way,
    /// and over a cell whose text fits, the list's own tool tip shows.
    /// </summary>
    public static class TrimmedTextToolTip
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(TrimmedTextToolTip), new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        /// <summary>
        /// Whether <paramref name="root"/> or a text block below it in the visual tree trims its text.
        /// </summary>
        public static bool HasTrimmedText(DependencyObject root)
        {
            if (root is TextBlock textBlock && IsTrimmed(textBlock))
                return true;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                if (HasTrimmedText(VisualTreeHelper.GetChild(root, i)))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether <paramref name="textBlock"/> is narrower than its text, so it shows an ellipsis.
        /// Hidden text blocks do not count; the tab headers keep one that reserves the width of the
        /// selected (semibold) text.
        /// </summary>
        public static bool IsTrimmed(TextBlock textBlock)
        {
            if (textBlock.TextTrimming == TextTrimming.None || textBlock.Visibility != Visibility.Visible
                || string.IsNullOrEmpty(textBlock.Text))
                return false;

            var text = new FormattedText(textBlock.Text, CultureInfo.CurrentUICulture, textBlock.FlowDirection,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize, Brushes.Black, null, TextOptions.GetTextFormattingMode(textBlock),
                VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);
            double available = textBlock.ActualWidth - textBlock.Padding.Left - textBlock.Padding.Right;
            return text.Width > available + 0.5;
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element)
                return;

            element.ToolTipOpening -= OnToolTipOpening;
            element.MouseEnter -= OnMouseEnter;
            if ((bool)e.NewValue)
            {
                element.ToolTipOpening += OnToolTipOpening;
                element.MouseEnter += OnMouseEnter;
            }
        }

        // WPF looks for a tool tip right after MouseEnter and passes over elements whose tool tip is
        // disabled, so over a text that fits the tool tip of an ancestor shows instead, such as the
        // drag and drop hint of a list.
        private static void OnMouseEnter(object sender, MouseEventArgs e)
        {
            var element = (FrameworkElement)sender;
            element.SetCurrentValue(ToolTipService.IsEnabledProperty, HasTrimmedText(element));
        }

        // Handling the event cancels the tool tip. Deciding on every opening sees the current text,
        // language and width without tracking any of them.
        private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (!HasTrimmedText((DependencyObject)sender))
                e.Handled = true;
        }
    }
}
