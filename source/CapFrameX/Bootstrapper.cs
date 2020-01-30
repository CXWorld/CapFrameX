using CapFrameX.Data;
using CapFrameX.Statistics;
using DryIoc;
using Prism.DryIoc;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Events;
using Prism.Regions;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Configuration;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.PresentMonInterface;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.MVVM;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Overlay;
using Serilog;
using Microsoft.Extensions.Logging;
using CapFrameX.Extensions;
using System.IO;
using CapFrameX.Contracts.UpdateCheck;
using CapFrameX.Updater;
using Serilog.Formatting.Compact;

namespace CapFrameX
{
	public class Bootstrapper : DryIocBootstrapper
	{

		protected override DependencyObject CreateShell()
		{
			var shell = Container.Resolve<Shell>();
			Container.UseInstance<IShell>(shell);
			return shell;
		}

		protected override void InitializeShell()
		{
			base.InitializeShell();
			LogAppInfo();
			Application.Current.MainWindow = (Window)Shell;
			Application.Current.MainWindow.Show();
		}

		protected override void ConfigureContainer()
		{
			base.ConfigureContainer();

			// Vertical components
			Container.Register<IEventAggregator, EventAggregator>(Reuse.Singleton, null, null, IfAlreadyRegistered.Replace, "EventAggregator");
			Container.Register<IAppConfiguration, CapFrameXConfiguration>(Reuse.Singleton);
			Container.ConfigureSerilogILogger(CreateLoggerConfiguration(Container.Resolve<IAppConfiguration>()));

			// Prism
			Container.Register<IRegionManager, RegionManager>(Reuse.Singleton, null, null, IfAlreadyRegistered.Replace, "RegionManager");

			// Core components
			Container.Register<IRecordDirectoryObserver, RecordDirectoryObserver>(Reuse.Singleton);
			Container.Register<IStatisticProvider, FrametimeStatisticProvider>(Reuse.Singleton);
			Container.Register<IFrametimeAnalyzer, FrametimeAnalyzer>(Reuse.Singleton);
			Container.Register<ICaptureService, PresentMonCaptureService>(Reuse.Singleton);
			Container.Register<IOverlayService, OverlayService>(Reuse.Singleton);
			Container.Register<IOverlayEntryProvider, OverlayEntryProvider>(Reuse.Singleton);
			Container.Register<IRecordDataProvider, RecordDataProvider>(Reuse.Singleton);
			Container.Register<IAppVersionProvider, AppVersionProvider>(Reuse.Singleton);
			Container.RegisterInstance<IWebVersionProvider>(new WebVersionProvider(), Reuse.Singleton);
			Container.Register<IUpdateCheck, UpdateCheck>(Reuse.Singleton);
		}

		/// <summary>
		/// https://www.c-sharpcorner.com/forums/using-ioc-with-wpf-prism-in-mvvm
		/// </summary>
		protected override void ConfigureViewModelLocator()
		{
			base.ConfigureViewModelLocator();

			ViewModelLocationProvider.SetDefaultViewTypeToViewModelTypeResolver(viewType =>
			{
				var viewName = viewType.FullName;

				// Naming convention
				viewName = viewName.Replace(".View.", ".ViewModel.");
				viewName = viewName.Replace(".Views.", ".ViewModels.");
				var viewAssemblyName = viewType.GetTypeInfo().Assembly.FullName;

				// Saving ViewModels in another assembly.
				viewAssemblyName = viewAssemblyName.Replace("View", "ViewModel");
				var suffix = viewName.EndsWith("View", StringComparison.Ordinal) ? "Model" : "ViewModel";
				var viewModelName = string.Format(CultureInfo.InvariantCulture, "{0}{1}, {2}", viewName, suffix, viewAssemblyName);
				var t = Type.GetType(viewModelName);
				return t;
			});

			ViewModelLocationProvider.SetDefaultViewModelFactory(type => Container.Resolve(type, IfUnresolved.Throw));
		}

		protected override void ConfigureModuleCatalog()
		{
			base.ConfigureModuleCatalog();

			ModuleCatalog moduleCatalog = (ModuleCatalog)ModuleCatalog;
			moduleCatalog.AddModule(typeof(CapFrameXViewRegion));
		}

		private LoggerConfiguration CreateLoggerConfiguration(IAppConfiguration appConfiguration)
		{
			var path = appConfiguration.LoggingDirectory;
			if (path.Contains(@"MyDocuments\CapFrameX\Logs"))
			{
				path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"CapFrameX\Logs");
			}
			return new LoggerConfiguration()
				.MinimumLevel.Debug()
				.Enrich.FromLogContext()
				.WriteTo.File(
					path: Path.Combine(path, "CapFrameX.log"),
					fileSizeLimitBytes: 10240,
					formatter: new CompactJsonFormatter()
				);
		}

		private void LogAppInfo()
		{
			var loggerFactory = Container.Resolve<ILoggerFactory>();
			var version = Container.Resolve<IAppVersionProvider>().GetAppVersion().ToString();
			loggerFactory.CreateLogger<ILogger<Bootstrapper>>().LogInformation("CapFrameX {version} started", version);
		}
	}
}
