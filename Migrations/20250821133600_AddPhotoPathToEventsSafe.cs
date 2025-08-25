using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoPathToEventsSafe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safely add PhotoPath column to Events table if it doesn't exist
            migrationBuilder.Sql(@"
                SET @column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'events'
                    AND COLUMN_NAME = 'PhotoPath'
                );
                
                SET @sql = IF(@column_exists = 0, 
                    'ALTER TABLE events ADD COLUMN PhotoPath longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL;', 
                    'SELECT ''PhotoPath column already exists in events table'';'
                );
                
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove PhotoPath column if it exists
            migrationBuilder.Sql(@"
                SET @column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'events'
                    AND COLUMN_NAME = 'PhotoPath'
                );
                
                SET @sql = IF(@column_exists > 0, 
                    'ALTER TABLE events DROP COLUMN PhotoPath;', 
                    'SELECT ''PhotoPath column does not exist in events table'';'
                );
                
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }
    }
}
