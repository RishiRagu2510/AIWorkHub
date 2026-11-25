ALTER TABLE employees ADD COLUMN IF NOT EXISTS aadhar_number varchar(50);
ALTER TABLE employees ADD COLUMN IF NOT EXISTS permanent_address varchar(1000);
ALTER TABLE employees ADD COLUMN IF NOT EXISTS emergency_contact_name varchar(200);
ALTER TABLE employees ADD COLUMN IF NOT EXISTS emergency_contact_phone varchar(50);
ALTER TABLE employees ADD COLUMN IF NOT EXISTS emergency_contact_relation varchar(100);
