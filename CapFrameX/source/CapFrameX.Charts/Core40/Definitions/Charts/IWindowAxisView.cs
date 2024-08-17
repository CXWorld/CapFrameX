namespace LiveCharts.Definitions.Charts
{
	/// <summary>
	/// IWindowAxisView
	/// </summary>
	public interface IWindowAxisView : IAxisView
    {
		/// <summary>
		/// SetSelectedWindow
		/// </summary>
		/// <param name="window"></param>
		void SetSelectedWindow(IAxisWindow window);
    }
}