namespace Dataisland.Serilog.RequestLogging;

public sealed class RequestLoggingConfig
{
    public string? DefaultLevel { get; set; } = "Information";
    public List<PathLevelRule> PathLevels { get; set; } = new();
    public BodyCaptureConfig BodyCapture { get; set; } = new();
}

public sealed class BodyCaptureConfig
{
    public bool Enabled { get; set; }
    public int MaxRequestBytes { get; set; } = 2048;
    public int MaxResponseBytes { get; set; } = 4096;
    public List<string> AllowedRequestFields { get; set; } = new();
    public List<string> AllowedResponseFields { get; set; } = new();
    public List<string> RedactedFields { get; set; } =
    [
        "access_token",
        "api_key",
        "authorization",
        "password",
        "refresh_token",
        "token"
    ];
}

public sealed class PathLevelRule
{
    public string? Path { get; set; }
    public string? Method { get; set; }
    public string? Level { get; set; }
    public bool PrefixMatch { get; set; } = true;
}

