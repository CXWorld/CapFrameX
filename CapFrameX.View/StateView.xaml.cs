using CapFrameX.Configuration;
using CapFrameX.OcatInterface;
using CapFrameX.PresentMonInterface;
using CapFrameX.ViewModel;
using Prism.Events;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für StateView.xaml
	/// </summary>
	public partial class StateView : UserControl
	{
		public StateView()
		{
			InitializeComponent();

            //OSMajorVersion.Text = $"OS major version: {Environment.OSVersion.Version.Major}";

            if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new StateViewModel( new RecordDirectoryObserver(appConfiguration), new EventAggregator(), appConfiguration, new PresentMonCaptureService());
			}
		}
	}
}
