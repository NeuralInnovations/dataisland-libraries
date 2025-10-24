namespace Dataisland.MongoDB.Migrations;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MigrationAttribute : Attribute
{
    public int Version { get; }
    public string? Description { get; }

    public MigrationAttribute(int version, string? description = null)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Migration version must be a positive integer.");
        }
        Version = version;
        Description = description;
    }

    public override string ToString() => $"Migration v{Version}: {Description}";
}

