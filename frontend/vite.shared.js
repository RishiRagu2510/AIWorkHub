import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv } from 'vite';
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
import path from 'node:path';
const workspace = fileURLToPath(new URL('.', import.meta.url));
export function createPortalConfig(name) {
  return defineConfig(({ mode }) => {
    const appRoot = path.join(workspace, name);
    const env = { ...loadEnv(mode, workspace, 'VITE_'), ...loadEnv(mode, appRoot, 'VITE_') };
    const portal = { hub: 'hub', gridhub: 'workspace', persona: 'employee', chronos: 'timesheet', landing: 'landing' }[name];
    const ports = { hub: 5172, workspace: 5173, employee: 5174, timesheet: 5175, landing: 5171 };
    const certKeyPath = path.join(workspace, '.certs/localhost.key');
    const certCrtPath = path.join(workspace, '.certs/localhost.crt');
    const https = fs.existsSync(certKeyPath) && fs.existsSync(certCrtPath)
      ? { key: fs.readFileSync(certKeyPath), cert: fs.readFileSync(certCrtPath) }
      : false;
    const target = env.VITE_API_BASE_URL || 'http://localhost:5204';
    const proxy = { target, changeOrigin: false, xfwd: true };
    return {
      root: appRoot,
      envDir: workspace,
      build: { outDir: 'dist', emptyOutDir: true },
      plugins: [react(), {
        name: 'remove-retired-shared-session-cookie',
        configureServer(server) {
          server.middlewares.use((req, res, next) => {
            // Retire only the old shared cookie and its chunks. Portal cookies stay intact.
            const names = (req.headers.cookie || '').split(';').map(c => c.trim().split('=')[0])
              .filter(name => /^AIWorkHub\.Session(?:C\d+)?$/.test(name));
            if (names.length) res.setHeader('Set-Cookie', [...new Set(names)].map(name =>
              `${name}=; Path=/; Max-Age=0; HttpOnly; SameSite=Lax`));
            next();
          });
        },
      }],
      cacheDir: path.join(workspace, `node_modules/.vite-${name}`),
      define: { ...Object.fromEntries(Object.entries(env).map(([key, value]) => [`import.meta.env.${key}`, JSON.stringify(value)])), 'import.meta.env.VITE_PORTAL': JSON.stringify(portal) },
      server: {
        https,
        fs: { allow: [workspace, '/Users/rragupathi/RiderProjects/Poc_Project/AIWorkHub/frontend'] },
        host: '0.0.0.0',
        port: ports[portal],
        strictPort: true,
        allowedHosts: ['rejoicing-phantom-malformed.ngrok-free.dev', 'ngrok-free.dev'],
        proxy: {
          '/api': { ...proxy },
          '/signin-oidc': { ...proxy },
          '/hubs': { ...proxy, ws: true },
        },
      },
    };
  });
}
