﻿using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.PresentMonInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;

namespace CapFrameX
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private Bootstrapper _bootstrapper;

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			_bootstrapper = new Bootstrapper();
			_bootstrapper.Run(true);
		}

		private void CapFrameXExit(object sender, ExitEventArgs e)
		{
			PresentMonCaptureService.TryKillPresentMon();

			var overlayService = _bootstrapper.Container.Resolve(typeof(IOverlayService), true) as IOverlayService;
			overlayService?.IsOverlayActiveStream.OnNext(false);

			//var overlayEntryProvider = _bootstrapper.Container.Resolve(typeof(IOverlayEntryProvider), true) as IOverlayEntryProvider;
			//_ = overlayEntryProvider?.SaveOverlayEntriesToJson();

			var sensorService = _bootstrapper.Container.Resolve(typeof(ISensorService), true) as ISensorService;
			sensorService?.CloseOpenHardwareMonitor();
		}

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			if (!IsAdministrator)
			{
				MessageBox.Show("Run CapFrameX as administrator. Right click on desktop shortcut" + Environment.NewLine
					+ "and got to Properties -> Shortcut -> Advanced then check option Run as administrator.");
				Current.Shutdown();
			}

			Process currentProcess = Process.GetCurrentProcess();
			if (Process.GetProcesses().Any(p => p.ProcessName == currentProcess.ProcessName && p.Id != currentProcess.Id))
			{
				MessageBox.Show("Already an instance running...");
				Current.Shutdown();
			}

			// check resource folder spelling
			try
			{
				var sourceFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					@"CapFrameX\Ressources\");
				var destinationFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					@"CapFrameX\Resources\");

				if (Directory.Exists(sourceFolder))
				{
					Directory.Move(sourceFolder, destinationFolder);
				}

				if (!Directory.Exists(destinationFolder))
				{
					Directory.CreateDirectory(destinationFolder);
				}
			}
			catch { }

			// create capture folder
			try
			{
				var captureFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					@"CapFrameX\Captures\");

				if (!Directory.Exists(captureFolder))
				{
					Directory.CreateDirectory(captureFolder);
				}
			}
			catch { }
		}

		public static bool IsAdministrator =>
			new WindowsPrincipal(WindowsIdentity.GetCurrent())
			.IsInRole(WindowsBuiltInRole.Administrator);

	}
}
