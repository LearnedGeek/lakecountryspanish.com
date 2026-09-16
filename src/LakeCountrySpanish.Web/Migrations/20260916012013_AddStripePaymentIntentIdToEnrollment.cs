using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddStripePaymentIntentIdToEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripePaymentIntentId",
                table: "ProgramEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_StripePaymentIntentId",
                table: "ProgramEnrollments",
                column: "StripePaymentIntentId",
                filter: "\"StripePaymentIntentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgramEnrollments_StripePaymentIntentId",
                table: "ProgramEnrollments");

            migrationBuilder.DropColumn(
                name: "StripePaymentIntentId",
                table: "ProgramEnrollments");
        }
    }
}
