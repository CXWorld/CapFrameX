namespace CapFrameX.Contracts.Data
{
	public interface ISystemInfoEntry
	{
		string IsSelected { get; set; }
		string Key { get; set; }
		string Letter { get; }
		string Value { get; set; }
	}
}