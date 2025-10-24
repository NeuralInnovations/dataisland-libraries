namespace Dataisland.Serilog.RequestLogging;

public sealed class RequestLoggingConfig
{
    public string? DefaultLevel { get; set; } = "Information";
    public List<PathLevelRule> PathLevels { get; set; } = new();
}

public sealed class PathLevelRule
{
    public string? Path { get; set; }
    public string? Level { get; set; }
    public bool PrefixMatch { get; set; } = true;
}

