
using CapFrameX.Contracts.Overlay;
using Prism.Mvvm;

namespace CapFrameX.Data
{
	public class OverlayEntryFormatChange : BindableBase, IOverlayEntryFormatChange
	{
		private bool _colorsSelected = true;
		private bool _limitsSelected = true;
		private bool _formatSelected = true;

		public bool Colors
		{
			get
			{
				return _colorsSelected;
			}
			set
			{
				_colorsSelected = value;
				RaisePropertyChanged();
			}
		}

		public bool Limits
		{
			get
			{
				return _limitsSelected;
			}
			set
			{
				_limitsSelected = value;
				RaisePropertyChanged();
			}
		}
		public bool Format
		{
			get
			{
				return _formatSelected;
			}
			set
			{
				_formatSelected = value;
				RaisePropertyChanged();
			}
		}

	}
}
