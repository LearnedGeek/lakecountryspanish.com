using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddClassroomUrlAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentPackages_Payments_PaymentId1",
                table: "StudentPackages");

            migrationBuilder.DropIndex(
                name: "IX_StudentPackages_PaymentId",
                table: "StudentPackages");

            migrationBuilder.DropIndex(
                name: "IX_StudentPackages_PaymentId1",
                table: "StudentPackages");

            migrationBuilder.DropColumn(
                name: "PaymentId1",
                table: "StudentPackages");

            migrationBuilder.AddColumn<string>(
                name: "ClassroomUrlOverride",
                table: "ScheduledClasses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreditForfeited",
                table: "ScheduledClasses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TeacherNotes",
                table: "ScheduledClasses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassroomUrl",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BlockedDates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedDates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentPackages_PaymentId",
                table: "StudentPackages",
                column: "PaymentId",
                unique: true,
                filter: "[PaymentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedDates");

            migrationBuilder.DropIndex(
                name: "IX_StudentPackages_PaymentId",
                table: "StudentPackages");

            migrationBuilder.DropColumn(
                name: "ClassroomUrlOverride",
                table: "ScheduledClasses");

            migrationBuilder.DropColumn(
                name: "CreditForfeited",
                table: "ScheduledClasses");

            migrationBuilder.DropColumn(
                name: "TeacherNotes",
                table: "ScheduledClasses");

            migrationBuilder.DropColumn(
                name: "ClassroomUrl",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "PaymentId1",
                table: "StudentPackages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPackages_PaymentId",
                table: "StudentPackages",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPackages_PaymentId1",
                table: "StudentPackages",
                column: "PaymentId1",
                unique: true,
                filter: "[PaymentId1] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentPackages_Payments_PaymentId1",
                table: "StudentPackages",
                column: "PaymentId1",
                principalTable: "Payments",
                principalColumn: "Id");
        }
    }
}
