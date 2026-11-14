using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CapFrameX.View.Controls
{
    /// <summary>
    /// Opens a tool bar's overflow menu above the tool bar, its right edge on the overflow button.
    /// The tool bars of the analysis page sit at the bottom of the window: opened downwards, as the
    /// template places it, the menu covered the status bar and the taskbar.
    /// </summary>
    public static class ToolBarOverflowPlacement
    {
        public static readonly DependencyProperty OpensUpwardProperty = DependencyProperty.RegisterAttached(
            "OpensUpward", typeof(bool), typeof(ToolBarOverflowPlacement), new PropertyMetadata(false, OnOpensUpwardChanged));

        public static bool GetOpensUpward(DependencyObject element) => (bool)element.GetValue(OpensUpwardProperty);

        public static void SetOpensUpward(DependencyObject element, bool value) => element.SetValue(OpensUpwardProperty, value);

        /// <summary>
        /// The popup of the tool bar's template that hosts the overflow panel, or null. Found through
        /// the overflow panel, which every tool bar template has to name PART_ToolBarOverflowPanel.
        /// </summary>
        public static Popup FindOverflowPopup(ToolBar toolBar)
        {
            toolBar.ApplyTemplate();
            DependencyObject current = toolBar.Template?.FindName("PART_ToolBarOverflowPanel", toolBar) as DependencyObject;
            while (current != null && current is not Popup)
                current = LogicalTreeHelper.GetParent(current);

            return current as Popup;
        }

        /// <summary>
        /// Above the overflow button with the right edges aligned; below it only when there is no
        /// room above.
        /// </summary>
        public static CustomPopupPlacement[] PlaceAboveRightAligned(Size popupSize, Size targetSize, Point offset)
            => new[]
            {
                new CustomPopupPlacement(new Point(targetSize.Width - popupSize.Width, -popupSize.Height), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(targetSize.Width - popupSize.Width, targetSize.Height), PopupPrimaryAxis.Horizontal),
            };

        private static void OnOpensUpwardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ToolBar toolBar)
                return;

            toolBar.Loaded -= OnLoaded;
            if (!(bool)e.NewValue)
                return;

            toolBar.Loaded += OnLoaded;
            if (toolBar.IsLoaded)
                Apply(toolBar);
        }

        // A theme change applies a new template, so the placement is set at every load.
        private static void OnLoaded(object sender, RoutedEventArgs e) => Apply((ToolBar)sender);

        private static void Apply(ToolBar toolBar)
        {
            var popup = FindOverflowPopup(toolBar);
            if (popup == null)
                return;

            popup.Placement = PlacementMode.Custom;
            popup.CustomPopupPlacementCallback = PlaceAboveRightAligned;
        }
    }
}
