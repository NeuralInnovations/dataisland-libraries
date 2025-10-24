namespace Dataisland.MongoDB.Migrations;

public readonly record struct MigrationVersion(int Version)
{
    public static readonly MigrationVersion Latest = new(int.MaxValue);
}