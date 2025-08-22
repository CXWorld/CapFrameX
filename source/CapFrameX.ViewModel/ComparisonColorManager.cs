using CapFrameX.Contracts.Configuration;
using CapFrameX.ViewModel.SubModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public class ComparisonColorManager
	{
        private static readonly SolidColorBrush[] _comparisonBrushes =
            Enumerable.Range(0, 11)
                      .Select(_ => new SolidColorBrush(Colors.Transparent))
                      .ToArray();



        private readonly Dictionary<int, bool> _usedColorDictionary;

		public ComparisonColorManager()
		{
            _usedColorDictionary = new Dictionary<int, bool>();

			for (int i = 0; i < _comparisonBrushes.Length; i++)
			{
				_usedColorDictionary.Add(i, false);
			}
		}

        public void SetColors(ObservableCollection<ComparisonColorItems> lineGraphColors)
        {
            if (lineGraphColors == null)
                return;

            for (int i = 0; i < _comparisonBrushes.Length && i < lineGraphColors.Count; i++)
            {
                _comparisonBrushes[i] = new SolidColorBrush(lineGraphColors[i].Color);
            }
        }

        public SolidColorBrush GetNextFreeColor()
		{
			var entry = _usedColorDictionary.Where(e => e.Value == false)
				.Select(e => (KeyValuePair<int, bool>?)e)
				.FirstOrDefault();

			if (entry == null)
				return _comparisonBrushes.Last();

			int index = entry.Value.Key;
			_usedColorDictionary[index] = true;
			return _comparisonBrushes[index];
		}

		public void LockColorOnChange(SolidColorBrush color)
		{
			for (int i = 0; i < _comparisonBrushes.Length; i++)
			{
				if (_comparisonBrushes[i].Color == color.Color)
				{
					_usedColorDictionary[i] = true;
					break;
				}
			}

		}

		public void FreeColor(SolidColorBrush color)
		{
			for (int i = 0; i < _comparisonBrushes.Length; i++)
			{
				if (_comparisonBrushes[i].Color == color.Color)
				{ 
					_usedColorDictionary[i] = false;
					break;
				}
			}
		}

		public void FreeAllColors()
		{
			for (int i = 0; i < _comparisonBrushes.Length; i++)
			{
				_usedColorDictionary[i] = false;
			}
		}
	}
}
