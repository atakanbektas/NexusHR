using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusHR.Candidate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCvAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "candidate_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_daily_budgets",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    CommittedUsd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_daily_budgets", x => x.Day);
                });

            migrationBuilder.CreateTable(
                name: "ai_daily_quotas",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reservations = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_daily_quotas", x => new { x.Day, x.Subject });
                });

            migrationBuilder.CreateTable(
                name: "cv_analyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SchemaVersion = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    ExtractorVersion = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Delivered = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalProcessingAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CancellationRequested = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    ReservedUsd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ReservationDay = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_analyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cv_analyses_candidate_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "candidate_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cv_analysis_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    ProviderRequestId = table.Column<string>(type: "text", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    ReservedUsd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    UsageCostUsd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    PriceVersion = table.Column<string>(type: "text", nullable: false),
                    InputRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    OutputRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_analysis_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cv_analysis_attempts_cv_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "cv_analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cv_analysis_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reviewer = table.Column<string>(type: "text", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_analysis_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cv_analysis_reviews_cv_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "cv_analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cv_analyses_CandidateId_RequestedBy_IdempotencyKey",
                table: "cv_analyses",
                columns: new[] { "CandidateId", "RequestedBy", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cv_analyses_DocumentId",
                table: "cv_analyses",
                column: "DocumentId",
                unique: true,
                filter: "\"Status\" IN ('Queued', 'Running')");

            migrationBuilder.CreateIndex(
                name: "IX_cv_analyses_Status_NextAttemptAtUtc",
                table: "cv_analyses",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cv_analysis_attempts_AnalysisId_Number",
                table: "cv_analysis_attempts",
                columns: new[] { "AnalysisId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cv_analysis_reviews_AnalysisId_Revision",
                table: "cv_analysis_reviews",
                columns: new[] { "AnalysisId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_daily_budgets");

            migrationBuilder.DropTable(
                name: "ai_daily_quotas");

            migrationBuilder.DropTable(
                name: "cv_analysis_attempts");

            migrationBuilder.DropTable(
                name: "cv_analysis_reviews");

            migrationBuilder.DropTable(
                name: "cv_analyses");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "candidate_documents");
        }
    }
}
