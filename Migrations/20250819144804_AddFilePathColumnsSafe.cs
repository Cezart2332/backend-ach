using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddFilePathColumnsSafe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safely add PhotoPath column if it doesn't exist
            migrationBuilder.Sql(@"
                SET @column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'locations'
                    AND COLUMN_NAME = 'PhotoPath'
                );
                
                SET @sql = IF(@column_exists = 0, 
                    'ALTER TABLE locations ADD COLUMN PhotoPath longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL;', 
                    'SELECT ''PhotoPath column already exists'';'
                );
                
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Safely add MenuPath column if it doesn't exist
            migrationBuilder.Sql(@"
                SET @column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'locations'
                    AND COLUMN_NAME = 'MenuPath'
                );
                
                SET @sql = IF(@column_exists = 0, 
                    'ALTER TABLE locations ADD COLUMN MenuPath longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL;', 
                    'SELECT ''MenuPath column already exists'';'
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
                    AND TABLE_NAME = 'locations'
                    AND COLUMN_NAME = 'PhotoPath'
                );
                
                SET @sql = IF(@column_exists > 0, 
                    'ALTER TABLE locations DROP COLUMN PhotoPath;', 
                    'SELECT ''PhotoPath column does not exist'';'
                );
                
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Remove MenuPath column if it exists
            migrationBuilder.Sql(@"
                SET @column_exists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'locations'
                    AND COLUMN_NAME = 'MenuPath'
                );
                
                SET @sql = IF(@column_exists > 0, 
                    'ALTER TABLE locations DROP COLUMN MenuPath;', 
                    'SELECT ''MenuPath column does not exist'';'
                );
                
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }
    }
}
