using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Complaint.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerApiV1Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveryLogs_WebhookId",
                schema: "dbo",
                table: "WebhookDeliveryLogs");

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryId",
                schema: "dbo",
                table: "WebhookDeliveryLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OutboxMessageId",
                schema: "dbo",
                table: "WebhookDeliveryLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourcePartnerId",
                schema: "dbo",
                table: "Complaints",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "KeyPrefix",
                schema: "dbo",
                table: "ApiKeys",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "DataAccessMode",
                schema: "dbo",
                table: "ApiKeys",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                schema: "dbo",
                table: "ApiKeys",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartnerId",
                schema: "dbo",
                table: "ApiKeys",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "dbo",
                table: "ApiKeys",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RotatedToKeyId",
                schema: "dbo",
                table: "ApiKeys",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RotationGraceEndsAt",
                schema: "dbo",
                table: "ApiKeys",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApiIdempotencyRecords",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiKeyId = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResponseStatus = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiIdempotencyRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiIdempotencyRecords_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalSchema: "dbo",
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Partners",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookOutboxMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PartnerId = table.Column<int>(type: "int", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookOutboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookOutboxMessages_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "dbo",
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.Sql("""
                SET IDENTITY_INSERT [dbo].[Partners] ON;
                INSERT INTO [dbo].[Partners]
                    ([Id], [Code], [Name], [ContactName], [ContactEmail], [ContactPhone], [IsActive], [CreatedAt], [UpdatedAt])
                VALUES
                    (1, N'LEGACY', N'ระบบเดิมก่อนแยก Partner', NULL, NULL, NULL, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                SET IDENTITY_INSERT [dbo].[Partners] OFF;

                UPDATE [dbo].[ApiKeys]
                SET [PartnerId] = 1,
                    [Environment] = N'Production',
                    [DataAccessMode] = CASE WHEN [KeyType] = N'Internal' THEN N'All' ELSE N'Own' END;

                UPDATE [dbo].[Complaints]
                SET [SourcePartnerId] = 1
                WHERE [Channel] = N'API';

                UPDATE [dbo].[WebhookDeliveryLogs]
                SET [DeliveryId] = NEWID()
                WHERE [DeliveryId] IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "DeliveryId",
                schema: "dbo",
                table: "WebhookDeliveryLogs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DataAccessMode",
                schema: "dbo",
                table: "ApiKeys",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Environment",
                schema: "dbo",
                table: "ApiKeys",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PartnerId",
                schema: "dbo",
                table: "ApiKeys",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLogs_DeliveryId",
                schema: "dbo",
                table: "WebhookDeliveryLogs",
                column: "DeliveryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLogs_OutboxMessageId",
                schema: "dbo",
                table: "WebhookDeliveryLogs",
                column: "OutboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLogs_WebhookId_OutboxMessageId",
                schema: "dbo",
                table: "WebhookDeliveryLogs",
                columns: new[] { "WebhookId", "OutboxMessageId" },
                unique: true,
                filter: "[OutboxMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_SourcePartnerId",
                schema: "dbo",
                table: "Complaints",
                column: "SourcePartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                schema: "dbo",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_PartnerId_Environment",
                schema: "dbo",
                table: "ApiKeys",
                columns: new[] { "PartnerId", "Environment" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiIdempotencyRecords_ApiKeyId_IdempotencyKey",
                schema: "dbo",
                table: "ApiIdempotencyRecords",
                columns: new[] { "ApiKeyId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiIdempotencyRecords_ExpiresAt",
                schema: "dbo",
                table: "ApiIdempotencyRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_Code",
                schema: "dbo",
                table: "Partners",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookOutboxMessages_EventId",
                schema: "dbo",
                table: "WebhookOutboxMessages",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookOutboxMessages_PartnerId",
                schema: "dbo",
                table: "WebhookOutboxMessages",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookOutboxMessages_ProcessedAt_NextAttemptAt",
                schema: "dbo",
                table: "WebhookOutboxMessages",
                columns: new[] { "ProcessedAt", "NextAttemptAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeys_Partners_PartnerId",
                schema: "dbo",
                table: "ApiKeys",
                column: "PartnerId",
                principalSchema: "dbo",
                principalTable: "Partners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_Partners_SourcePartnerId",
                schema: "dbo",
                table: "Complaints",
                column: "SourcePartnerId",
                principalSchema: "dbo",
                principalTable: "Partners",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WebhookDeliveryLogs_WebhookOutboxMessages_OutboxMessageId",
                schema: "dbo",
                table: "WebhookDeliveryLogs",
                column: "OutboxMessageId",
                principalSchema: "dbo",
                principalTable: "WebhookOutboxMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeys_Partners_PartnerId",
                schema: "dbo",
                table: "ApiKeys");

            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_Partners_SourcePartnerId",
                schema: "dbo",
                table: "Complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_WebhookDeliveryLogs_WebhookOutboxMessages_OutboxMessageId",
                schema: "dbo",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropTable(
                name: "ApiIdempotencyRecords",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "WebhookOutboxMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Partners",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveryLogs_DeliveryId",
                schema: "dbo",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveryLogs_OutboxMessageId",
                schema: "dbo",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveryLogs_WebhookId_OutboxMessageId",
                schema: "dbo",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_SourcePartnerId",
                schema: "dbo",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_KeyHash",
                schema: "dbo",
                table: "ApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_PartnerId_Environment",
                schema: "dbo",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "DeliveryId",
                schema: "dbo",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropColumn(
                name: "OutboxMessageId",
                schema: "dbo",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropColumn(
                name: "SourcePartnerId",
                schema: "dbo",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "DataAccessMode",
                schema: "dbo",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "Environment",
                schema: "dbo",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                schema: "dbo",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "dbo",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "RotatedToKeyId",
                schema: "dbo",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "RotationGraceEndsAt",
                schema: "dbo",
                table: "ApiKeys");

            migrationBuilder.AlterColumn<string>(
                name: "KeyPrefix",
                schema: "dbo",
                table: "ApiKeys",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLogs_WebhookId",
                schema: "dbo",
                table: "WebhookDeliveryLogs",
                column: "WebhookId");
        }
    }
}
