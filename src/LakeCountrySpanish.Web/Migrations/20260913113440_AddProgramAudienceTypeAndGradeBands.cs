using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramAudienceTypeAndGradeBands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AudienceNeedsReview",
                table: "Programs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AudienceType",
                table: "Programs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProgramGradeBands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgramId = table.Column<int>(type: "integer", nullable: false),
                    GradeBand = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramGradeBands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramGradeBands_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramGradeBands_ProgramId_GradeBand",
                table: "ProgramGradeBands",
                columns: new[] { "ProgramId", "GradeBand" },
                unique: true);

            // ------------------------------------------------------------------
            // Data conversion — parse the legacy GradeRange free-text field into
            // AudienceType + ProgramGradeBands rows for every existing program.
            //
            // GradeBand enum values (matches CurriculumEnums.cs):
            //   K4=0, K5=1, Grade1=2, Grade2=3, Grade3=4, Grade4=5,
            //   Grade5=6, Grade6=7, Grade7=8, Grade8=9
            //
            // AudienceType enum values:
            //   Grades=0, Adult=1, All=2
            //
            // Parser strategy (best-effort, per issue #19 spec):
            //   • GradeRange = "0"  AND  AgeMin >= 18   → Adult
            //   • GradeRange matches a "K-N", "N-M", "5K-2nd" pattern → Grades + bands
            //   • Empty / null / unparseable → All, AudienceNeedsReview = true
            //     (Karen sees a warning banner and picks the right audience)
            //
            // Rather than write PostgreSQL parsing SQL, we insert individual UPDATE
            // + INSERT statements for the seven programs currently on prod +
            // structurally similar patterns Karen has used before. Unknown patterns
            // fall through to the AudienceNeedsReview banner.
            // ------------------------------------------------------------------

            migrationBuilder.Sql(@"
-- Adult placeholder: GradeRange = '0' + adult age → Adult
UPDATE ""Programs""
SET ""AudienceType"" = 1
WHERE TRIM(""GradeRange"") = '0' AND ""AgeMin"" >= 18;

-- Grade patterns Karen has typed. Each pattern populates GradeBands
-- for the affected programs. New rows in ProgramGradeBands are keyed
-- on (ProgramId, GradeBand) — the unique index catches accidental dupes.

-- Pattern: '3-6' or '3–6' → Grade3, Grade4, Grade5, Grade6
INSERT INTO ""ProgramGradeBands"" (""ProgramId"", ""GradeBand"")
SELECT ""Id"", band FROM ""Programs"" p
CROSS JOIN (VALUES (4), (5), (6), (7)) AS b(band)
WHERE TRIM(REPLACE(p.""GradeRange"", '–', '-')) = '3-6'
ON CONFLICT DO NOTHING;
UPDATE ""Programs"" SET ""AudienceType"" = 0
WHERE TRIM(REPLACE(""GradeRange"", '–', '-')) = '3-6';

-- Pattern: '3-8'
INSERT INTO ""ProgramGradeBands"" (""ProgramId"", ""GradeBand"")
SELECT ""Id"", band FROM ""Programs"" p
CROSS JOIN (VALUES (4), (5), (6), (7), (8), (9)) AS b(band)
WHERE TRIM(REPLACE(p.""GradeRange"", '–', '-')) = '3-8'
ON CONFLICT DO NOTHING;
UPDATE ""Programs"" SET ""AudienceType"" = 0
WHERE TRIM(REPLACE(""GradeRange"", '–', '-')) = '3-8';

-- Pattern: 'K-2' → K4, K5, Grade1, Grade2 (Karen's ""K"" covers both K4 + K5)
INSERT INTO ""ProgramGradeBands"" (""ProgramId"", ""GradeBand"")
SELECT ""Id"", band FROM ""Programs"" p
CROSS JOIN (VALUES (0), (1), (2), (3)) AS b(band)
WHERE TRIM(REPLACE(p.""GradeRange"", '–', '-')) = 'K-2'
ON CONFLICT DO NOTHING;
UPDATE ""Programs"" SET ""AudienceType"" = 0
WHERE TRIM(REPLACE(""GradeRange"", '–', '-')) = 'K-2';

-- Pattern: '5K-2nd' → K5, Grade1, Grade2
INSERT INTO ""ProgramGradeBands"" (""ProgramId"", ""GradeBand"")
SELECT ""Id"", band FROM ""Programs"" p
CROSS JOIN (VALUES (1), (2), (3)) AS b(band)
WHERE TRIM(p.""GradeRange"") IN ('5K-2nd', '5k-2nd', '5K-2', '5k-2')
ON CONFLICT DO NOTHING;
UPDATE ""Programs"" SET ""AudienceType"" = 0
WHERE TRIM(""GradeRange"") IN ('5K-2nd', '5k-2nd', '5K-2', '5k-2');

-- Pattern: '4K-2nd' → K4, K5, Grade1, Grade2
INSERT INTO ""ProgramGradeBands"" (""ProgramId"", ""GradeBand"")
SELECT ""Id"", band FROM ""Programs"" p
CROSS JOIN (VALUES (0), (1), (2), (3)) AS b(band)
WHERE TRIM(p.""GradeRange"") IN ('4K-2nd', '4k-2nd', '4K-2', '4k-2')
ON CONFLICT DO NOTHING;
UPDATE ""Programs"" SET ""AudienceType"" = 0
WHERE TRIM(""GradeRange"") IN ('4K-2nd', '4k-2nd', '4K-2', '4k-2');

-- Pattern: '3K-4K' → K4 only (enum has no K3; flag for Karen review)
INSERT INTO ""ProgramGradeBands"" (""ProgramId"", ""GradeBand"")
SELECT ""Id"", 0 FROM ""Programs"" p
WHERE TRIM(p.""GradeRange"") IN ('3K-4K', '3k-4k')
ON CONFLICT DO NOTHING;
UPDATE ""Programs""
SET ""AudienceType"" = 0, ""AudienceNeedsReview"" = TRUE
WHERE TRIM(""GradeRange"") IN ('3K-4K', '3k-4k');

-- Pattern: '4K-5K' or 'K' alone → K4, K5
INSERT INTO ""ProgramGradeBands"" (""ProgramId"", ""GradeBand"")
SELECT ""Id"", band FROM ""Programs"" p
CROSS JOIN (VALUES (0), (1)) AS b(band)
WHERE TRIM(p.""GradeRange"") IN ('4K-5K', '4k-5k', 'K')
ON CONFLICT DO NOTHING;
UPDATE ""Programs"" SET ""AudienceType"" = 0
WHERE TRIM(""GradeRange"") IN ('4K-5K', '4k-5k', 'K');

-- Anything still AudienceType=0 (default from AddColumn) that has NO grade
-- bands populated AND wasn't caught above → mark for review. Includes:
--   • Empty / null GradeRange with no adult age
--   • Unrecognized patterns
--   • The 'All'-style intent that got no explicit mapping
UPDATE ""Programs""
SET ""AudienceNeedsReview"" = TRUE
WHERE ""AudienceType"" = 0
  AND NOT EXISTS (
    SELECT 1 FROM ""ProgramGradeBands"" pgb WHERE pgb.""ProgramId"" = ""Programs"".""Id""
  );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramGradeBands");

            migrationBuilder.DropColumn(
                name: "AudienceNeedsReview",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "AudienceType",
                table: "Programs");
        }
    }
}
