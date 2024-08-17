using OxyPlot;

namespace CapFrameX.View.Controls
{
	public class CustomPanManipulator : MouseManipulator
	{
		public CustomPanManipulator(IPlotView plotView, EAxisDescription axisDescription)
			: base(plotView)
		{
			AxisDescription = axisDescription;
		}

		private ScreenPoint PreviousPosition { get; set; }

		private bool IsPanEnabled { get; set; }

		public EAxisDescription AxisDescription { get; }

		public override void Completed(OxyMouseEventArgs e)
		{
			base.Completed(e);
			if (!this.IsPanEnabled)
			{
				return;
			}

			this.View.SetCursorType(CursorType.Default);
			e.Handled = true;
		}

		public override void Delta(OxyMouseEventArgs e)
		{
			base.Delta(e);
			if (!this.IsPanEnabled)
			{
				return;
			}


			switch (AxisDescription)
			{
				case EAxisDescription.XY:
					if (this.XAxis != null)
					{
						this.XAxis.Pan(this.PreviousPosition, e.Position);
					}

					if (this.YAxis != null)
					{
						this.YAxis.Pan(this.PreviousPosition, e.Position);
					}
					break;
				case EAxisDescription.X:
					if (this.XAxis != null)
					{
						this.XAxis.Pan(this.PreviousPosition, e.Position);
					}
					break;
				case EAxisDescription.Y:
					if (this.YAxis != null)
					{
						this.YAxis.Pan(this.PreviousPosition, e.Position);
					}
					break;
			}



			this.PlotView.InvalidatePlot(false);
			this.PreviousPosition = e.Position;
			e.Handled = true;
		}

		public override void Started(OxyMouseEventArgs e)
		{
			base.Started(e);
			this.PreviousPosition = e.Position;

			this.IsPanEnabled = (this.XAxis != null && this.XAxis.IsPanEnabled)
								|| (this.YAxis != null && this.YAxis.IsPanEnabled);

			if (this.IsPanEnabled)
			{
				this.View.SetCursorType(CursorType.Pan);
				e.Handled = true;
			}
		}
	}
}
