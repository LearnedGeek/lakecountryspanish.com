using LakeCountrySpanish.Web.Data;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LakeCountrySpanish.Web.Migrations;

/// <summary>
/// Base class for migrations whose Up/Down logic lives in raw SQL files
/// alongside the .cs migration. Use this when:
///
/// - Seeding reference data with rollbacks (WI standards, curriculum themes,
///   default content)
/// - Bulk data corrections that EF's fluent API would make awkward
/// - Schema changes that need accompanying data migrations
///
/// Don't use for ASP.NET Identity user inserts — password hashes need the
/// runtime hasher (see SeedData.InitializeAsync for that path).
///
/// Usage:
///
///   public partial class SeedSomething : BaseMigration
///   {
///       protected override void Up(MigrationBuilder mb) => RunSql(mb);
///       protected override void Down(MigrationBuilder mb) => RunSqlRollback(mb);
///   }
///
/// And create:
///   Migrations/Scripts/SeedSomething.sql
///   Migrations/Scripts/SeedSomething.rollback.sql
///
/// Pattern adopted from the Allevo Therapeutics repo.
/// </summary>
public abstract class BaseMigration : Migration
{
    protected void RunSql(MigrationBuilder migrationBuilder)
    {
        var sql = MigrationHelper.GetMigrationScript(GetType().Name, "Up");
        migrationBuilder.Sql(sql);
    }

    protected void RunSqlRollback(MigrationBuilder migrationBuilder)
    {
        var sql = MigrationHelper.GetMigrationScript(GetType().Name, "Down");
        migrationBuilder.Sql(sql);
    }
}
