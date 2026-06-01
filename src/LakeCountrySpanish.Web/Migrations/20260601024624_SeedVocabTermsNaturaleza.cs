using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations;

/// <summary>
/// Seeds the initial set of 20 outdoor / nature Spanish vocabulary terms
/// driving the first scavenger-hunt worksheet. Up/Down logic lives in
/// <c>Scripts/SeedVocabTermsNaturaleza.sql</c> and its rollback counterpart;
/// this migration delegates to the <see cref="BaseMigration"/> helper.
/// </summary>
public partial class SeedVocabTermsNaturaleza : BaseMigration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => RunSql(migrationBuilder);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => RunSqlRollback(migrationBuilder);
}
