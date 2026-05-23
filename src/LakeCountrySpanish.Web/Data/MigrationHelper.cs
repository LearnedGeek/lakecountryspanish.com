using System.Reflection;

namespace LakeCountrySpanish.Web.Data;

/// <summary>
/// Loads SQL migration scripts from the Migrations/Scripts folder. Used by
/// <see cref="LakeCountrySpanish.Web.Migrations.BaseMigration"/> for data
/// migrations whose Up/Down logic reads better as raw SQL than as EF fluent
/// API calls.
///
/// Each SQL-based migration has two files alongside the .cs file:
///   Migrations/Scripts/{MigrationClassName}.sql           — Up
///   Migrations/Scripts/{MigrationClassName}.rollback.sql  — Down
///
/// Files are copied to the output directory at build time (see
/// CopyToOutputDirectory in LakeCountrySpanish.Web.csproj). Pattern adopted
/// from the Allevo Therapeutics repo.
/// </summary>
public static class MigrationHelper
{
    public static string GetMigrationScript(string migrationClassName, string scriptType)
    {
        var fileName = scriptType == "Up"
            ? $"{migrationClassName}.sql"
            : $"{migrationClassName}.rollback.sql";

        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? Directory.GetCurrentDirectory();

        var scriptPath = Path.Combine(assemblyDir, "Migrations", "Scripts", fileName);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException(
                $"SQL migration script not found: {scriptPath}. " +
                $"Did you mark the .sql file as <Content CopyToOutputDirectory=\"PreserveNewest\"> in the .csproj?");
        }

        return File.ReadAllText(scriptPath);
    }
}
