using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyBinaryDataAndAddFilePathColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new file path columns if they don't exist
            migrationBuilder.Sql(@"
                ALTER TABLE locations 
                ADD COLUMN IF NOT EXISTS PhotoPath VARCHAR(500) NULL,
                ADD COLUMN IF NOT EXISTS MenuPath VARCHAR(500) NULL;
            ");

            // Remove the old binary data columns
            migrationBuilder.Sql(@"
                ALTER TABLE locations 
                DROP COLUMN IF EXISTS Photo,
                DROP COLUMN IF EXISTS MenuData,
                DROP COLUMN IF EXISTS MenuName,
                DROP COLUMN IF EXISTS HasMenu;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add back the old columns
            migrationBuilder.Sql(@"
                ALTER TABLE locations 
                ADD COLUMN IF NOT EXISTS Photo LONGBLOB,
                ADD COLUMN IF NOT EXISTS MenuData LONGBLOB,
                ADD COLUMN IF NOT EXISTS MenuName VARCHAR(255) NOT NULL DEFAULT '',
                ADD COLUMN IF NOT EXISTS HasMenu BOOLEAN NOT NULL DEFAULT FALSE;
            ");

            // Remove the new columns
            migrationBuilder.Sql(@"
                ALTER TABLE locations 
                DROP COLUMN IF EXISTS PhotoPath,
                DROP COLUMN IF EXISTS MenuPath;
            ");
        }
    }
}
