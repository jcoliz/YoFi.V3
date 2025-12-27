using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoFi.V3.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionSplits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YoFi.V3.Splits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionId = table.Column<long>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Memo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoFi.V3.Splits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YoFi.V3.Splits_YoFi.V3.Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "YoFi.V3.Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.Splits_Category",
                table: "YoFi.V3.Splits",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.Splits_Key",
                table: "YoFi.V3.Splits",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.Splits_TransactionId",
                table: "YoFi.V3.Splits",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.Splits_TransactionId_Order",
                table: "YoFi.V3.Splits",
                columns: new[] { "TransactionId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YoFi.V3.Splits");
        }
    }
}
