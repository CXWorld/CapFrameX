﻿namespace CapFrameX.Contracts.MVVM
{
	public interface IShell
	{
		System.Windows.Controls.ContentControl GlobalScreenshotArea { get; }

		bool IsGpuAccelerationActive { get; set; }
	}
}
