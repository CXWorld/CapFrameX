namespace CapFrameX.Contracts.Localization
{
    public sealed class LanguageOption
    {
        public LanguageOption(string code, string name)
        {
            Code = code;
            Name = name;
        }

        public string Code { get; }
        public string Name { get; }
    }
}
