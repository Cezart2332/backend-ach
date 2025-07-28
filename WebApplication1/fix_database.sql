-- Fix database schema by removing ProfileImage and Tags columns from companies table if they exist

-- Check if ProfileImage column exists and drop it
SET @col_exists = 0;
SELECT 1 INTO @col_exists
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'acumh' 
  AND TABLE_NAME = 'companies' 
  AND COLUMN_NAME = 'ProfileImage';

SET @sql = IF(@col_exists > 0, 'ALTER TABLE companies DROP COLUMN ProfileImage', 'SELECT "ProfileImage column does not exist"');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Check if Tags column exists and drop it
SET @col_exists = 0;
SELECT 1 INTO @col_exists
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'acumh' 
  AND TABLE_NAME = 'companies' 
  AND COLUMN_NAME = 'Tags';

SET @sql = IF(@col_exists > 0, 'ALTER TABLE companies DROP COLUMN Tags', 'SELECT "Tags column does not exist"');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Show the current companies table structure
DESCRIBE companies;
