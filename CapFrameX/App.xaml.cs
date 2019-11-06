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
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			Bootstrapper bootstrapper = new Bootstrapper();
			bootstrapper.Run(true);
		}

		private void CapFrameXExit(object sender, ExitEventArgs e)
		{
			PresentMonCaptureService.TryKillPresentMon();
		}

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			if (!IsAdministrator)
			{
				MessageBox.Show("Run CapFrameX as administrator. Right click on desktop shortcut" + Environment.NewLine
					+ "and got to Properties -> Shortcut -> Advanced then check option Run as administrator.");
				Current.Shutdown();
			}

			Process proc = Process.GetCurrentProcess();
			var count = Process.GetProcesses().Where(p =>
							 p.ProcessName == proc.ProcessName).Count();

			if (count > 1)
			{
				MessageBox.Show("Already an instance is running...");
				Current.Shutdown();
			}

			// check resource folder spelling
			try
			{
				var sourceFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
						@"CapFrameX\Ressources\");

				if (Directory.Exists(sourceFolder))
				{
					Directory.Move(sourceFolder, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
						@"CapFrameX\Resources\"));
				}
			}
			catch { }

			// compare ignore list
			try
			{
				var ignoreLiveListFilename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
						@"CapFrameX\Resources\ProcessIgnoreList.txt");
				var ignoreListFileName = Path.Combine("PresentMon", "ProcessIgnoreList.txt");

				if (File.Exists(ignoreLiveListFilename))
				{
					var processesLive = File.ReadAllLines(ignoreLiveListFilename);
					var processes = File.ReadAllLines(ignoreListFileName);

					var unionList = processesLive.Union(processes);
					File.WriteAllLines(ignoreLiveListFilename, unionList.OrderBy(name => name));
				}
			}
			catch { }

			// compare game name mapping list
			try
			{
				var gameNameLiveListFilename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					@"CapFrameX\Resources\ProcessGameNameMatchingList.txt");
				var gameNameListFileName = Path.Combine("NameMatching", "ProcessGameNameMatchingList.txt");

				if (File.Exists(gameNameLiveListFilename))
				{
					var gameNamesLive = File.ReadAllLines(gameNameLiveListFilename);
					var gameNames = File.ReadAllLines(gameNameListFileName);

					var unionList = gameNamesLive.Union(gameNames);
					File.WriteAllLines(gameNameLiveListFilename, unionList.OrderBy(name => name));
				}
			}
			catch { }
		}

		public static bool IsAdministrator =>
			new WindowsPrincipal(WindowsIdentity.GetCurrent())
				   .IsInRole(WindowsBuiltInRole.Administrator);
	}
}
