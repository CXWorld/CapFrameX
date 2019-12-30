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
			Application.Current.MainWindow = (Window)Shell;
			Application.Current.MainWindow.Show();
		}

		protected override void ConfigureContainer()
		{
			base.ConfigureContainer();

			// Vertical components
			Container.Register<IEventAggregator, EventAggregator>(Reuse.Singleton, null, null, IfAlreadyRegistered.Replace, "EventAggregator");
			Container.Register<IAppConfiguration, CapFrameXConfiguration>(Reuse.Singleton);

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
	}
}
