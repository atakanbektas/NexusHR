using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusHR.Candidate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiAttemptObservability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "cv_analysis_attempts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvidenceAccepted",
                table: "cv_analysis_attempts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvidenceProposed",
                table: "cv_analysis_attempts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvidenceRejected",
                table: "cv_analysis_attempts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureStage",
                table: "cv_analysis_attempts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NativeOutputTokensPerSecond",
                table: "cv_analysis_attempts",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cv_analysis_attempts_StartedAtUtc",
                table: "cv_analysis_attempts",
                column: "StartedAtUtc",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cv_analysis_attempts_StartedAtUtc",
                table: "cv_analysis_attempts");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "cv_analysis_attempts");

            migrationBuilder.DropColumn(
                name: "EvidenceAccepted",
                table: "cv_analysis_attempts");

            migrationBuilder.DropColumn(
                name: "EvidenceProposed",
                table: "cv_analysis_attempts");

            migrationBuilder.DropColumn(
                name: "EvidenceRejected",
                table: "cv_analysis_attempts");

            migrationBuilder.DropColumn(
                name: "FailureStage",
                table: "cv_analysis_attempts");

            migrationBuilder.DropColumn(
                name: "NativeOutputTokensPerSecond",
                table: "cv_analysis_attempts");
        }
    }
}
