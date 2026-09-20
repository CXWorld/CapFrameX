namespace CapFrameX.Service.Contracts.App;

public sealed record AppVersionDto(
    string ApplicationName,
    string Version,
    string InformationalVersion,
    string TargetFramework,
    string ProcessArchitecture,
    string Platform);
