using Pulumi;
using Pulumiverse.Time;

namespace Dataisland.Infrastructure.Pipelines;

public class Dependency(Dependency? parent = null)
{
    private readonly List<CustomResource> _dependencies = new();

    public static implicit operator InputList<Resource>(Dependency dependency)
    {
        return dependency.Dependencies;
    }

    public Dependency? Parent => parent;
    public List<Dependency> Children { get; } = new List<Dependency>();

    public Dependency NewNested() => new Dependency(this);
    public CustomResource[] Dependencies => _dependencies.Concat(parent?._dependencies ?? new()).ToArray();

    public Dependency Flush()
    {
        if (parent != null)
        {
            parent._dependencies.AddRange(_dependencies);
            _dependencies.Clear();
        }

        return this;
    }

    public CustomResource Add(CustomResource resource)
    {
        if (!_dependencies.Contains(resource))
        {
            _dependencies.Add(resource);
        }

        return resource;
    }

    public T Add<T>(T resource)
        where T : CustomResource
    {
        if (!_dependencies.Contains(resource))
        {
            _dependencies.Add(resource);
        }

        return resource;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <param name="duration">[Time duration](https://golang.org/pkg/time/#ParseDuration) to delay resource creation. For example, `30s` for 30 seconds or `5m` for 5 minutes. Updating this value by itself will not trigger a delay.</param>
    /// <returns></returns>
    public CustomResource AddWait(string key, string duration)
    {
        return Add(new Sleep(key, new SleepArgs
        {
            CreateDuration = duration
        }));
    }
}