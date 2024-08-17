using CapFrameX.Contracts.MVVM;
using System;
using System.Windows;

namespace CapFrameX.MVVM.AttachedProperties
{
	public class MouseHelper : DependencyObject
	{
		public static readonly DependencyProperty IsMouseOverProperty = DependencyProperty.RegisterAttached(
			"IsMouseOver", typeof(bool), typeof(MouseHelper), new PropertyMetadata(PropertyChangedCallback));

		public static void PropertyChangedCallback(DependencyObject depObj, DependencyPropertyChangedEventArgs args)
		{
			var control = depObj as FrameworkElement;
			var eventHandler = control?.DataContext as IMouseEventHandler;

			if ((bool)args.NewValue)
				eventHandler?.OnMouseEnter();
			else
				eventHandler?.OnMouseLeave();
		}

		public static void SetIsMouseOver(DependencyObject target, Boolean value)
		{
			target.SetValue(IsMouseOverProperty, value);
		}

		public static bool GetIsMouseOver(DependencyObject target)
		{
			return (bool)target.GetValue(IsMouseOverProperty);
		}
	}
}
