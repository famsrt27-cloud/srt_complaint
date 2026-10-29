using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Complaint.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdate2ComplaintFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResponsibleDepartment",
                schema: "dbo",
                table: "Complaints",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionPurpose",
                schema: "dbo",
                table: "Complaints",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponsibleDepartment",
                schema: "dbo",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "SubmissionPurpose",
                schema: "dbo",
                table: "Complaints");
        }
    }
}
