


BEGIN;
CREATE TEMP TABLE requested_career_profiles(code text PRIMARY KEY,name text) ON COMMIT DROP;
INSERT INTO requested_career_profiles(code,name) VALUES
 ('AI_SDLC_ENGINEER','AI SDLC Engineer'),
 ('ANALYST','Analyst'),
 ('ARCHITECT','Architect'),
 ('BACKEND_DEVELOPER','Backend Developer'),
 ('BIG_DATA_ENGINEER','Big Data Engineer Profiles'),
 ('CUSTOMER_SUPPORT_ANALYST','Customer Support Analyst'),
 ('DATA_SCIENTIST','Data Scientist'),
 ('DELIVERY_MANAGEMENT','Delivery Management'),
 ('DELIVERY_MANAGER','Delivery Manager (Inactive)'),
 ('DESIGN','Design Profiles'),
 ('DEVOPS_ENGINEER','DevOps Engineer Profiles'),
 ('HR','HR Profiles'),
 ('HYBRIS_DEVELOPER','Hybris Developer'),
 ('MACHINE_LEARNING_ENGINEER','Machine Learning Engineer'),
 ('MOBILE_DEVELOPER','Mobile Developer Profiles'),
 ('NOC_ENGINEER','NOC Engineer'),
 ('NONTECHNICAL_ENGINEERING_REQUIREMENTS','Nontechnical Requirements for Engineers'),
 ('QUALITY_ENGINEER','Quality Engineer Profiles'),
 ('RECRUITMENT','Recruitment'),
 ('UI_APP_ENGINEER','UI App Engineer'),
 ('UI_DEVELOPER','UI Developer Profiles'),
 ('UI_MOBILE_ENGINEER','UI Mobile Engineer'),
 ('DOTNET_DEVELOPER','.NET Developer'),
 ('APPLICATION_ECOMMERCE_DEVELOPER','Application (Ecommerce) Developer Specialization'),
 ('DISTRIBUTED_SYSTEM_DEVELOPER','Distributed System Developer Specialization'),
 ('JAVA_BACKEND_DEVELOPER_NEW','Java Backend Developer (New)'),
 ('PYTHON_DEVELOPER','Python Developer'),
 ('RUBY_DEVELOPER','Ruby Developer'),
 ('SEARCH_DEVELOPER','Search Developer Specialization'),
 ('DATA_SCIENTIST_NEW','Data Scientist (New)'),
 ('DEVOPS_ENGINEER_NEW','DevOps Engineer (New)');
INSERT INTO career_profiles(code,name)
SELECT r.code,r.name FROM requested_career_profiles r
WHERE NOT EXISTS (SELECT 1 FROM career_profiles p WHERE p.code=r.code)
ORDER BY r.code ON CONFLICT(code) DO NOTHING;
INSERT INTO grades(code,name,display_order)
SELECT 'T'||n,'T'||n,n FROM generate_series(0,6) n
WHERE NOT EXISTS (SELECT 1 FROM grades g WHERE g.code='T'||n)
ORDER BY n ON CONFLICT(code) DO NOTHING;
INSERT INTO grades(code,name,display_order,family)
SELECT code,code,n,'HR' FROM (VALUES ('H1',1),('H2',2),('H3',3),('H4',4)) v(code,n)
ON CONFLICT(code) DO NOTHING;
UPDATE grades SET family='HR' WHERE family IS NULL AND code IN ('H1','H2','H3','H4');
COMMIT;

BEGIN;

WITH titles AS (SELECT p.id AS profile_id,g.id AS grade_id,v.title,v.sort_order
 FROM career_profiles p CROSS JOIN (VALUES
 ('T0','Intern',0),('T1','Junior Software Engineer',1),('T2','Software Engineer',2),
 ('T3','Senior Software Engineer',3),('T4','Staff Software Engineer',4)) v(code,title,sort_order)
 JOIN grades g ON g.code=v.code
 WHERE p.code IN ('DOTNET_DEVELOPER','BACKEND_DEVELOPER','APPLICATION_ECOMMERCE_DEVELOPER',
 'DISTRIBUTED_SYSTEM_DEVELOPER','JAVA_BACKEND_DEVELOPER_NEW','PYTHON_DEVELOPER','RUBY_DEVELOPER',
 'SEARCH_DEVELOPER','HYBRIS_DEVELOPER','MOBILE_DEVELOPER','UI_APP_ENGINEER','UI_DEVELOPER','UI_MOBILE_ENGINEER'))
INSERT INTO career_levels(career_profile_id,grade_id,career_title,display_order)
SELECT profile_id,grade_id,title,sort_order FROM titles
ON CONFLICT(career_profile_id,grade_id) DO NOTHING;
COMMIT;

BEGIN;


INSERT INTO career_levels(career_profile_id,grade_id,career_title,display_order)
SELECT p.id,g.id,v.title,v.sort_order
FROM career_profiles p
CROSS JOIN (VALUES
 ('H1','HR Associate',1),
 ('H2','HR Executive',2),
 ('H3','Senior HR Executive',3),
 ('H4','HR Manager',4)
) v(code,title,sort_order)
JOIN grades g ON g.code=v.code AND g.is_active
WHERE p.code='HR' AND p.is_active
ON CONFLICT(career_profile_id,grade_id) DO NOTHING;

COMMIT;
