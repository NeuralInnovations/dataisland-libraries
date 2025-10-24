using Dataisland.Organizations.Models;

namespace Dataisland.Organizations;

public class OrganizationsModelBuilder
{
    internal Func<IServiceProvider, OrganizationsOptions, IModels>? ModelFactory { get; private set; }
    internal Action<OrganizationsOptions>? OptionsAction { get; private set; }

    public OrganizationsModelBuilder UseModel(Func<IServiceProvider, OrganizationsOptions, IModels> model)
    {
        ModelFactory = model;
        return this;
    }

    public OrganizationsModelBuilder Options(Action<OrganizationsOptions> options)
    {
        OptionsAction = options;
        return this;
    }

    internal OrganizationsOptions Build()
    {
        var options = new OrganizationsOptions();
        if (OptionsAction != null)
            OptionsAction(options);
        return options;
    }
}