using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LakeCountrySpanish.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Programs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TagLine = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    HeroImagePath = table.Column<string>(type: "text", nullable: true),
                    LocationName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LocationAddress = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MeetingDays = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    GradeRange = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AgeMin = table.Column<int>(type: "integer", nullable: false),
                    AgeMax = table.Column<int>(type: "integer", nullable: false),
                    FullPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InstallmentsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InstallmentCount = table.Column<int>(type: "integer", nullable: false),
                    FinalInstallmentDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CashOptionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    StripeProductId = table.Column<string>(type: "text", nullable: true),
                    StripeFullPriceId = table.Column<string>(type: "text", nullable: true),
                    StripeInstallmentPriceId = table.Column<string>(type: "text", nullable: true),
                    WaiverText = table.Column<string>(type: "text", nullable: false),
                    RefundPolicyText = table.Column<string>(type: "text", nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsListed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Programs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgramEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgramId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParentFirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ParentLastName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ParentEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParentAddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentCity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ParentState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParentZip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StudentFirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StudentLastName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StudentGrade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StudentBirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MedicalConcerns = table.Column<string>(type: "text", nullable: true),
                    StudentNotes = table.Column<string>(type: "text", nullable: true),
                    EmergencyName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EmergencyPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmergencyRelationship = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PickupAuthorization = table.Column<string>(type: "text", nullable: false),
                    WaiverAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PhotoReleaseGrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StripeCheckoutSessionId = table.Column<string>(type: "text", nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "text", nullable: true),
                    StripeCustomerId = table.Column<string>(type: "text", nullable: true),
                    FirstPaymentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SecondPaymentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalAmountPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AdminNotes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramEnrollments_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_ProgramId_CreatedAt",
                table: "ProgramEnrollments",
                columns: new[] { "ProgramId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_StripeCheckoutSessionId",
                table: "ProgramEnrollments",
                column: "StripeCheckoutSessionId",
                filter: "\"StripeCheckoutSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_StripeSubscriptionId",
                table: "ProgramEnrollments",
                column: "StripeSubscriptionId",
                filter: "\"StripeSubscriptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Programs_IsActive_IsListed",
                table: "Programs",
                columns: new[] { "IsActive", "IsListed" });

            migrationBuilder.CreateIndex(
                name: "IX_Programs_Slug",
                table: "Programs",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramEnrollments");

            migrationBuilder.DropTable(
                name: "Programs");
        }
    }
}
