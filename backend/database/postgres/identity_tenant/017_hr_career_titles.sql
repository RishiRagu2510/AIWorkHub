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
