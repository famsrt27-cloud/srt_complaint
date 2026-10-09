using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Complaint.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteAttachmentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ComplaintNoteId",
                schema: "dbo",
                table: "ComplaintAttachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintAttachments_ComplaintNoteId",
                schema: "dbo",
                table: "ComplaintAttachments",
                column: "ComplaintNoteId");

            migrationBuilder.AddForeignKey(
                name: "FK_ComplaintAttachments_ComplaintNotes_ComplaintNoteId",
                schema: "dbo",
                table: "ComplaintAttachments",
                column: "ComplaintNoteId",
                principalSchema: "dbo",
                principalTable: "ComplaintNotes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComplaintAttachments_ComplaintNotes_ComplaintNoteId",
                schema: "dbo",
                table: "ComplaintAttachments");

            migrationBuilder.DropIndex(
                name: "IX_ComplaintAttachments_ComplaintNoteId",
                schema: "dbo",
                table: "ComplaintAttachments");

            migrationBuilder.DropColumn(
                name: "ComplaintNoteId",
                schema: "dbo",
                table: "ComplaintAttachments");
        }
    }
}
