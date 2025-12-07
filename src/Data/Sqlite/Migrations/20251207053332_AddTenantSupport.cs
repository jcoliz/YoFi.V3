using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoFi.V3.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YoFi.V3.Tenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoFi.V3.Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YoFi.V3.UserTenantRoleAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoFi.V3.UserTenantRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YoFi.V3.UserTenantRoleAssignments_YoFi.V3.Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "YoFi.V3.Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.Tenants_Key",
                table: "YoFi.V3.Tenants",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.UserTenantRoleAssignments_TenantId",
                table: "YoFi.V3.UserTenantRoleAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.UserTenantRoleAssignments_UserId_TenantId",
                table: "YoFi.V3.UserTenantRoleAssignments",
                columns: new[] { "UserId", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YoFi.V3.UserTenantRoleAssignments");

            migrationBuilder.DropTable(
                name: "YoFi.V3.Tenants");
        }
    }
}
