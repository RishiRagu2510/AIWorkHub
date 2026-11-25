#!/usr/bin/env python3
"""Validate schemas in disposable databases; optionally apply to local Docker databases."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[2]
SCHEMAS = {
    'identity_tenant': ROOT / 'Services/IdentityTenant/Database/postgres',
    'jira': ROOT / 'Services/Jira/Database/postgres',
    'audit': ROOT / 'Services/Audit/Database/postgres',
    'rag': ROOT / 'Services/AIAgent/Database/postgres',
    'notification': ROOT / 'Infrastructure/database/postgres/notification',
}
UPGRADES = {'identity_tenant': 3, 'jira': 11, 'audit': 2, 'rag': 6}
SERVICES = ['identity_tenant', 'jira', 'audit', 'notification', 'rag']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true', help='Apply validated schemas and reference permissions (no demo data).')
    parser.add_argument('--container', default='aiworkhub-postgres')
    parser.add_argument('--user', default='aiworkhub')
    args = parser.parse_args()

    def sql(db, source):
        result = subprocess.run(['docker', 'exec', '-i', args.container, 'psql', '-X',
                                 '-U', args.user, '-d', db, '-v', 'ON_ERROR_STOP=1', '-Atq'],
                                input=source, text=True, capture_output=True)
        if result.returncode:
            raise RuntimeError(f'{db}: {result.stderr.strip()}')
        return result.stdout.strip()

    def columns(db):
        return json.loads(sql(db, """SELECT coalesce(json_agg(json_build_array(table_name,column_name)), '[]')
          FROM information_schema.columns WHERE table_schema='public';"""))

    def execute(db, paths):
        for path in paths:
            sql(db, path.read_text())


    expected = {}
    for service in SERVICES:
        paths = sorted(p for p in SCHEMAS[service].glob('*.sql') if 'seed' not in p.name)
        scratch = 'aiw_schema_check_' + uuid.uuid4().hex[:16]
        sql('postgres', f'CREATE DATABASE {scratch};')
        try:
            if service == 'rag':
                sql(scratch, 'CREATE EXTENSION IF NOT EXISTS vector;')
            execute(scratch, paths)
            upgrades = [p for p in paths if int(p.name[:3]) >= UPGRADES.get(service, 1000)]
            execute(scratch, upgrades)  # Repeatability on the latest schema.
            if service == 'identity_tenant':
                execute(scratch, [SCHEMAS[service] / '002_seed.sql'] * 2)
            expected[service] = {tuple(c) for c in columns(scratch)}
            print(f'{service}: fresh schema and repeatable upgrades passed', flush=True)
        finally:
            sql('postgres', f'DROP DATABASE {scratch} WITH (FORCE);')

    if not args.apply:
        return
    for service in SERVICES:
        db = service + '_db'
        paths = sorted(p for p in SCHEMAS[service].glob('*.sql') if 'seed' not in p.name)
        existing = columns(db)
        if existing:


            baseline_tables = set()
            for p in paths:
                if int(p.name[:3]) < UPGRADES.get(service, 1000):
                    baseline_tables.update(re.findall(r'CREATE TABLE\s+(?:IF NOT EXISTS\s+)?(\w+)', p.read_text(), re.I))
            missing = baseline_tables - {c[0] for c in existing}
            if missing:
                raise RuntimeError(f'{db}: incomplete baseline; missing tables {sorted(missing)}')
            paths = [p for p in paths if int(p.name[:3]) >= UPGRADES.get(service, 1000)]
        if service == 'rag':
            sql(db, 'CREATE EXTENSION IF NOT EXISTS vector;')
        execute(db, paths)
        if service == 'identity_tenant':
            execute(db, [SCHEMAS[service] / '002_seed.sql'])
        missing = expected[service] - {tuple(c) for c in columns(db)}
        if missing:
            raise RuntimeError(f'{db}: missing schema columns {sorted(missing)}')
        print(f'{db}: applied and verified; no demo data inserted', flush=True)


if __name__ == '__main__':
    main()
