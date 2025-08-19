using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLocationFileStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns for file paths
            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "locations",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MenuPath",
                table: "locations",
                type: "longtext",
                nullable: true);

            // Note: We keep the old Photo and MenuData columns temporarily for data migration
            // They can be removed in a future migration after data is migrated
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "MenuPath",
                table: "locations");
        }
    }
}
