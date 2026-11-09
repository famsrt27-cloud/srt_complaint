using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Complaint.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplaintDepartments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintDepartments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintDepartments_Name",
                schema: "dbo",
                table: "ComplaintDepartments",
                column: "Name",
                unique: true);

            migrationBuilder.Sql(
                """
                UPDATE [dbo].[ComplaintCategories]
                SET [DepartmentName] = LTRIM(RTRIM([DepartmentName]))
                WHERE [DepartmentName] IS NOT NULL;

                UPDATE [dbo].[Complaints]
                SET [ResponsibleDepartment] = LTRIM(RTRIM([ResponsibleDepartment]))
                WHERE [ResponsibleDepartment] IS NOT NULL;

                WITH ExistingDepartments AS
                (
                    SELECT LTRIM(RTRIM([DepartmentName])) AS [Name]
                    FROM [dbo].[ComplaintCategories]
                    WHERE [DepartmentName] IS NOT NULL AND LTRIM(RTRIM([DepartmentName])) <> ''
                    UNION
                    SELECT LTRIM(RTRIM([ResponsibleDepartment])) AS [Name]
                    FROM [dbo].[Complaints]
                    WHERE [ResponsibleDepartment] IS NOT NULL AND LTRIM(RTRIM([ResponsibleDepartment])) <> ''
                )
                INSERT INTO [dbo].[ComplaintDepartments]
                    ([Name], [Description], [IsActive], [SortOrder], [CreatedAt], [UpdatedAt])
                SELECT
                    [Name],
                    NULL,
                    CAST(1 AS bit),
                    CONVERT(int, ROW_NUMBER() OVER (ORDER BY [Name])),
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM ExistingDepartments;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplaintDepartments",
                schema: "dbo");
        }
    }
}
