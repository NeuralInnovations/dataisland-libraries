using System.Collections;
using Pulumi;

namespace Dataisland.Infrastructure.Pipelines;

public class PipelineContext(
    PipelineStep[] steps,
    Config config
) : IPipelineContext
{
    public Dictionary<string, object?> Outputs { get; } = new();
    public Dependency Dependency { get; } = new Dependency();
    public Config Config => config;
    public string? GetEnv(string name) => Environment.GetEnvironmentVariable(name);

    public string GetEnvRequire(string name)
    {
        var result = GetEnv(name);
        if (result == null)
        {
            throw new Exception($"Environment variable '{name}' does not exist.");
        }

        return result;
    }

    public TResult GetStepResult<TStep, TResult>()
        where TStep : PipelineStep<TResult>
    {
        return PipelineStep.Internal.GetResult<TStep, TResult>(
            (TStep)steps.First(x => x is TStep)!
            ?? throw new Exception()
        )!;
    }

    public void Output(string key, object? value)
    {
        Outputs[key] = value;
    }
}