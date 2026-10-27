using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusHR.Hiring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HiringConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "hiring_processes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "hiring_processes");
        }
    }
}
