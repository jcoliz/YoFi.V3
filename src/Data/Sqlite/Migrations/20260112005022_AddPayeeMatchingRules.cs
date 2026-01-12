using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoFi.V3.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPayeeMatchingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "YoFi.V3.ImportReviewTransactions",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "YoFi.V3.PayeeMatchingRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PayeePattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PayeeIsRegex = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<string>(type: "TEXT", nullable: true),
                    MatchCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoFi.V3.PayeeMatchingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YoFi.V3.PayeeMatchingRules_YoFi.V3.Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "YoFi.V3.Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.PayeeMatchingRules_Key",
                table: "YoFi.V3.PayeeMatchingRules",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.PayeeMatchingRules_TenantId",
                table: "YoFi.V3.PayeeMatchingRules",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YoFi.V3.PayeeMatchingRules");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "YoFi.V3.ImportReviewTransactions");
        }
    }
}
