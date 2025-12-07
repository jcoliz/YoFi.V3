using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoFi.V3.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSimpleTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YoFi.V3.Transactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Payee = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoFi.V3.Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YoFi.V3.Transactions_YoFi.V3.Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "YoFi.V3.Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.Transactions_Key",
                table: "YoFi.V3.Transactions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.Transactions_TenantId",
                table: "YoFi.V3.Transactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.Transactions_TenantId_Date",
                table: "YoFi.V3.Transactions",
                columns: new[] { "TenantId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YoFi.V3.Transactions");
        }
    }
}
