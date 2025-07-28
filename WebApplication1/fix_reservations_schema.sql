-- Fix reservations table schema - restructure from CompanyId to LocationId

-- First, check if LocationId column exists and has proper values
-- If not, we need to populate it based on existing data
ALTER TABLE reservations ADD COLUMN IF NOT EXISTS LocationId int NULL;

-- Update LocationId based on existing data (you'll need to adjust this based on your actual data)
-- This assumes you want to map reservations to the first location of each company
UPDATE reservations r 
SET LocationId = (
    SELECT l.Id 
    FROM locations l 
    WHERE l.CompanyId = r.CompanyId 
    LIMIT 1
) 
WHERE r.LocationId IS NULL AND r.CompanyId IS NOT NULL;

-- Now make LocationId NOT NULL and add foreign key
ALTER TABLE reservations MODIFY COLUMN LocationId int NOT NULL;

-- Add foreign key constraint for LocationId if it doesn't exist
ALTER TABLE reservations ADD CONSTRAINT FK_reservations_locations_LocationId 
FOREIGN KEY (LocationId) REFERENCES locations(Id) ON DELETE CASCADE;

-- Drop the CompanyId foreign key constraint if it exists
ALTER TABLE reservations DROP FOREIGN KEY IF EXISTS FK_reservations_companies_CompanyId;

-- Drop the CompanyId column
ALTER TABLE reservations DROP COLUMN IF EXISTS CompanyId;

-- Drop the old index and create new one
DROP INDEX IF EXISTS IX_reservations_CompanyId_ReservationDate_ReservationTime ON reservations;
CREATE INDEX IX_reservations_LocationId_ReservationDate_ReservationTime 
ON reservations(LocationId, ReservationDate, ReservationTime);
