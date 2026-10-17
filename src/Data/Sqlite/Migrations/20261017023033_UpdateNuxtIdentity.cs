using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoFi.V3.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNuxtIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Key",
                table: "RefreshTokens",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """UPDATE "RefreshTokens" SET "Key" = lower(hex(randomblob(16)))""");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Key",
                table: "RefreshTokens",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_Key",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "RefreshTokens");
        }
    }
}
