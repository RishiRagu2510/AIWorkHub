import { lazy } from 'react';
import { Route } from 'react-router';
import { Access } from '../../src/App';

function safeLazy(importFn) {
  return lazy(async () => {
    try {
      const mod = await importFn();
      sessionStorage.removeItem('chunk_reload_' + window.location.pathname);
      return mod;
    } catch (err) {
      const isChunkError =
        err?.name === 'TypeError' ||
        err?.message?.includes('Failed to fetch dynamically imported module') ||
        err?.message?.includes('Importing a module script failed');
      const key = 'chunk_reload_' + window.location.pathname;
      if (isChunkError && !sessionStorage.getItem(key)) {
        sessionStorage.setItem(key, 'true');
        window.location.reload();
        return new Promise(() => {});
      }
      sessionStorage.removeItem(key);
      throw err;
    }
  });
}

const HubHome = safeLazy(() => import('./pages/HubHome'));
const KnowledgePage = safeLazy(() => import('./pages/KnowledgePage'));

export const routes = (
  <>
    <Route path="home" element={<Access portal="hub"><HubHome /></Access>} />
    <Route path="knowledge" element={<Access portal="hub"><KnowledgePage /></Access>} />
  </>
);
