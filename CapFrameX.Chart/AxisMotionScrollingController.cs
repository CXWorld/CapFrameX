using System;
using System.Windows.Threading;

namespace CapFrameX.Chart
{
	public class AxisMotionScrollingController
	{
		private const int ANIMATION_INTERVAL_MS = 33;

		private const double DECELERATION_FACTOR = 0.997;

		private const double ANIMATION_STOP_THRESHOLD = 0.05;

		private const double ANIMATION_START_THRESHOLD = 0.5;

		private const int STOP_INPUT_MINIMUM_VELOCITY_UPDATE_DELAY = 50;

		public bool Enabled { get; set; }

		public double MinValue { get; set; }

		public double MaxValue { get; set; }

		public double CurValue
		{
			get { return innerCurValue; }
			set
			{
				if (innerCurValue != value)
				{
					innerCurValue = value;
					curVelocity = 0;
				}
			}
		}

		public bool EnableFrictionScrolling { get; set; }

		private readonly DispatcherTimer animationTimer;

		private readonly Action<double> valueUpdateAction;

		private bool inputCaptured;

		private double innerCurValue;

		private double curVelocity;

		private double lastPosition;

		private long lastTime;

		public AxisMotionScrollingController(Action<double> valueUpdateAction, bool enableFrictionScrolling = true)
		{
			this.valueUpdateAction = valueUpdateAction;
			animationTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(ANIMATION_INTERVAL_MS)
			};

			animationTimer.Tick += AnimationTimerOnTick;
			EnableFrictionScrolling = enableFrictionScrolling;
		}
	
		private void AnimationTimerOnTick(object sender, EventArgs eventArgs)
		{
			var curTime = GetCurTime();
			var dT = curTime - lastTime;
			lastTime = curTime;

			curVelocity *= Math.Pow(DECELERATION_FACTOR, dT);

			if (Math.Abs(curVelocity) < ANIMATION_STOP_THRESHOLD)
			{
				animationTimer.Stop();
				curVelocity = 0;
				return;
			}

			IncrementCurValue(curVelocity * dT);
		}

		private static long GetCurTime()
		{
			return DateTime.Now.Ticks / 10000;
		}

		private void IncrementCurValue(double delta)
		{
			innerCurValue = BringInRange(innerCurValue + delta, MinValue, MaxValue);
			valueUpdateAction(innerCurValue);
		}

		private double BringInRange(double val, double min, double max)
		{
			if (min > max)
				throw new ArgumentException("min should be smaller then max");

			if (val < min) return min;
			if (val > max) return max;
			return val;
		}

		public void StartInput(double position)
		{
			if (!Enabled) return;

			animationTimer.Stop();
			inputCaptured = true;
			lastPosition = position;
			lastTime = GetCurTime();
		}

		public void StopInput(double position)
		{
			if (!inputCaptured) return;

			var curTime = GetCurTime();
			if (curTime - lastTime > STOP_INPUT_MINIMUM_VELOCITY_UPDATE_DELAY)
				IncrementInput(position);

			inputCaptured = false;
			if (Enabled && EnableFrictionScrolling && Math.Abs(curVelocity) >= ANIMATION_START_THRESHOLD)
				animationTimer.Start();
		}

		public void IncrementInput(double position)
		{
			if (!inputCaptured || !Enabled) return;

			var dP = position - lastPosition;
			var dT = GetCurTime() - lastTime;
			lastPosition = position;
			lastTime += dT;

			IncrementCurValue(dP);

			curVelocity = dP / dT;
		}
	}
}
