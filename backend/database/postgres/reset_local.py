#!/usr/bin/env python3
"""Explicit destructive reset of the dedicated local AIWorkHub Docker data only."""
from pathlib import Path
import argparse,json,os,re,secrets,signal,subprocess,sys,time,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
COMPOSE=ROOT.parent/'infrastructure/docker/docker-compose.yml'
DATABASES=['identity_tenant','jira','audit','notification','rag']
SERVICES=[('IdentityTenant',5233,ROOT/'services/IdentityTenant/IdentityTenant.Api/IdentityTenant.Api.csproj'),('Jira',5148,ROOT/'services/Jira/Jira.Api/Jira.Api.csproj'),('Audit',5067,ROOT/'services/Audit/Audit.Api/Audit.Api.csproj'),('Gateway',5204,ROOT/'gateway/ApiGateway/ApiGateway.csproj')]
def run(args,**kwargs):return subprocess.run(args,check=True,**kwargs)
def sql(db,source):
 return run(['docker','exec','-i','aiworkhub-postgres','psql','-X','-U','aiworkhub','-d',db,'-v','ON_ERROR_STOP=1','-Atq'],input=source,text=True,capture_output=True).stdout.strip()
def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--delete-all-and-bootstrap',action='store_true');args=parser.parse_args()
 if not args.delete_all_and_bootstrap:parser.error('Explicit --delete-all-and-bootstrap is required.')
 compose=['docker','compose','-f',str(COMPOSE)]
 config=json.loads(run(compose+['config','--format','json'],capture_output=True,text=True).stdout)
 expected={'aiworkhub_'+x+'_data' for x in ['postgres','mongodb','redis','kafka','minio']}
 if config['name']!='aiworkhub' or {v['name'] for v in config['volumes'].values()}!=expected:raise RuntimeError('Unexpected Docker project/volumes; refusing reset.')
 if any(v.get('external') for v in config['volumes'].values()):raise RuntimeError('Refusing external volumes.')
 client=sql('identity_tenant_db',"SELECT client_id FROM identity_providers WHERE provider_type='GOOGLE' ORDER BY id LIMIT 1;")
 if not client or not re.fullmatch(r'[A-Za-z0-9._-]+',client):raise RuntimeError('Google client configuration must be available before reset.')
 topics=run(['docker','exec','aiworkhub-kafka','/opt/kafka/bin/kafka-topics.sh','--bootstrap-server','kafka:19092','--list'],capture_output=True,text=True).stdout.splitlines()
 topics=sorted({t for t in topics if t.startswith('aiworkhub.')}|{'aiworkhub.issue.audit','aiworkhub.issue.audit.dlq'})

 secret_files=[]
 for name,port,project in SERVICES[:3]:
  sid=ET.parse(project).findtext('.//UserSecretsId')
  path=Path.home()/'.microsoft/usersecrets'/sid/'secrets.json'
  secret_files.append((path,json.loads(path.read_text(encoding='utf-8-sig'))))
 writers=set()
 for name,port,project in SERVICES:
  if not project.exists():raise RuntimeError('Missing service project: '+str(project))
  pids=subprocess.run(['lsof','-tiTCP:'+str(port),'-sTCP:LISTEN'],capture_output=True,text=True).stdout.split()
  for pid in set(pids):
   command=run(['ps','-p',pid,'-o','comm='],capture_output=True,text=True).stdout
   if not any(x in command for x in ['dotnet','IdentityTenant','Jira','Audit','ApiGateway']):raise RuntimeError('Unexpected service on '+str(port))
   writers.add(int(pid))
 for pid in writers:os.kill(pid,signal.SIGTERM)
 for attempt in range(50):
  alive=[pid for pid in writers if subprocess.run(['ps','-p',str(pid),'-o','pid='],capture_output=True).returncode==0]
  if not alive:break
  time.sleep(.2)
 else:raise RuntimeError('Backend writers did not stop; data was not reset.')
 print('Backend writers stopped. Clearing dedicated local Docker data.',flush=True)
 run(compose+['down','--volumes'])
 run(compose+['up','-d','--wait','--wait-timeout','240'])
 run([sys.executable,str(ROOT/'database/postgres/apply.py'),'--apply'])
 bootstrap=(ROOT/'database/postgres/setup/grid_dynamics.sql').read_text()
 sql('identity_tenant_db','\\set google_client_id '+client+'\n'+bootstrap)

 reset_sequences="""DO $$ DECLARE r record; seq text; maximum bigint; BEGIN
 FOR r IN SELECT table_name,column_name FROM information_schema.columns WHERE table_schema='public' AND (is_identity='YES' OR column_default LIKE 'nextval(%') LOOP
  seq:=pg_get_serial_sequence(format('public.%I',r.table_name),r.column_name);
  IF seq IS NOT NULL THEN
   EXECUTE format('SELECT max(%I) FROM public.%I',r.column_name,r.table_name) INTO maximum;
   PERFORM setval(seq::regclass,COALESCE(maximum,1),maximum IS NOT NULL);
  END IF;
 END LOOP; END $$;"""
 for service in DATABASES:sql(service+'_db',reset_sequences)
 for topic in topics:run(['docker','exec','aiworkhub-kafka','/opt/kafka/bin/kafka-topics.sh','--bootstrap-server','kafka:19092','--create','--if-not-exists','--topic',topic,'--partitions','1','--replication-factor','1'],capture_output=True,text=True)
 key=secrets.token_urlsafe(64)
 for path,data in secret_files:
  if isinstance(data.get('Jwt'),dict):
   data['Jwt']['SigningKey']=key
   data.pop('Jwt:SigningKey',None)
  else:data['Jwt:SigningKey']=key
  temp=path.with_suffix('.reset.tmp');temp.write_text(json.dumps(data,indent=2)+'\n');os.chmod(temp,0o600);temp.replace(path)
 print('Setup rebuilt and previous access tokens invalidated.',flush=True)
 for name,port,project in SERVICES:
  with open('/private/tmp/gridhub-'+name.lower()+'-runtime.log','a') as log:
   subprocess.Popen(['dotnet','run','--no-build','--project',str(project),'--launch-profile','http'],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
 print('Backend services restarted.',flush=True)
 for service in DATABASES:
  counts=sql(service+'_db',"SELECT count(*) FROM information_schema.tables WHERE table_schema='public';")
  print(service+'_db: '+counts+' tables',flush=True)
 print(sql('identity_tenant_db',"SELECT 'tenants='||count(*)||', min_id='||min(id) FROM tenants; SELECT 'users='||count(*)||', min_id='||min(id) FROM users; SELECT 'employees='||count(*) FROM employees; SELECT 'career_profiles='||count(*) FROM career_profiles; SELECT 'grades='||count(*) FROM grades;"),flush=True)
 print(sql('jira_db',"SELECT 'projects='||count(*) FROM projects; SELECT 'issues='||count(*) FROM issues; SELECT 'work_logs='||count(*) FROM work_logs;"),flush=True)
if __name__=='__main__':main()
