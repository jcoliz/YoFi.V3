using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoFi.V3.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionRecordFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "YoFi.V3.Transactions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Memo",
                table: "YoFi.V3.Transactions",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "YoFi.V3.Transactions",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.Transactions_TenantId_ExternalId",
                table: "YoFi.V3.Transactions",
                columns: new[] { "TenantId", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_YoFi.V3.Transactions_TenantId_ExternalId",
                table: "YoFi.V3.Transactions");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "YoFi.V3.Transactions");

            migrationBuilder.DropColumn(
                name: "Memo",
                table: "YoFi.V3.Transactions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "YoFi.V3.Transactions");
        }
    }
}
