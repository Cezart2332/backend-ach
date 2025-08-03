using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRefreshTokenForCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "refresh_tokens",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "refresh_tokens",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_CompanyId",
                table: "refresh_tokens",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_companies_CompanyId",
                table: "refresh_tokens",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_companies_CompanyId",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_CompanyId",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "refresh_tokens");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "refresh_tokens",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
