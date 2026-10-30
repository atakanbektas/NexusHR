using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusHR.Candidate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CvVersionsAndFactReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_candidate_documents_CandidateId_Type",
                table: "candidate_documents");

            migrationBuilder.AddColumn<string>(
                name: "CorrectedValue",
                table: "cv_analysis_reviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FactIndex",
                table: "cv_analysis_reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "cv_analyses",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "candidate_documents",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousDocumentId",
                table: "candidate_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "candidate_documents",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_documents_CandidateId_Type",
                table: "candidate_documents",
                columns: new[] { "CandidateId", "Type" },
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_documents_CandidateId_Type_Revision",
                table: "candidate_documents",
                columns: new[] { "CandidateId", "Type", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM candidate_documents WHERE "Revision" > 1)
                       OR EXISTS (SELECT 1 FROM cv_analysis_reviews WHERE "FactIndex" IS NOT NULL) THEN
                        RAISE EXCEPTION 'CV history/reviews exist. Use a forward migration; rollback would lose data.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_candidate_documents_CandidateId_Type",
                table: "candidate_documents");

            migrationBuilder.DropIndex(
                name: "IX_candidate_documents_CandidateId_Type_Revision",
                table: "candidate_documents");

            migrationBuilder.DropColumn(
                name: "CorrectedValue",
                table: "cv_analysis_reviews");

            migrationBuilder.DropColumn(
                name: "FactIndex",
                table: "cv_analysis_reviews");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "cv_analyses");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                table: "candidate_documents");

            migrationBuilder.DropColumn(
                name: "PreviousDocumentId",
                table: "candidate_documents");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "candidate_documents");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_documents_CandidateId_Type",
                table: "candidate_documents",
                columns: new[] { "CandidateId", "Type" },
                unique: true);
        }
    }
}
