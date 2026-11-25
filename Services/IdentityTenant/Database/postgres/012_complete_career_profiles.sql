

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
UPDATE career_profiles p SET name=r.name,updated_at=CURRENT_TIMESTAMP
FROM requested_career_profiles r WHERE p.code=r.code AND p.name IS DISTINCT FROM r.name;
INSERT INTO career_profiles(code,name)
SELECT r.code,r.name FROM requested_career_profiles r
WHERE NOT EXISTS (SELECT 1 FROM career_profiles p WHERE p.code=r.code)
ORDER BY r.code ON CONFLICT(code) DO NOTHING;
INSERT INTO grades(code,name,display_order)
SELECT 'T'||n,'T'||n,n FROM generate_series(0,6) n
WHERE NOT EXISTS (SELECT 1 FROM grades g WHERE g.code='T'||n)
ORDER BY n ON CONFLICT(code) DO NOTHING;
COMMIT;
