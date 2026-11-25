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

const EmployeeOnboarding = safeLazy(() => import('./pages/EmployeeOnboarding'));
const EmployeeDirectory = safeLazy(() => import('./pages/EmployeeDirectory'));
const EmployeeProfile = safeLazy(() => import('./pages/EmployeeProfile'));
const EmployeeAbsence = safeLazy(() => import('./pages/EmployeeAbsence'));
const EmployeeOrgChart = safeLazy(() => import('./pages/EmployeeOrgChart'));
const EmployeeReports = safeLazy(() => import('./pages/EmployeeReports'));
const EmployeeSetup = safeLazy(() => import('./pages/EmployeeSetup'));

export const routes = <>
  <Route path="employees/onboarding/:issueId" element={<Access portal="employee" permission="ISSUE_VIEW"><EmployeeOnboarding /></Access>} />
  <Route path="employees/onboarding/:issueId/complete" element={<Access portal="employee" permission="EMPLOYEE_CREATE"><EmployeeOnboarding completing /></Access>} />
  <Route path="employees/setup" element={<Access portal="employee" permission="EMPLOYEE_CREATE"><EmployeeSetup /></Access>} />
  <Route path="employees/reports" element={<Access portal="employee" permission="EMPLOYEE_VIEW"><EmployeeReports /></Access>} />
              <Route path="employees/leave" element={<Access portal="employee" permission="EMPLOYEE_VIEW"><EmployeeAbsence /></Access>} />
              <Route
                path="employees/admin"
                element={
                  <Access portal="employee" permission="EMPLOYEE_VIEW">
                    <EmployeeDirectory management />
                  </Access>
                }
              />
              <Route
                path="employees/admin/:id"
                element={
                  <Access portal="employee" permission="EMPLOYEE_VIEW">
                    <EmployeeProfile management />
                  </Access>
                }
              />
              <Route
                path="employees"
                element={
                  <Access portal="employee" permission="EMPLOYEE_VIEW">
                    <EmployeeDirectory />
                  </Access>
                }
              />
              <Route
                path="profile"
                element={
                  <Access portal="employee" permission="EMPLOYEE_VIEW">
                    <EmployeeProfile key="profile" self />
                  </Access>
                }
              />
              <Route
                path="employees/me"
                element={
                  <Access portal="employee" permission="EMPLOYEE_VIEW">
                    <EmployeeProfile key="me" self />
                  </Access>
                }
              />
              <Route
                path="employees/skills"
                element={
                  <Access portal="employee" permission="EMPLOYEE_VIEW">
                    <EmployeeProfile key="skills" self skills />
                  </Access>
                }
              />
              <Route
                path="employees/org-chart"
                element={
                  <Access portal="employee" permission="EMPLOYEE_VIEW">
                    <EmployeeOrgChart />
                  </Access>
                }
              />
              <Route
                path="employees/:id"
                element={
                  <Access portal="employee" permission="EMPLOYEE_VIEW">
                    <EmployeeProfile />
                  </Access>
                }
              />

</>;
