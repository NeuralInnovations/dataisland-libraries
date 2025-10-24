namespace Dataisland.Sentry;

[Serializable]
public class SentryOptions
{
    public string Dsn { get; set; } = null!;
    public string Environment { get; set; } = "develop";
}