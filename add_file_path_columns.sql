-- Add PhotoPath and MenuPath columns to locations table
ALTER TABLE locations 
ADD COLUMN PhotoPath VARCHAR(500) NULL,
ADD COLUMN MenuPath VARCHAR(500) NULL;

-- Verify the columns were added
DESCRIBE locations;
