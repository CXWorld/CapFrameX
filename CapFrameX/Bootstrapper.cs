using DryIoc;
using Prism.DryIoc;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows;

namespace CapFrameX
{
    public class Bootstrapper : DryIocBootstrapper
    {
        protected override DependencyObject CreateShell()
        {
            return Container.Resolve<Shell>();
        }

        protected override void InitializeShell()
        {
            base.InitializeShell();
            Application.Current.MainWindow = (Window)Shell;
            Application.Current.MainWindow.Show();
        }

        protected override void ConfigureContainer()
        {
            // Prism
            Container.Register<IRegionManager, RegionManager>(Reuse.Singleton);

            base.ConfigureContainer();
        }

        /// <summary>
        /// https://www.c-sharpcorner.com/forums/using-ioc-with-wpf-prism-in-mvvm
        /// </summary>
        protected override void ConfigureViewModelLocator()
        {
            ViewModelLocationProvider.SetDefaultViewTypeToViewModelTypeResolver(x =>
            {
                var viewName = x.FullName;

                // Convention
                viewName = viewName.Replace(".View.", ".ViewModel.");
                viewName = viewName.Replace(".Views.", ".ViewModels.");
                var viewAssemblyName = x.GetTypeInfo().Assembly.FullName;

                // Saving ViewModels in another assembly.
                viewAssemblyName = viewAssemblyName.Replace("View", "ViewModel");
                var suffix = viewName.EndsWith("View", StringComparison.Ordinal) ? "Model" : "ViewModel";
                var viewModelName = string.Format(CultureInfo.InvariantCulture, "{0}{1}, {2}", viewName, suffix, viewAssemblyName);
                var t = Type.GetType(viewModelName);
                return t;
            });

            base.ConfigureViewModelLocator();
            ViewModelLocationProvider.SetDefaultViewModelFactory(type => Container.Resolve(type, IfUnresolved.ReturnDefault));
        }

        protected override void ConfigureModuleCatalog()
        {
            base.ConfigureModuleCatalog();

            ModuleCatalog moduleCatalog = (ModuleCatalog)ModuleCatalog;
            moduleCatalog.AddModule(typeof(CapFrameXViewRegion));
        }
    }
}
