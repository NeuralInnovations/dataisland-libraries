using Pulumi;

namespace Dataisland.Infrastructure.Pipelines;

public class Pipeline : Stack
{
    protected Pipeline(params PipelineStep[] steps)
    {
        var config = new Config();
        var context = new PipelineContext(steps, config);
        foreach (var step in steps)
        {
            PipelineStep.Internal.Run(step, context);
        }

        RegisterOutputs(context.Outputs);
    }
}