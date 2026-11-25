BEGIN;
CREATE UNIQUE INDEX IF NOT EXISTS ux_employee_user_tenant ON users(tenant_id,id);
CREATE TABLE IF NOT EXISTS employees (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 tenant_id bigint NOT NULL REFERENCES tenants(id),
 user_id bigint NOT NULL,
 employee_code varchar(4000) NOT NULL,
 first_name varchar(4000) NOT NULL,
 last_name varchar(4000) NOT NULL,
 preferred_name varchar(4000),
 work_email varchar(4000) NOT NULL,
 phone varchar(4000),
 profile_photo_url varchar(4000),
 department_id bigint,
 discipline_id bigint,
 job_title_id bigint,
 designation_id bigint,
 grade_id bigint,
 location_id bigint,
 manager_employee_id bigint,
 employment_type varchar(4000) NOT NULL,
 employment_status varchar(4000) NOT NULL,
 joining_date date NOT NULL,
 end_date date,
 is_deleted boolean NOT NULL DEFAULT false,
 created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
 updated_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
 UNIQUE(tenant_id,id),
 UNIQUE(tenant_id,user_id),
 UNIQUE(tenant_id,employee_code),
 FOREIGN KEY(tenant_id,user_id) REFERENCES users(tenant_id,id),
 CHECK(end_date IS NULL OR end_date >= joining_date),
 CHECK(employment_status IN ('ACTIVE','ON_LEAVE','TERMINATED','INACTIVE')),
 CHECK(employment_type IN ('FULL_TIME','PART_TIME','CONTRACT','INTERN'))
);
CREATE TABLE IF NOT EXISTS employee_references (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 tenant_id bigint NOT NULL REFERENCES tenants(id),
 kind varchar(4000) NOT NULL,
 name varchar(4000) NOT NULL,
 UNIQUE(tenant_id,id),
 UNIQUE(tenant_id,kind,name),
 CHECK(kind IN ('DEPARTMENT','DISCIPLINE','JOB_TITLE','DESIGNATION','GRADE','LOCATION'))
);
CREATE TABLE IF NOT EXISTS employee_skills (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 tenant_id bigint NOT NULL REFERENCES tenants(id),
 employee_id bigint NOT NULL,
 skill_name varchar(4000) NOT NULL,
 category varchar(4000),
 proficiency integer NOT NULL,
 years_of_experience numeric(18,2) NOT NULL,
 last_used_date date,
 UNIQUE(tenant_id,id),
 FOREIGN KEY(tenant_id,employee_id) REFERENCES employees(tenant_id,id),
 CHECK(proficiency BETWEEN 1 AND 5),
 CHECK(years_of_experience BETWEEN 0 AND 80),
 UNIQUE(tenant_id,employee_id,skill_name)
);
CREATE TABLE IF NOT EXISTS employee_educations (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 tenant_id bigint NOT NULL REFERENCES tenants(id),
 employee_id bigint NOT NULL,
 institution varchar(4000) NOT NULL,
 qualification varchar(4000) NOT NULL,
 start_date date NOT NULL,
 end_date date,
 UNIQUE(tenant_id,id),
 FOREIGN KEY(tenant_id,employee_id) REFERENCES employees(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS employee_certifications (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 tenant_id bigint NOT NULL REFERENCES tenants(id),
 employee_id bigint NOT NULL,
 name varchar(4000) NOT NULL,
 issuer varchar(4000) NOT NULL,
 issued_date date NOT NULL,
 expiry_date date,
 credential_url varchar(4000),
 UNIQUE(tenant_id,id),
 FOREIGN KEY(tenant_id,employee_id) REFERENCES employees(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS employee_experiences (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 tenant_id bigint NOT NULL REFERENCES tenants(id),
 employee_id bigint NOT NULL,
 company varchar(4000) NOT NULL,
 title varchar(4000) NOT NULL,
 description varchar(4000),
 start_date date NOT NULL,
 end_date date,
 UNIQUE(tenant_id,id),
 FOREIGN KEY(tenant_id,employee_id) REFERENCES employees(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS employee_languages (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 tenant_id bigint NOT NULL REFERENCES tenants(id),
 employee_id bigint NOT NULL,
 name varchar(4000) NOT NULL,
 proficiency varchar(4000) NOT NULL,
 UNIQUE(tenant_id,id),
 FOREIGN KEY(tenant_id,employee_id) REFERENCES employees(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS employee_compensations (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 tenant_id bigint NOT NULL REFERENCES tenants(id),
 employee_id bigint NOT NULL,
 ctc numeric(18,2) NOT NULL,
 bonus numeric(18,2) NOT NULL,
 currency varchar(4000) NOT NULL,
 effective_from date NOT NULL,
 effective_to date,
 UNIQUE(tenant_id,id),
 FOREIGN KEY(tenant_id,employee_id) REFERENCES employees(tenant_id,id),
 CHECK(ctc >= 0 AND bonus >= 0),
 CHECK(effective_to IS NULL OR effective_to >= effective_from)
);
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_employees_manager_employee_id') THEN ALTER TABLE employees ADD CONSTRAINT fk_employees_manager_employee_id FOREIGN KEY(tenant_id,manager_employee_id) REFERENCES employees(tenant_id,id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_employees_department_id') THEN ALTER TABLE employees ADD CONSTRAINT fk_employees_department_id FOREIGN KEY(tenant_id,department_id) REFERENCES employee_references(tenant_id,id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_employees_discipline_id') THEN ALTER TABLE employees ADD CONSTRAINT fk_employees_discipline_id FOREIGN KEY(tenant_id,discipline_id) REFERENCES employee_references(tenant_id,id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_employees_job_title_id') THEN ALTER TABLE employees ADD CONSTRAINT fk_employees_job_title_id FOREIGN KEY(tenant_id,job_title_id) REFERENCES employee_references(tenant_id,id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_employees_designation_id') THEN ALTER TABLE employees ADD CONSTRAINT fk_employees_designation_id FOREIGN KEY(tenant_id,designation_id) REFERENCES employee_references(tenant_id,id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_employees_grade_id') THEN ALTER TABLE employees ADD CONSTRAINT fk_employees_grade_id FOREIGN KEY(tenant_id,grade_id) REFERENCES employee_references(tenant_id,id); END IF; END $$;
DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_employees_location_id') THEN ALTER TABLE employees ADD CONSTRAINT fk_employees_location_id FOREIGN KEY(tenant_id,location_id) REFERENCES employee_references(tenant_id,id); END IF; END $$;
CREATE INDEX IF NOT EXISTS ix_employees_manager_employee_id ON employees(tenant_id,manager_employee_id) WHERE NOT is_deleted;
CREATE INDEX IF NOT EXISTS ix_employees_department_id ON employees(tenant_id,department_id) WHERE NOT is_deleted;
CREATE INDEX IF NOT EXISTS ix_employees_discipline_id ON employees(tenant_id,discipline_id) WHERE NOT is_deleted;
CREATE INDEX IF NOT EXISTS ix_employees_grade_id ON employees(tenant_id,grade_id) WHERE NOT is_deleted;
CREATE INDEX IF NOT EXISTS ix_employees_location_id ON employees(tenant_id,location_id) WHERE NOT is_deleted;
CREATE INDEX IF NOT EXISTS ix_employees_employment_status ON employees(tenant_id,employment_status) WHERE NOT is_deleted;
CREATE INDEX IF NOT EXISTS ix_employee_skills_employee ON employee_skills(tenant_id,employee_id);
CREATE INDEX IF NOT EXISTS ix_employee_educations_employee ON employee_educations(tenant_id,employee_id);
CREATE INDEX IF NOT EXISTS ix_employee_certifications_employee ON employee_certifications(tenant_id,employee_id);
CREATE INDEX IF NOT EXISTS ix_employee_experiences_employee ON employee_experiences(tenant_id,employee_id);
CREATE INDEX IF NOT EXISTS ix_employee_languages_employee ON employee_languages(tenant_id,employee_id);
CREATE INDEX IF NOT EXISTS ix_employee_compensations_employee ON employee_compensations(tenant_id,employee_id);
CREATE TABLE IF NOT EXISTS employee_outbox (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 event_id uuid NOT NULL UNIQUE,
 payload jsonb NOT NULL,
 occurred_at timestamptz NOT NULL,
 processed_at timestamptz,
 attempt_count integer NOT NULL DEFAULT 0,
 next_attempt_at timestamptz,
 locked_until timestamptz,
 locked_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_employee_outbox_pending ON employee_outbox(id) WHERE processed_at IS NULL;
INSERT INTO permissions(code,name,description) VALUES
('EMPLOYEE_VIEW','Employee View','Employee module permission'),
('EMPLOYEE_CREATE','Employee Create','Employee module permission'),
('EMPLOYEE_UPDATE','Employee Update','Employee module permission'),
('EMPLOYEE_DELETE','Employee Delete','Employee module permission'),
('EMPLOYEE_VIEW_SENSITIVE','Employee View Sensitive','Employee module permission'),
('EMPLOYEE_MANAGE_ORG','Employee Manage Org','Employee module permission'),
('EMPLOYEE_VIEW_TEAM','Employee View Team','Employee module permission'),
('EMPLOYEE_UPDATE_SELF','Employee Update Self','Employee module permission') ON CONFLICT(code) DO NOTHING;
INSERT INTO role_permissions(role_id,permission_id)
SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
WHERE (r.code='ADMIN' AND p.code LIKE 'EMPLOYEE_%')
 OR (r.code IN ('DEVELOPER','PROJECT_MANAGER') AND p.code IN ('EMPLOYEE_VIEW','EMPLOYEE_UPDATE_SELF'))
 OR (r.code='PROJECT_MANAGER' AND p.code='EMPLOYEE_VIEW_TEAM')
ON CONFLICT DO NOTHING;
COMMIT;
