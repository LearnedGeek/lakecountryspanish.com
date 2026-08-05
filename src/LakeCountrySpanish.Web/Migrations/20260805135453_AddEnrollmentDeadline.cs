using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EnrollmentDeadline",
                table: "Programs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnrollmentDeadline",
                table: "Programs");
        }
    }
}
