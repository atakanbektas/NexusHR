using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusHR.Hiring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferResponseToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfferResponseTokenHash",
                table: "hiring_processes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferResponseTokenUsedAtUtc",
                table: "hiring_processes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfferResponseTokenHash",
                table: "hiring_processes");

            migrationBuilder.DropColumn(
                name: "OfferResponseTokenUsedAtUtc",
                table: "hiring_processes");
        }
    }
}
