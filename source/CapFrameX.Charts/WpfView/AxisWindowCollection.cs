using System.Collections.Generic;
using LiveCharts.Helpers;

namespace LiveCharts.Wpf
{
	/// <summary>
	/// AxisWindowCollection
	/// </summary>
	public class AxisWindowCollection : NoisyCollection<AxisWindow>
    {
		/// <summary>
		/// Ctor
		/// </summary>
        public AxisWindowCollection()
        {
            NoisyCollectionChanged += OnNoisyCollectionChanged;
        }

        private void OnNoisyCollectionChanged(IEnumerable<AxisWindow> oldItems, IEnumerable<AxisWindow> newItems)
        {
            
        }
    }
}