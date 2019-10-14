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
				switch (_comparisonContext)
				{
					case EComparisonContext.DateTime:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title =
									GetLabelDateTimeContext(ComparisonRecords[i], GetMaxDateTimeAlignment());
							}
						}
						break;
					case EComparisonContext.CPU:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title =
									GetLabelCpuContext(ComparisonRecords[i], GetMaxCpuAlignment());
							}
						}
						break;
					case EComparisonContext.GPU:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title =
									GetLabelGpuContext(ComparisonRecords[i], GetMaxGpuAlignment());
							}
						}
						break;
					case EComparisonContext.Custom:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title =
									GetLabelCustomContext(ComparisonRecords[i], GetMaxCommentAlignment());
							}
						}
						break;
					default:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title =
									GetLabelDateTimeContext(ComparisonRecords[i], GetMaxDateTimeAlignment());
							}
						}
						break;
				}
			}

			ComparisonModel.InvalidatePlot(false);
		}

		private int GetMaxDateTimeAlignment()
		{
			var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
			var maxDateTimeLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.DateTime.Length);

			return Math.Max(maxGameNameLength, maxDateTimeLength);
		}

		private int GetMaxCommentAlignment()
		{
			var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
			var maxContextLength = ComparisonRecords.Max(record
				=> record.WrappedRecordInfo.FileRecordInfo.Comment.SplitWordWise(PART_LENGTH).Max(part => part.Length));

			return Math.Max(maxGameNameLength, maxContextLength);
		}

		private int GetMaxGpuAlignment()
		{
			var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
			var maxGpuLength = ComparisonRecords.Max(record
				=> record.WrappedRecordInfo.FileRecordInfo.GraphicCardName.SplitWordWise(PART_LENGTH).Max(part => part.Length));

			return Math.Max(maxGameNameLength, maxGpuLength);
		}

		private int GetMaxCpuAlignment()
		{
			var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
			var maxCpuLength = ComparisonRecords.Max(record
				=> record.WrappedRecordInfo.FileRecordInfo.ProcessorName.SplitWordWise(PART_LENGTH).Max(part => part.Length));

			return Math.Max(maxGameNameLength, maxCpuLength);
		}

		private void OnCustomContex()
		{
			_comparisonContext = EComparisonContext.Custom;
			SetLabelCustomContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void OnGpuContex()
		{
			_comparisonContext = EComparisonContext.GPU;
			SetLabelGpuContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void OnCpuContext()
		{
			_comparisonContext = EComparisonContext.CPU;
			SetLabelCpuContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void OnDateTimeContext()
		{
			_comparisonContext = EComparisonContext.DateTime;
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
					ComparisonModel.Series[i].Title =
						GetLabelDateTimeContext(ComparisonRecords[i], GetMaxDateTimeAlignment());
				}
			}
		}

		private string GetLabelDateTimeContext(ComparisonRecordInfoWrapper record, int maxAlignment)
		{
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			var dateTime = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.DateTime);
			return gameName + Environment.NewLine + dateTime;
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
					ComparisonModel.Series[i].Title = GetLabelCpuContext(ComparisonRecords[i], GetMaxCpuAlignment());
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

			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			return gameName + infoPartsFormatted;
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
					ComparisonModel.Series[i].Title = GetLabelGpuContext(ComparisonRecords[i], GetMaxGpuAlignment());
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

				infoPartsFormatted += Environment.NewLine + string.Format(CultureInfo.InvariantCulture, alignmentFormat, part);
			}

			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			return gameName + Environment.NewLine + infoPartsFormatted;
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
					ComparisonModel.Series[i].Title = GetLabelCustomContext(ComparisonRecords[i], GetMaxCommentAlignment());
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

			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			return gameName + infoPartsFormatted;
		}
	}
}
