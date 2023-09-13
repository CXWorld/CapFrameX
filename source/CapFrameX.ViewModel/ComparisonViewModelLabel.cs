using CapFrameX.Data;
using CapFrameX.Extensions;
using System;
using System.Linq;

namespace CapFrameX.ViewModel
{
	public partial class ComparisonViewModel
	{
		private void OnShowContextLegendChanged()
		{
			if (!ComparisonRecords.Any())
				return;

			if (!IsContextLegendActive)
			{
				ComparisonFrametimesModel.Series.ForEach(series => series.Title = null);
				ComparisonFpsModel.Series.ForEach(series => series.Title = null);
			}
			else
			{
				OnComparisonContextChanged();
			}

			ComparisonFrametimesModel.InvalidatePlot(true);
			ComparisonFpsModel.InvalidatePlot(true);
		}

		private string[] GetLabelForContext(ComparisonRecordInfo record, EComparisonContext context)
		{
			switch(context)
			{
				case EComparisonContext.CPU:
					return GetLabelLines(record.FileRecordInfo.ProcessorName);
				case EComparisonContext.GPU:
					return GetLabelLines(record.FileRecordInfo.GraphicCardName);
				case EComparisonContext.SystemRam:
					return GetLabelLines(record.FileRecordInfo.SystemRamInfo);
				case EComparisonContext.DateTime:
					return GetLabelLines($"{record.FileRecordInfo.CreationDate} { record.FileRecordInfo.CreationTime}");
				case EComparisonContext.Custom:
					return GetLabelLines(record.FileRecordInfo.Comment);
                case EComparisonContext.GPUDriver:
                    return GetLabelLines(record.FileRecordInfo.GPUDriverVersion);
                case EComparisonContext.API:
                    return GetLabelLines(record.FileRecordInfo.ApiInfo);
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
