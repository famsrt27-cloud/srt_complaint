using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Complaint.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaWarningTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SlaWarningSentAt",
                schema: "dbo",
                table: "Complaints",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlaWarningSentAt",
                schema: "dbo",
                table: "Complaints");
        }
    }
}
