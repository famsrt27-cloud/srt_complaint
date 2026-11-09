using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Complaint.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowAndAcceptanceEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolutionSummary",
                schema: "dbo",
                table: "Complaints",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowStep",
                schema: "dbo",
                table: "Complaints",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Received");

            migrationBuilder.AddColumn<DateTime>(
                name: "WorkflowUpdatedAt",
                schema: "dbo",
                table: "Complaints",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.Sql(@"
                UPDATE [dbo].[Complaints]
                SET [WorkflowStep] = CASE
                        WHEN [Status] = N'Closed' THEN N'Completed'
                        WHEN [Status] = N'Rejected' THEN N'Rejected'
                        WHEN [Status] = N'Resolved' THEN N'Summary'
                        WHEN [Status] = N'InProgress' THEN N'Investigation'
                        WHEN [AssignedToId] IS NOT NULL THEN N'Screening'
                        ELSE N'Received'
                    END,
                    [WorkflowUpdatedAt] = COALESCE([UpdatedAt], [CreatedAt], GETUTCDATE());");

            migrationBuilder.CreateTable(
                name: "ComplaintAcceptanceEvidence",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComplaintId = table.Column<int>(type: "int", nullable: false),
                    AcceptanceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TermsTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TermsVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TermsContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TermsContentSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintAcceptanceEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplaintAcceptanceEvidence_Complaints_ComplaintId",
                        column: x => x.ComplaintId,
                        principalSchema: "dbo",
                        principalTable: "Complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintAcceptanceEvidence_ComplaintId_AcceptanceType",
                schema: "dbo",
                table: "ComplaintAcceptanceEvidence",
                columns: new[] { "ComplaintId", "AcceptanceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintAcceptanceEvidence_EvidenceId",
                schema: "dbo",
                table: "ComplaintAcceptanceEvidence",
                column: "EvidenceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplaintAcceptanceEvidence",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "ResolutionSummary",
                schema: "dbo",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "WorkflowStep",
                schema: "dbo",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "WorkflowUpdatedAt",
                schema: "dbo",
                table: "Complaints");
        }
    }
}
