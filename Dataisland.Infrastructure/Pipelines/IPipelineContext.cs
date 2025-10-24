using Pulumi;

namespace Dataisland.Infrastructure.Pipelines;

public interface IPipelineContext
{
    Dependency Dependency { get; }

    Config Config { get; }

    string? GetEnv(string name);

    string GetEnvRequire(string name);

    TResult GetStepResult<TStep, TResult>()
        where TStep : PipelineStep<TResult>;

    void Output(string key, object value);
}