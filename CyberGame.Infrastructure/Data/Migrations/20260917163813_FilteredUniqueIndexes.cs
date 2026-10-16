using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberGame.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FilteredUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaySessions_Invoices_InvoiceId",
                table: "PlaySessions");

            migrationBuilder.DropIndex(
                name: "IX_PlaySessions_InvoiceId",
                table: "PlaySessions");

            migrationBuilder.DropIndex(
                name: "IX_PlaySessions_MachineId",
                table: "PlaySessions");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_BranchId_UserId_Open",
                table: "Shifts",
                columns: new[] { "BranchId", "UserId" },
                unique: true,
                filter: "[Status] = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_MachineId_Active",
                table: "PlaySessions",
                column: "MachineId",
                unique: true,
                filter: "[Status] IN ('Open', 'Paused')");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PlaySessionId",
                table: "Invoices",
                column: "PlaySessionId",
                unique: true,
                filter: "[PlaySessionId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_PlaySessions_PlaySessionId",
                table: "Invoices",
                column: "PlaySessionId",
                principalTable: "PlaySessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_PlaySessions_PlaySessionId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_BranchId_UserId_Open",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_PlaySessions_MachineId_Active",
                table: "PlaySessions");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PlaySessionId",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_InvoiceId",
                table: "PlaySessions",
                column: "InvoiceId",
                unique: true,
                filter: "[InvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_MachineId",
                table: "PlaySessions",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaySessions_Invoices_InvoiceId",
                table: "PlaySessions",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
