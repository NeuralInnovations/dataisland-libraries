using Microsoft.Extensions.Logging;

namespace Dataisland.MongoDB;

public interface IRepositoryApplyIndex
{
    Task ApplyAsync(ILogger logger);
}