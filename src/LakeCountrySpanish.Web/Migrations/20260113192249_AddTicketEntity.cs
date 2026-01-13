using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TicketId",
                table: "ScheduledClasses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    UsedForClassId = table.Column<int>(type: "int", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StripeSessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubscriptionId = table.Column<int>(type: "int", nullable: true),
                    TokensSpent = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_ScheduledClasses_UsedForClassId",
                        column: x => x.UsedForClassId,
                        principalTable: "ScheduledClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tickets_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledClasses_TicketId",
                table: "ScheduledClasses",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_StudentId_IsUsed_ExpiresAt",
                table: "Tickets",
                columns: new[] { "StudentId", "IsUsed", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_StudentId_Source",
                table: "Tickets",
                columns: new[] { "StudentId", "Source" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SubscriptionId",
                table: "Tickets",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_UsedForClassId",
                table: "Tickets",
                column: "UsedForClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledClasses_Tickets_TicketId",
                table: "ScheduledClasses",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledClasses_Tickets_TicketId",
                table: "ScheduledClasses");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledClasses_TicketId",
                table: "ScheduledClasses");

            migrationBuilder.DropColumn(
                name: "TicketId",
                table: "ScheduledClasses");
        }
    }
}
