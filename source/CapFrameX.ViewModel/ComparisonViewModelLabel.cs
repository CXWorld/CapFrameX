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

		private string[] GetLabelForContext(ComparisonRecordInfoWrapper record, EComparisonContext context)
		{
			switch(context)
			{
				case EComparisonContext.CPU:
					return GetLabelLines(record.WrappedRecordInfo.FileRecordInfo.ProcessorName, GetMaxCpuAlignment());
				case EComparisonContext.GPU:
					return GetLabelLines(record.WrappedRecordInfo.FileRecordInfo.GraphicCardName, GetMaxGpuAlignment());
				case EComparisonContext.SystemRam:
					return GetLabelLines(record.WrappedRecordInfo.FileRecordInfo.SystemRamInfo, GetMaxSystemRamAlignment());
				case EComparisonContext.DateTime:
					return GetLabelLines($"{record.WrappedRecordInfo.FileRecordInfo.CreationDate} { record.WrappedRecordInfo.FileRecordInfo.CreationTime}", GetMaxDateTimeAlignment());
				case EComparisonContext.Custom:
					return GetLabelLines(record.WrappedRecordInfo.FileRecordInfo.Comment, GetMaxCommentAlignment());
				case EComparisonContext.None:
					return Array.Empty<string>();
				default:
					return Array.Empty<string>();
			}
		}

		private string[] GetLabelLines(string rawLabel, int maxAlignment)
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
