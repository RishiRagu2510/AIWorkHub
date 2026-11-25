#!/usr/bin/env python3
"""
Deploy and apply AIWorkHub database schemas and seeds to a cloud PostgreSQL database
(e.g., Supabase, Render PostgreSQL, Neon).

Usage:
    python3 Scripts/database/deploy_cloud_db.py --url "postgresql://postgres:password@host:port/dbname"
    # Or export DATABASE_URL="postgresql://..."
    python3 Scripts/database/deploy_cloud_db.py
"""

import argparse
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]

SCHEMA_DIRS = [
    ("IdentityTenant", ROOT / "Services/IdentityTenant/Database/postgres"),
    ("Jira", ROOT / "Services/Jira/Database/postgres"),
    ("Audit", ROOT / "Services/Audit/Database/postgres"),
    ("AIAgent", ROOT / "Services/AIAgent/Database/postgres"),
]

def run_psql(connection_url: str, sql_file: Path) -> bool:
    cmd = [
        "psql",
        "-X",
        "-v", "ON_ERROR_STOP=1",
        connection_url,
        "-f", str(sql_file)
    ]
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        # Ignore already exists errors for repeatable schemas
        if "already exists" in result.stderr.lower():
            print(f"  [Notice] {sql_file.name}: Some entities already exist (skipping duplicates).")
            return True
        print(f"  [ERROR] Failed on {sql_file.name}:")
        print(result.stderr)
        return False
    return True

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default=os.getenv("DATABASE_URL"), help="PostgreSQL connection URI")
    args = parser.parse_args()

    if not args.url:
        print("Error: No database connection URL provided.")
        print("Please pass --url 'postgresql://...' or set DATABASE_URL environment variable.")
        sys.exit(1)

    url = args.url

    print("==================================================")
    print("AIWorkHub Cloud Database Migration")
    print("==================================================")

    # Enable pgvector if available (for AIAgent/RAG)
    try:
        subprocess.run(
            ["psql", "-X", url, "-c", "CREATE EXTENSION IF NOT EXISTS vector;"],
            capture_output=True, text=True
        )
        print("Enabled extension: vector (if supported)")
    except Exception:
        pass

    for service_name, schema_dir in SCHEMA_DIRS:
        print(f"\nApplying migrations for: {service_name} ({schema_dir.relative_to(ROOT)})")
        sql_files = sorted(schema_dir.glob("*.sql"))
        if not sql_files:
            print("  No .sql files found.")
            continue

        for sql_file in sql_files:
            print(f"  -> Executing {sql_file.name}...")
            success = run_psql(url, sql_file)
            if not success:
                print(f"\nMigration halted at {service_name}/{sql_file.name}")
                sys.exit(1)

    print("\n==================================================")
    print("All schemas and seed data applied successfully!")
    print("==================================================")

if __name__ == "__main__":
    main()
