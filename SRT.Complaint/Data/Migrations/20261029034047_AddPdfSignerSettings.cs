using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Complaint.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfSignerSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PdfSignerSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PositionTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActingTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SignatureStoredPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfSignerSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PdfSignerSettings_StaffUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "StaffUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "PdfSignerSettings",
                columns: new[] { "Id", "ActingTitle", "FullName", "PositionTitle", "SignatureStoredPath", "UpdatedAt", "UpdatedById" },
                values: new object[] { 1, "ปฏิบัติการแทน ผู้อำนวยการศูนย์ประชาสัมพันธ์", "นางสาวชัชชนก แสงมุข", "หัวหน้ากองประชาสัมพันธ์", null, new DateTime(2026, 9, 21, 0, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.CreateIndex(
                name: "IX_PdfSignerSettings_UpdatedById",
                schema: "dbo",
                table: "PdfSignerSettings",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdfSignerSettings",
                schema: "dbo");
        }
    }
}
