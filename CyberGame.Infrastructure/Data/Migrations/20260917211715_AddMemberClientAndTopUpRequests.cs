using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberGame.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberClientAndTopUpRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MemberFailedLoginCount",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "MemberLastLoginAtUtc",
                table: "Customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MemberLockoutEndUtc",
                table: "Customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberPinHash",
                table: "Customers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MemberTopUpRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WalletTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberTopUpRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberTopUpRequests_AppUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberTopUpRequests_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberTopUpRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberTopUpRequests_WalletTransactions_WalletTransactionId",
                        column: x => x.WalletTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberTopUpRequests_BranchId",
                table: "MemberTopUpRequests",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberTopUpRequests_CustomerId_Pending",
                table: "MemberTopUpRequests",
                column: "CustomerId",
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_MemberTopUpRequests_ProcessedByUserId",
                table: "MemberTopUpRequests",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberTopUpRequests_WalletTransactionId",
                table: "MemberTopUpRequests",
                column: "WalletTransactionId",
                unique: true,
                filter: "[WalletTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberTopUpRequests");

            migrationBuilder.DropColumn(
                name: "MemberFailedLoginCount",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MemberLastLoginAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MemberLockoutEndUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MemberPinHash",
                table: "Customers");
        }
    }
}
