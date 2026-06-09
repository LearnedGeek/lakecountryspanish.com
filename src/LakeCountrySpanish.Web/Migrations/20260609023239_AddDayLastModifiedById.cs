using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDayLastModifiedById : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastModifiedById",
                table: "Days",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastModifiedById",
                table: "Days");
        }
    }
}
