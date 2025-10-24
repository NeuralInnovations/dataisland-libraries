namespace Dataisland.Infrastructure.Pipelines;

public abstract class PipelineStep<TResult> : PipelineStep
{
    protected abstract TResult RunWith(IPipelineContext context);

    protected override void Run(IPipelineContext context)
    {
        Internal.PrepareToExecute(this, context);
        var result = RunWith(context);
        Internal.SetResult(this, result);
    }
}

public abstract class PipelineStep
{
    private enum State
    {
        None,
        Prepared,
        Executed,
    }

    internal static class Internal
    {
        internal static void PrepareToExecute(PipelineStep step, IPipelineContext context)
        {
            if (step._state != State.None)
                throw new InvalidOperationException(
                    $"Cannot prepare to execute {step.GetType().Name} step");

            step._state = State.Prepared;
        }

        internal static PipelineStep Run(PipelineStep step, IPipelineContext context)
        {
            step.Run(context);
            return step;
        }

        internal static void SetResult<TResult>(PipelineStep step, TResult result)
        {
            if (step._state != State.Prepared)
                throw new InvalidOperationException(
                    $"Cannot finalize {step.GetType().Name} step");

            step._state = State.Executed;
            step._result = result;
        }

        public static TResult? GetResult<TStep, TResult>(TStep value) where TStep : PipelineStep<TResult>
        {
            return (TResult?)value._result;
        }
    }

    private State _state;
    private object? _result;

    protected abstract void Run(IPipelineContext context);
}