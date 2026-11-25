import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const cwd = fileURLToPath(new URL('..', import.meta.url));
const children = ['landing', 'hub', 'gridhub', 'persona', 'chronos'].map((name) => spawn(process.execPath,
 ['--max-http-header-size=65536', 'node_modules/vite/bin/vite.js', '--config', `${name}/vite.config.js`], { cwd, stdio: 'inherit' }));
let stopping = false;
function stop(code = 0) { if (stopping) return; stopping = true; for (const child of children) child.kill('SIGTERM'); process.exitCode = code; }
for (const child of children) { child.on('error', () => stop(1)); child.on('exit', (code) => { if (!stopping) stop(code || 1); }); }
process.on('SIGINT', () => stop()); process.on('SIGTERM', () => stop());
