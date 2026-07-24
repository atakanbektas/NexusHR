using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusHR.Hiring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eligible_candidates",
                columns: table => new
                {
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    BecameEligibleAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eligible_candidates", x => x.CandidateId);
                });

            migrationBuilder.CreateTable(
                name: "hiring_processes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Department = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EmploymentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GrossSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ProposedStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OfferExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfferSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfferRespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hiring_processes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hiring_processes_eligible_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "eligible_candidates",
                        principalColumn: "CandidateId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eligible_candidates_Email",
                table: "eligible_candidates",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "ux_hiring_processes_candidate_active",
                table: "hiring_processes",
                column: "CandidateId",
                unique: true,
                filter: "\"Status\" IN ('Draft', 'OfferPrepared', 'OfferSent', 'OfferAccepted')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hiring_processes");

            migrationBuilder.DropTable(
                name: "eligible_candidates");
        }
    }
}
