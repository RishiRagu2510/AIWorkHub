
ALTER TABLE grades ADD COLUMN IF NOT EXISTS family varchar(100);
UPDATE grades SET family='Engineering' WHERE family IS NULL AND code ~ '^T[0-6]$';
ALTER TABLE employee_skills ADD COLUMN IF NOT EXISTS skill_id bigint REFERENCES skills(id) ON DELETE RESTRICT;
WITH matches AS (
 SELECT e.id, min(s.id) AS skill_id
 FROM employee_skills e JOIN skills s ON lower(trim(e.skill_name))=lower(trim(s.name))
 WHERE e.skill_id IS NULL
 GROUP BY e.id HAVING count(*)=1
), unique_links AS (
 SELECT m.*, row_number() OVER (PARTITION BY e.tenant_id,e.employee_id,m.skill_id ORDER BY e.id) AS ordinal
 FROM matches m JOIN employee_skills e ON e.id=m.id
 WHERE NOT EXISTS (SELECT 1 FROM employee_skills linked WHERE linked.tenant_id=e.tenant_id AND linked.employee_id=e.employee_id AND linked.skill_id=m.skill_id)
)
UPDATE employee_skills e SET skill_id=m.skill_id FROM unique_links m WHERE e.id=m.id AND m.ordinal=1;
CREATE UNIQUE INDEX IF NOT EXISTS uq_employee_skill_master ON employee_skills(tenant_id,employee_id,skill_id);
CREATE INDEX IF NOT EXISTS "IX_employee_skills_skill_id" ON employee_skills(skill_id);
