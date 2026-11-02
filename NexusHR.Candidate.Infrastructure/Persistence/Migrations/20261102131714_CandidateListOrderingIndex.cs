using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusHR.Candidate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CandidateListOrderingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_candidates_CreatedAtUtc_Id",
                table: "candidates",
                columns: new[] { "CreatedAtUtc", "Id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_candidates_CreatedAtUtc_Id",
                table: "candidates");
        }
    }
}
