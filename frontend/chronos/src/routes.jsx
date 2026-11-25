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

const Timesheets = safeLazy(() => import('./pages/Timesheets'));
const LogWorkPage = safeLazy(() => import('./pages/LogWorkPage'));
const TimesheetHistory = safeLazy(() => import('./pages/TimesheetHistory'));

export const routes = <>
              <Route path="timesheets" element={<Navigate to="/timesheets/my" replace />} />
              <Route
                path="timesheets/my"
                element={
                  <Access portal="timesheet" permission="TIMESHEET_VIEW">
                    <Timesheets key="my" />
                  </Access>
                }
              />
              <Route
                path="timesheets/log-work"
                element={
                  <Access portal="timesheet" permission="WORKLOG_CREATE">
                    <LogWorkPage />
                  </Access>
                }
              />
              <Route
                path="timesheets/history"
                element={
                  <Access portal="timesheet" permission="TIMESHEET_VIEW">
                    <TimesheetHistory />
                  </Access>
                }
              />
              <Route
                path="timesheets/team"
                element={
                  <Access portal="timesheet" permission="TIMESHEET_VIEW">
                    <Access portal="timesheet" permission="TIMESHEET_TEAM_VIEW">
                      <Timesheets key="team" team />
                    </Access>
                  </Access>
                }
              />

</>;
