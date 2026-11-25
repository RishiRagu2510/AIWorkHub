
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'projects'
              AND column_name = 'key' AND character_maximum_length < 100) THEN
   ALTER TABLE projects ALTER COLUMN key TYPE VARCHAR(100);
 END IF;
END $$;
