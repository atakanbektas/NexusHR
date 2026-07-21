using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusHR.Candidate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCandidateDocumentMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ObjectName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_documents_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_documents_CandidateId_Type",
                table: "candidate_documents",
                columns: new[] { "CandidateId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_documents_ObjectName",
                table: "candidate_documents",
                column: "ObjectName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_documents");
        }
    }
}
