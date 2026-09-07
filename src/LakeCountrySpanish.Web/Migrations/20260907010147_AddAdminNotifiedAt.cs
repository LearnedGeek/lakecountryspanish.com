using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminNotifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdminNotifiedAt",
                table: "ProgramEnrollments",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill so existing enrollments don't retroactively trigger a
            // digest when the background service first ticks after deploy. Old
            // rows are treated as "already notified" — the inline per-enrollment
            // email fired at their creation time under the pre-digest code path.
            migrationBuilder.Sql(@"
                UPDATE ""ProgramEnrollments""
                SET ""AdminNotifiedAt"" = ""CreatedAt""
                WHERE ""AdminNotifiedAt"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNotifiedAt",
                table: "ProgramEnrollments");
        }
    }
}
