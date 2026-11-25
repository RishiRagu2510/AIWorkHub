ALTER TABLE employee_educations ADD COLUMN IF NOT EXISTS field_of_study varchar(200);
ALTER TABLE employee_certifications ADD COLUMN IF NOT EXISTS credential_id varchar(200);
