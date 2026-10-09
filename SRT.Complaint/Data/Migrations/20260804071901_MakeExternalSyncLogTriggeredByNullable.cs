using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Complaint.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeExternalSyncLogTriggeredByNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalSyncLogs_StaffUsers_TriggeredById",
                schema: "dbo",
                table: "ExternalSyncLogs");

            migrationBuilder.AlterColumn<int>(
                name: "TriggeredById",
                schema: "dbo",
                table: "ExternalSyncLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalSyncLogs_StaffUsers_TriggeredById",
                schema: "dbo",
                table: "ExternalSyncLogs",
                column: "TriggeredById",
                principalSchema: "dbo",
                principalTable: "StaffUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalSyncLogs_StaffUsers_TriggeredById",
                schema: "dbo",
                table: "ExternalSyncLogs");

            migrationBuilder.AlterColumn<int>(
                name: "TriggeredById",
                schema: "dbo",
                table: "ExternalSyncLogs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalSyncLogs_StaffUsers_TriggeredById",
                schema: "dbo",
                table: "ExternalSyncLogs",
                column: "TriggeredById",
                principalSchema: "dbo",
                principalTable: "StaffUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
