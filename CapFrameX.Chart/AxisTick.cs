using System.Windows;
using System.Windows.Media;

namespace CapFrameX.Chart
{
	public class AxisTick : FrameworkElement
	{
		public static readonly DependencyProperty TicksProperty = DependencyProperty.Register(
		"Ticks", typeof(int), typeof(AxisTick), new FrameworkPropertyMetadata(8, FrameworkPropertyMetadataOptions.AffectsRender));

		public int Ticks
		{
			get { return (int)GetValue(TicksProperty); }
			set { SetValue(TicksProperty, value); }
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			InvalidateVisual();
		}
	}
}
