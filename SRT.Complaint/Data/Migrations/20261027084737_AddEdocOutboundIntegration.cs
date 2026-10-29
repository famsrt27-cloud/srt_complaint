using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Complaint.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEdocOutboundIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EdocDocumentLinks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComplaintId = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    WID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BasketId = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    BasketName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegisterNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RemoteDocumentDate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SyncStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RemoteStatusCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RemoteStatusDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTrackedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextTrackingAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdocDocumentLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EdocDocumentLinks_Complaints_ComplaintId",
                        column: x => x.ComplaintId,
                        principalSchema: "dbo",
                        principalTable: "Complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EdocDocumentLinks_StaffUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EdocAttachmentDeliveries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EdocDocumentLinkId = table.Column<int>(type: "int", nullable: false),
                    ComplaintAttachmentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdocAttachmentDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EdocAttachmentDeliveries_ComplaintAttachments_ComplaintAttachmentId",
                        column: x => x.ComplaintAttachmentId,
                        principalSchema: "dbo",
                        principalTable: "ComplaintAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EdocAttachmentDeliveries_EdocDocumentLinks_EdocDocumentLinkId",
                        column: x => x.EdocDocumentLinkId,
                        principalSchema: "dbo",
                        principalTable: "EdocDocumentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EdocAttachmentDeliveries_ComplaintAttachmentId",
                schema: "dbo",
                table: "EdocAttachmentDeliveries",
                column: "ComplaintAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EdocAttachmentDeliveries_EdocDocumentLinkId_ComplaintAttachmentId",
                schema: "dbo",
                table: "EdocAttachmentDeliveries",
                columns: new[] { "EdocDocumentLinkId", "ComplaintAttachmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EdocDocumentLinks_ComplaintId",
                schema: "dbo",
                table: "EdocDocumentLinks",
                column: "ComplaintId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EdocDocumentLinks_CreatedById",
                schema: "dbo",
                table: "EdocDocumentLinks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EdocDocumentLinks_SyncStatus_NextTrackingAt",
                schema: "dbo",
                table: "EdocDocumentLinks",
                columns: new[] { "SyncStatus", "NextTrackingAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EdocDocumentLinks_WID",
                schema: "dbo",
                table: "EdocDocumentLinks",
                column: "WID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EdocAttachmentDeliveries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EdocDocumentLinks",
                schema: "dbo");
        }
    }
}
