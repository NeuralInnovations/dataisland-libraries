namespace Dataisland.Infrastructure.Pipelines;

public static class PipelineContextExtensions
{
    public static string MakeDomain(this IPipelineContext context, params string[] parts)
    {
        return string.Join(".", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}