

INSERT INTO skills(code,name,category)
SELECT 'LEGACY_'||md5(lower(trim(e.skill_name))), min(trim(e.skill_name)),
 CASE WHEN count(DISTINCT nullif(trim(e.category),''))=1 THEN min(nullif(trim(e.category),'')) END
FROM employee_skills e
WHERE e.skill_id IS NULL AND length(trim(e.skill_name)) BETWEEN 1 AND 200
 AND (e.category IS NULL OR length(trim(e.category))<=100)
 AND NOT EXISTS (SELECT 1 FROM skills s WHERE lower(trim(s.name))=lower(trim(e.skill_name)))
GROUP BY lower(trim(e.skill_name))
ON CONFLICT(code) DO NOTHING;
WITH matches AS (
 SELECT e.id,min(s.id) AS skill_id FROM employee_skills e
 JOIN skills s ON lower(trim(s.name))=lower(trim(e.skill_name))
 WHERE e.skill_id IS NULL GROUP BY e.id HAVING count(*)=1
), candidates AS (
 SELECT m.*,row_number() OVER(PARTITION BY e.tenant_id,e.employee_id,m.skill_id ORDER BY e.id) AS ordinal
 FROM matches m JOIN employee_skills e ON e.id=m.id
 WHERE NOT EXISTS(SELECT 1 FROM employee_skills linked WHERE linked.tenant_id=e.tenant_id AND linked.employee_id=e.employee_id AND linked.skill_id=m.skill_id)
)
UPDATE employee_skills e SET skill_id=c.skill_id FROM candidates c WHERE e.id=c.id AND c.ordinal=1;
