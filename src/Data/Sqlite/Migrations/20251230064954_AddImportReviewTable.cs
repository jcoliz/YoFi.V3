using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoFi.V3.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddImportReviewTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YoFi.V3.ImportReviewTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Payee = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Memo = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DuplicateStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    DuplicateOfKey = table.Column<Guid>(type: "TEXT", nullable: true),
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoFi.V3.ImportReviewTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YoFi.V3.ImportReviewTransactions_YoFi.V3.Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "YoFi.V3.Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.ImportReviewTransactions_Key",
                table: "YoFi.V3.ImportReviewTransactions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.ImportReviewTransactions_TenantId",
                table: "YoFi.V3.ImportReviewTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.ImportReviewTransactions_TenantId_Date",
                table: "YoFi.V3.ImportReviewTransactions",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.ImportReviewTransactions_TenantId_ExternalId",
                table: "YoFi.V3.ImportReviewTransactions",
                columns: new[] { "TenantId", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YoFi.V3.ImportReviewTransactions");
        }
    }
}
