import { lazy } from 'react';
import { Route, Navigate } from 'react-router';
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

const Invitations = safeLazy(() => import('./pages/Invitations'));
const MyWork = safeLazy(() => import('./pages/MyWork'));
const ProjectOverview = safeLazy(() => import('./pages/ProjectOverview'));
const Dashboard = safeLazy(() => import('./pages/Dashboard'));
const Accounts = safeLazy(() => import('./pages/Accounts'));
const Projects = safeLazy(() => import('./pages/Projects'));
const Issues = safeLazy(() => import('./pages/Issues'));
const IssueDetails = safeLazy(() => import('./pages/IssueDetails'));
const IssueEditor = safeLazy(() => import('./pages/IssueEditor'));
const AuditLogs = safeLazy(() => import('./pages/AuditLogs'));
const SprintBoard = safeLazy(() => import('./pages/SprintBoard'));

export const routes = <>
              <Route path="boards" element={<Navigate to="/projects" replace />} />
              <Route
                path="my-work"
                element={
                  <Access permission="ISSUE_VIEW">
                    <MyWork />
                  </Access>
                }
              />
              <Route
                path="projects/:projectId/board"
                element={
                  <Access permission="ISSUE_VIEW">
                    <SprintBoard />
                  </Access>
                }
              />
              <Route
                path="projects/:projectId"
                element={
                  <Access permission="PROJECT_VIEW">
                    <ProjectOverview />
                  </Access>
                }
              />
              <Route
                path="accounts"
                element={
                  <Access permission="PROJECT_VIEW">
                    <Accounts />
                  </Access>
                }
              />
              <Route path="dashboard" element={<Dashboard />} />
              <Route
                path="invitations"
                element={
                  <Access permission="USER_VIEW">
                    <Invitations />
                  </Access>
                }
              />
              <Route
                path="projects"
                element={
                  <Access permission="PROJECT_VIEW">
                    <Projects />
                  </Access>
                }
              />
              <Route
                path="issues"
                element={
                  <Access permission="ISSUE_VIEW">
                    <Issues />
                  </Access>
                }
              />
              <Route
                path="issues/create"
                element={
                  <Access permission="ISSUE_CREATE">
                    <IssueEditor />
                  </Access>
                }
              />
              <Route
                path="issues/:issueId"
                element={
                  <Access permission="ISSUE_VIEW">
                    <IssueDetails />
                  </Access>
                }
              />
              <Route
                path="issues/:issueId/edit"
                element={
                  <Access permission="ISSUE_UPDATE">
                    <IssueEditor />
                  </Access>
                }
              />
              <Route
                path="audit"
                element={
                  <Access permission="AUDIT_VIEW">
                    <AuditLogs />
                  </Access>
                }
              />

</>;
