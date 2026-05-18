namespace Dataisland.Configuration;

// Shared secret used for cross-instance medical-server traffic: library content
// proxy + remote search endpoints. Both sides (calling instance + serving instance)
// must be configured with the same SecretToken.
public class RemoteLibraryOptions
{
    public string? SecretToken { get; set; }
    public string HeaderName { get; set; } = "X-Remote-Library-Token";
}
