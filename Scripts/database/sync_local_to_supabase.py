#!/usr/bin/env python3
"""
Sync local Docker Postgres data into Supabase Postgres.
"""

import os
from pathlib import Path
import subprocess
import sys

URL = "postgresql://postgres.ufapujgzduuuzkujekrh:25102003%40Rishi@aws-1-ap-south-1.pooler.supabase.com:5432/postgres"
DUMP_DIR = Path("/tmp/aiworkhub_dump")

def run_psql(sql: str) -> str:
    res = subprocess.run(["psql", "-X", URL, "-c", sql], capture_output=True, text=True)
    if res.returncode != 0:
        print("SQL Error:", res.stderr)
    return res.stdout

def run_file(path: Path):
    print(f"Loading {path.name} ({path.stat().st_size // 1024} KB)...")
    cmd = f"""psql -X "{URL}" << 'EOF'
SET session_replication_role = 'replica';
\\i {path}
SET session_replication_role = 'origin';
EOF"""
    res = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    if res.returncode != 0:
        print(f"Error in {path.name}:", res.stderr[:500])
    else:
        print(f"Loaded {path.name} successfully.")

def main():
    print("Preparing Supabase public tables...")
    # Get all tables in public schema
    res = subprocess.run(
        ["psql", "-X", "-t", "-A", URL, "-c",
         "SELECT tablename FROM pg_tables WHERE schemaname = 'public';"],
        capture_output=True, text=True
    )
    tables = [t.strip() for t in res.stdout.strip().splitlines() if t.strip()]
    if tables:
        print(f"Truncating {len(tables)} tables with CASCADE...")
        truncate_sql = f"TRUNCATE {', '.join(tables)} CASCADE;"
        subprocess.run(["psql", "-X", URL, "-c", f"SET session_replication_role = 'replica'; {truncate_sql}"], capture_output=True)

    # Load dumps
    for name in ["identity.sql", "jira.sql", "audit.sql", "rag.sql"]:
        p = DUMP_DIR / name
        if p.exists():
            run_file(p)

    # Reset sequences to max id
    print("Syncing sequences...")
    reset_seq_sql = """
    DO $$
    DECLARE
        r RECORD;
    BEGIN
        FOR r IN (
            SELECT table_name, column_name, sequence_name
            FROM (
                SELECT c.table_name, c.column_name,
                       pg_get_serial_sequence(c.table_name, c.column_name) as sequence_name
                FROM information_schema.columns c
                WHERE c.table_schema = 'public'
            ) s
            WHERE sequence_name IS NOT NULL
        ) LOOP
            EXECUTE format('SELECT setval(%L, COALESCE((SELECT MAX(%I) FROM %I), 1))',
                           r.sequence_name, r.column_name, r.table_name);
        END LOOP;
    END $$;
    """
    subprocess.run(["psql", "-X", URL, "-c", reset_seq_sql], capture_output=True)
    print("All sequences synced.")

if __name__ == "__main__":
    main()
