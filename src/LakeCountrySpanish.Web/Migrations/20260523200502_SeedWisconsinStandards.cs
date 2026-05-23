using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations;

/// <summary>
/// Seeds the 36 Wisconsin DPI World Languages standards (Novice-band performance
/// indicators) from the June 2019 publication. Up/Down logic lives in the
/// matching <c>Scripts/SeedWisconsinStandards.sql</c> and
/// <c>Scripts/SeedWisconsinStandards.rollback.sql</c> files; this migration
/// delegates to the <see cref="BaseMigration"/> helper.
/// </summary>
public partial class SeedWisconsinStandards : BaseMigration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => RunSql(migrationBuilder);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => RunSqlRollback(migrationBuilder);
}
