namespace Dataisland.Serilog.RequestLogging;

public sealed class RequestLoggingConfig
{
    public string? DefaultLevel { get; set; } = "Information";
    public List<PathLevelRule> PathLevels { get; set; } = new();

    /// <summary>
    /// Captures JSON request bodies in the request completion event. Disabled by default because
    /// reading request bodies adds allocations and may expose sensitive data in logs.
    /// </summary>
    public bool CaptureRequestBody { get; set; }

    /// <summary>
    /// Captures response bodies in the request completion event. Disabled by default because it
    /// requires buffering the response until the request has completed.
    /// </summary>
    public bool CaptureResponseBody { get; set; }

    public int RequestBodyLimit { get; set; } = 2048;
    public int ResponseBodyLimit { get; set; } = 4096;
}

public sealed class PathLevelRule
{
    public string? Path { get; set; }
    public string? Method { get; set; }
    public string? Level { get; set; }
    public bool PrefixMatch { get; set; } = true;
}
