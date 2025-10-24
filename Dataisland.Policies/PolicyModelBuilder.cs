namespace Dataisland.Policies;

public class PolicyModelBuilder
{
    internal string? TableName { get; private set; }
    internal Func<IServiceProvider, PolicyOptions, IPoliciesModel>? Model { get; private set; }

    public PolicyModelBuilder UseModel(Func<IServiceProvider, PolicyOptions, IPoliciesModel> model)
    {
        Model = model;
        return this;
    }

    public PolicyModelBuilder UseTable(string tableName)
    {
        TableName = tableName;
        return this;
    }

    internal PolicyOptions Build()
    {
        return new PolicyOptions
        {
            TableName = TableName,
        };
    }
}