using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChagolTalk.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueReportPerConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId_ConversationId",
                table: "Reports",
                columns: new[] { "ReporterId", "ConversationId" },
                unique: true,
                filter: "\"ConversationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_ReporterId_ConversationId",
                table: "Reports");
        }
    }
}
