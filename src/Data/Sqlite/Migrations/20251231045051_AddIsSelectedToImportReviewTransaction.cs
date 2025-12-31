using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoFi.V3.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSelectedToImportReviewTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSelected",
                table: "YoFi.V3.ImportReviewTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.ImportReviewTransactions_TenantId_IsSelected",
                table: "YoFi.V3.ImportReviewTransactions",
                columns: new[] { "TenantId", "IsSelected" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_YoFi.V3.ImportReviewTransactions_TenantId_IsSelected",
                table: "YoFi.V3.ImportReviewTransactions");

            migrationBuilder.DropColumn(
                name: "IsSelected",
                table: "YoFi.V3.ImportReviewTransactions");
        }
    }
}
