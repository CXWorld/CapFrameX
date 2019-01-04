using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
using GongSolutions.Wpf.DragDrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;

namespace CapFrameX.ViewModel
{
	public class ReportViewModel : BindableBase, INavigationAware, IDropTarget
	{
		private readonly IStatisticProvider _frametimeStatisticProvider;
		private readonly IEventAggregator _eventAggregator;

		private bool _useEventMessages;

		public ObservableCollection<ReportInfo> ReportInfoCollecion { get; }
			= new ObservableCollection<ReportInfo>();

		public ICommand CopyTableDataCommand { get; }

		public ReportViewModel(IStatisticProvider frametimeStatisticProvider, IEventAggregator eventAggregator)
		{
			_frametimeStatisticProvider = frametimeStatisticProvider;
			_eventAggregator = eventAggregator;

			CopyTableDataCommand = new DelegateCommand(OnCopyTableData);

			SubscribeToSelectRecord();
		}

		private void OnCopyTableData()
		{
			if (!ReportInfoCollecion.Any())
				return;

			StringBuilder builder = new StringBuilder();

			// Header
			var displayNameGame = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.Game);
			var displayNameDate = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.Date);
			var displayNameTime = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.Time);
			var displayNameNumberOfSamples = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.NumberOfSamples);
			var displayNameRecordTime = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.RecordTime);
			var displayNameCpu = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.Cpu);
			var displayNameGraphicCard = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GraphicCard);
			var displayNameMaxFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.MaxFps);
			var displayNameAverageFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.AverageFps);
			var displayNameOnePercentQuantileFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.OnePercentQuantileFps);
			var displayNameZeroDotOnePercentQuantileFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.ZeroDotOnePercentQuantileFps);
			var displayNameMinFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.MinFps);
			var displayNameAdaptiveSTDFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.AdaptiveSTDFps);

			builder.Append(displayNameGame + "\t" +
						   displayNameDate + "\t" +
						   displayNameTime + "\t" +
						   displayNameNumberOfSamples + "\t" +
						   displayNameRecordTime + "\t" +
						   displayNameCpu + "\t" +
						   displayNameGraphicCard + "\t" +
						   displayNameMaxFps + "\t" +
						   displayNameAverageFps + "\t" +
						   displayNameOnePercentQuantileFps + "\t" +
						   displayNameZeroDotOnePercentQuantileFps + "\t" +
						   displayNameMinFps + "\t" +
						   displayNameAdaptiveSTDFps + 
						   Environment.NewLine);

			foreach (var reportInfo in ReportInfoCollecion)
			{				
				builder.Append(reportInfo.Game + "\t" + 
							   reportInfo.Date + "\t" +
							   reportInfo.Time + "\t" +
							   reportInfo.NumberOfSamples + "\t" +
							   reportInfo.RecordTime + "\t" +
							   reportInfo.Cpu + "\t" +
							   reportInfo.GraphicCard + "\t" +
							   reportInfo.MaxFps + "\t" +
							   reportInfo.AverageFps + "\t" +
							   reportInfo.OnePercentQuantileFps + "\t" +
							   reportInfo.ZeroDotOnePercentQuantileFps + "\t" +
							   reportInfo.MinFps + "\t" +
							   reportInfo.AdaptiveSTDFps +
							   Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void SubscribeToSelectRecord()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>()
							.Subscribe(msg =>
							{
								if (_useEventMessages)
								{
									ReportInfo reportInfo = GetReportInfoFromRecordInfo(msg.RecordInfo);
									AddReportRecord(reportInfo);
								}
							});
		}

		private ReportInfo GetReportInfoFromRecordInfo(OcatRecordInfo recordInfo)
		{
			var session = RecordManager.LoadData(recordInfo.FullPath);

			var fpsSequence = session.FrameTimes.Select(ft => 1000 / ft).ToList();
			var max = Math.Round(fpsSequence.Max(), 0);
			var average = Math.Round(fpsSequence.Average(), 0);
			var p1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fpsSequence, 0.01), 0);
			var p0dot1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fpsSequence, 0.001), 0);
			var min = Math.Round(fpsSequence.Min(), 0);
			var adaptiveStandardDeviation = Math.Round(_frametimeStatisticProvider.GetAdaptiveStandardDeviation(fpsSequence, 20), 0);

			var reportInfo = new ReportInfo()
			{
				Game = recordInfo.GameName,
				Date = recordInfo.CreationDate,
				Time = recordInfo.CreationTime,
				NumberOfSamples = session.FrameTimes.Count,
				RecordTime = Math.Round(session.LastFrameTime, 2).ToString(CultureInfo.InvariantCulture),
				Cpu = session.ProcessorName.Trim(new Char[] { ' ', '"' }),
				GraphicCard = session.GraphicCardName.Trim(new Char[] { ' ', '"' }),
				MaxFps = max,
				AverageFps = average,
				OnePercentQuantileFps = p1_quantile,
				ZeroDotOnePercentQuantileFps = p0dot1_quantile,
				MinFps = min,
				AdaptiveSTDFps = adaptiveStandardDeviation
			};

			return reportInfo;
		}

		private void AddReportRecord(ReportInfo reportInfo)
		{
			ReportInfoCollecion.Add(reportInfo);
		}

		public bool IsNavigationTarget(NavigationContext navigationContext)
		{
			return true;
		}

		public void OnNavigatedFrom(NavigationContext navigationContext)
		{
			_useEventMessages = false;
		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			_useEventMessages = true;
		}

		void IDropTarget.DragOver(IDropInfo dropInfo)
		{
			if (dropInfo != null)
			{
				dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
				dropInfo.Effects = DragDropEffects.Move;
			}
		}

		void IDropTarget.Drop(IDropInfo dropInfo)
		{
			if (dropInfo != null)
			{
				if (dropInfo.VisualTarget is FrameworkElement frameworkElement)
				{
					if (frameworkElement.Name == "ReportDataGrid")
					{
						if (dropInfo.Data is OcatRecordInfo recordInfo)
						{
							ReportInfo reportInfo = GetReportInfoFromRecordInfo(recordInfo);
							AddReportRecord(reportInfo);
						}
					}
					else if (frameworkElement.Name == "RemoveRecordItemControl")
					{
						if (dropInfo.Data is ReportInfo reportInfo)
						{
							ReportInfoCollecion.Remove(reportInfo);
						}
					}
				}
			}
		}
	}
}
