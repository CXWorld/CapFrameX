using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public class ComparisonColorManager
	{
		private static readonly SolidColorBrush[] _comparisonBrushes =
			new SolidColorBrush[]
			{
						// CX Green
						new SolidColorBrush(Color.FromRgb(156, 210, 0)),
						// CX Orange
						new SolidColorBrush(Color.FromRgb(241, 125, 32)),
						// CX Blue
						new SolidColorBrush(Color.FromRgb(34, 151, 243)),                
						// Light Orange / Dark Yellow
						new SolidColorBrush(Color.FromRgb(255, 180, 0)),
						// Red
						new SolidColorBrush(Color.FromRgb(200, 0, 0)),
						// Purple
						new SolidColorBrush(Color.FromRgb(100, 0, 160)),
						// Pink
						new SolidColorBrush(Color.FromRgb(220, 0, 140)),
						// Cyan
						new SolidColorBrush(Color.FromRgb(40, 225, 200)),
						// Brown
						new SolidColorBrush(Color.FromRgb(180, 130, 0)),
						// Dark Blue
						new SolidColorBrush(Color.FromRgb(0, 0, 180)),
						// Black
						new SolidColorBrush(Color.FromRgb(0, 0, 0))
			};

		private readonly Dictionary<int, bool> _usedColorDictionary;

		public ComparisonColorManager()
		{
			_usedColorDictionary = new Dictionary<int, bool>();

			for (int i = 0; i < _comparisonBrushes.Length; i++)
			{
				_usedColorDictionary.Add(i, false);
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

		public void FreeColor(SolidColorBrush color)
		{
			int index = Array.IndexOf(_comparisonBrushes, color);

			if (index >= 0)
				_usedColorDictionary[index] = false;
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
