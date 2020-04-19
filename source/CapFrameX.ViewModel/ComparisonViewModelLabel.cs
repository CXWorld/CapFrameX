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
				OnComparisonContextChanged();
			}

			ComparisonModel.InvalidatePlot(true);
		}

		private string[] GetLabelForContext(ComparisonRecordInfoWrapper record, EComparisonContext context)
		{
			switch(context)
			{
				case EComparisonContext.CPU:
					return GetLabelLines(record.WrappedRecordInfo.FileRecordInfo.ProcessorName);
				case EComparisonContext.GPU:
					return GetLabelLines(record.WrappedRecordInfo.FileRecordInfo.GraphicCardName);
				case EComparisonContext.SystemRam:
					return GetLabelLines(record.WrappedRecordInfo.FileRecordInfo.SystemRamInfo);
				case EComparisonContext.DateTime:
					return GetLabelLines($"{record.WrappedRecordInfo.FileRecordInfo.CreationDate} { record.WrappedRecordInfo.FileRecordInfo.CreationTime}");
				case EComparisonContext.Custom:
					return GetLabelLines(record.WrappedRecordInfo.FileRecordInfo.Comment);
				default:
					return Array.Empty<string>();
			}
		}

		private string[] GetLabelLines(string rawLabel)
		{
			if(string.IsNullOrWhiteSpace(rawLabel))
			{
				return Array.Empty<string>();
			}
			var labelParts = rawLabel.SplitWordWise(PART_LENGTH);

			return labelParts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
		}
	}
}
