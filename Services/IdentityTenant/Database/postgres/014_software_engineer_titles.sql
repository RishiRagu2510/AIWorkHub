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
ON CONFLICT(career_profile_id,grade_id) DO UPDATE SET career_title=EXCLUDED.career_title,updated_at=CURRENT_TIMESTAMP
WHERE career_levels.career_title IS DISTINCT FROM EXCLUDED.career_title;
COMMIT;
