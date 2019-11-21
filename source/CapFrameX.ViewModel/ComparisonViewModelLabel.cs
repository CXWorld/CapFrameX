using CapFrameX.Extensions;
using System;
using System.Globalization;
using System.Linq;

namespace CapFrameX.ViewModel
{
	public partial class ComparisonViewModel
	{
		// partial void xyz -> initialize

		private void OnShowContextLegendChanged()
		{
			if (!ComparisonRecords.Any())
				return;

			if (!IsContextLegendActive)
			{
				ComparisonModel.Series.ForEach(series => series.Title = null);
			}
			else
			{
				switch (_selectedComparisonContext)
				{
					case EComparisonContext.DateTime:
						SetLabelDateTimeContext();
						break;
					case EComparisonContext.CPU:
						SetLabelCpuContext();
						break;
					case EComparisonContext.GPU:
						SetLabelGpuContext();
						break;
					case EComparisonContext.SystemRam:
						SetLabelSystemRamContext();
						break;
					case EComparisonContext.Custom:
						SetLabelCustomContext();
						break;
					default:
						SetLabelDateTimeContext();
						break;
				}
			}

			ComparisonModel.InvalidatePlot(true);
		}

		private int GetMaxDateTimeAlignment()
		{
			bool hasUniqueGameNames = GetHasUniqueGameNames();
			if (hasUniqueGameNames)
			{
				return ComparisonRecords.Max(record => record.WrappedRecordInfo.DateTime.Length);
			}
			else
			{
				var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
				var maxDateTimeLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.DateTime.Length);

				return Math.Max(maxGameNameLength, maxDateTimeLength);
			}
		}

		private int GetMaxCommentAlignment()
		{
			bool hasUniqueGameNames = GetHasUniqueGameNames();
			if (hasUniqueGameNames)
			{
				return ComparisonRecords.Max(record
				=> record.WrappedRecordInfo.FileRecordInfo.Comment.SplitWordWise(PART_LENGTH).Max(part => part.Length));
			}
			else
			{
				var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
				var maxContextLength = ComparisonRecords.Max(record
					=> record.WrappedRecordInfo.FileRecordInfo.Comment.SplitWordWise(PART_LENGTH).Max(part => part.Length));

				return Math.Max(maxGameNameLength, maxContextLength);
			}
		}

		private int GetMaxGpuAlignment()
		{
			bool hasUniqueGameNames = GetHasUniqueGameNames();
			if (hasUniqueGameNames)
			{
				return ComparisonRecords.Max(record
				=> record.WrappedRecordInfo.FileRecordInfo.GraphicCardName.SplitWordWise(PART_LENGTH).Max(part => part.Length));
			}
			else
			{
				var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
				var maxGpuLength = ComparisonRecords.Max(record
					=> record.WrappedRecordInfo.FileRecordInfo.GraphicCardName.SplitWordWise(PART_LENGTH).Max(part => part.Length));

				return Math.Max(maxGameNameLength, maxGpuLength);
			}
		}

		private int GetMaxSystemRamAlignment()
		{
			bool hasUniqueGameNames = GetHasUniqueGameNames();
			if (hasUniqueGameNames)
			{
				return ComparisonRecords.Max(record
				=> record.WrappedRecordInfo.FileRecordInfo.SystemRamInfo.SplitWordWise(PART_LENGTH).Max(part => part.Length));
			}
			else
			{
				var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
				var maxSystemRamLength = ComparisonRecords.Max(record
					=> record.WrappedRecordInfo.FileRecordInfo.SystemRamInfo.SplitWordWise(PART_LENGTH).Max(part => part.Length));

				return Math.Max(maxGameNameLength, maxSystemRamLength);
			}
		}

		private int GetMaxCpuAlignment()
		{
			bool hasUniqueGameNames = GetHasUniqueGameNames();
			if (hasUniqueGameNames)
			{
				return ComparisonRecords.Max(record
				=> record.WrappedRecordInfo.FileRecordInfo.ProcessorName.SplitWordWise(PART_LENGTH).Max(part => part.Length));
			}
			else
			{
				var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
				var maxCpuLength = ComparisonRecords.Max(record
					=> record.WrappedRecordInfo.FileRecordInfo.ProcessorName.SplitWordWise(PART_LENGTH).Max(part => part.Length));

				return Math.Max(maxGameNameLength, maxCpuLength);
			}
		}

		private void OnCustomContex()
		{
			SetLabelCustomContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void OnGpuContex()
		{
			SetLabelGpuContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void OnSystemRamContex()
		{
			SetLabelSystemRamContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void OnCpuContext()
		{
			SetLabelCpuContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void OnDateTimeContext()
		{
			SetLabelDateTimeContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void SetLabelDateTimeContext()
		{
			ComparisonRowChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelDateTimeContext(record, GetMaxDateTimeAlignment());
			}).Reverse().ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					var wrappedComparisonInfo = ComparisonRecords[i];
					var chartTitle = $"{wrappedComparisonInfo.WrappedRecordInfo.FileRecordInfo.CreationDate} " +
						$"{ wrappedComparisonInfo.WrappedRecordInfo.FileRecordInfo.CreationTime}";
					ComparisonModel.Series[i].Title = chartTitle;
				}
			}
		}

		private string GetLabelDateTimeContext(ComparisonRecordInfoWrapper record, int maxAlignment)
		{
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";

			bool hasUniqueGameNames = GetHasUniqueGameNames();
			if (hasUniqueGameNames)
			{				
				return string.Format(CultureInfo.InvariantCulture, alignmentFormat,
					$"{record.WrappedRecordInfo.FileRecordInfo.CreationDate} { record.WrappedRecordInfo.FileRecordInfo.CreationTime}");
			}
			else
			{
				var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
				var dateTime = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.DateTime);
				return gameName + Environment.NewLine + dateTime;
			}
		}

		private void SetLabelCpuContext()
		{
			ComparisonRowChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelCpuContext(record, GetMaxCpuAlignment());
			}).Reverse().ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					var wrappedComparisonInfo = ComparisonRecords[i];
					var chartTitle = wrappedComparisonInfo.WrappedRecordInfo.FileRecordInfo.ProcessorName;
					ComparisonModel.Series[i].Title = chartTitle;
				}
			}
		}

		private string GetLabelCpuContext(ComparisonRecordInfoWrapper record, int maxAlignment)
		{
			var processorName = record.WrappedRecordInfo.FileRecordInfo.ProcessorName ?? "";
			var cpuInfoParts = processorName.SplitWordWise(PART_LENGTH);
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";

			var infoPartsFormatted = string.Empty;
			foreach (var part in cpuInfoParts)
			{
				if (part == string.Empty)
					continue;

				infoPartsFormatted += Environment.NewLine + string.Format(CultureInfo.InvariantCulture, alignmentFormat, part);
			}

			bool hasUniqueGameNames = GetHasUniqueGameNames();
			if (hasUniqueGameNames)
			{
				return infoPartsFormatted;
			}
			else
			{				
				var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
				return gameName + infoPartsFormatted;
			}
		}

		private void SetLabelGpuContext()
		{
			ComparisonRowChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelGpuContext(record, GetMaxGpuAlignment());
			}).Reverse().ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					var wrappedComparisonInfo = ComparisonRecords[i];
					var chartTitle = wrappedComparisonInfo.WrappedRecordInfo.FileRecordInfo.GraphicCardName;
					ComparisonModel.Series[i].Title = chartTitle;
				}
			}
		}

		private string GetLabelGpuContext(ComparisonRecordInfoWrapper record, int maxAlignment)
		{
			var graphicCardName = record.WrappedRecordInfo.FileRecordInfo.GraphicCardName ?? "";
			var gpuInfoParts = graphicCardName.SplitWordWise(PART_LENGTH);
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";

			var infoPartsFormatted = string.Empty;
			foreach (var part in gpuInfoParts)
			{
				if (part == string.Empty)
					continue;

				infoPartsFormatted += string.Format(CultureInfo.InvariantCulture, alignmentFormat, part) + Environment.NewLine;
			}

			bool hasUniqueGameNames = GetHasUniqueGameNames();
			if (hasUniqueGameNames)
			{
				return infoPartsFormatted;
			}
			else
			{			
				var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
				return gameName + Environment.NewLine + infoPartsFormatted;
			}
		}

		private void SetLabelSystemRamContext()
		{
			ComparisonRowChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelSystemRamContext(record, GetMaxSystemRamAlignment());
			}).Reverse().ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					var wrappedComparisonInfo = ComparisonRecords[i];
					var chartTitle = wrappedComparisonInfo.WrappedRecordInfo.FileRecordInfo.SystemRamInfo;
					ComparisonModel.Series[i].Title = chartTitle;
				}
			}
		}

		private string GetLabelSystemRamContext(ComparisonRecordInfoWrapper record, int maxAlignment)
		{
			var systemRamName = record.WrappedRecordInfo.FileRecordInfo.SystemRamInfo ?? "";
			var systemRamInfoParts = systemRamName.SplitWordWise(PART_LENGTH);
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";

			var infoPartsFormatted = string.Empty;
			foreach (var part in systemRamInfoParts)
			{
				if (part == string.Empty)
					continue;

				infoPartsFormatted +=
					string.Format(CultureInfo.InvariantCulture, alignmentFormat, part) + Environment.NewLine;
			}

			bool hasUniqueGameNames = GetHasUniqueGameNames();
			if (hasUniqueGameNames)
			{
				return infoPartsFormatted;
			}
			else
			{
				var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
				return gameName + Environment.NewLine + infoPartsFormatted;
			}
		}

		private void SetLabelCustomContext()
		{
			ComparisonRowChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelCustomContext(record, GetMaxCommentAlignment());
			}).Reverse().ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					var wrappedComparisonInfo = ComparisonRecords[i];
					var chartTitle = wrappedComparisonInfo.WrappedRecordInfo.FileRecordInfo.Comment;
					ComparisonModel.Series[i].Title = chartTitle;
				}
			}
		}

		private string GetLabelCustomContext(ComparisonRecordInfoWrapper record, int maxAlignment)
		{
			var comment = record.WrappedRecordInfo.FileRecordInfo.Comment ?? "";
			var commentParts = comment.SplitWordWise(PART_LENGTH);
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";

			var infoPartsFormatted = string.Empty;
			foreach (var part in commentParts)
			{
				if (part == string.Empty)
					continue;

				infoPartsFormatted += Environment.NewLine + string.Format(CultureInfo.InvariantCulture, alignmentFormat, part);
			}

			bool hasUniqueGameNames = GetHasUniqueGameNames();
			if (hasUniqueGameNames)
			{
				return infoPartsFormatted;
			}
			else
			{				
				var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
				return gameName + infoPartsFormatted;
			}
		}
	}
}
