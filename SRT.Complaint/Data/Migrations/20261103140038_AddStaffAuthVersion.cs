using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Complaint.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAuthVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthVersion",
                schema: "dbo",
                table: "StaffUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthVersion",
                schema: "dbo",
                table: "StaffUsers");
        }
    }
}
