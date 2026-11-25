import { versionHeader, versionMessages } from './version.js';
// Browser auth/API traffic defaults to VITE_API_BASE_URL (or current origin if empty).
export const apiBase = (import.meta?.env?.VITE_API_BASE_URL || '').replace(/\/$/, '');
export const loginUrl = `${apiBase}/api/auth/login`;
let accessToken = null;
const retryKeys = new Map();
let expired = () => {};
export function getAccessToken() {
  return accessToken || '';
}
export function setSessionToken(token) {
  accessToken = token;
  if (!token) retryKeys.clear();
}
export function onSessionExpired(callback) {
  expired = callback;
}
export const messages = {
  400: 'Please check the supplied values and try again.',
  401: 'Your session has expired. Please sign in again.',
  403: 'You do not have permission to perform this action.',
  404: 'The requested resource was not found.',
  409: 'This change conflicts with the current data. Refresh and try again.',
  412: versionMessages.STALE_VERSION,
  428: versionMessages.VERSION_REQUIRED,
  422: 'Please check the supplied values and try again.',
  429: 'Too many requests. Please wait a moment and try again.',
};
export function queryString(params = {}) {
  return new URLSearchParams(
    Object.entries(params).filter(([, value]) => value !== '' && value != null),
  ).toString();
}
export async function request(
  path,
  { method = 'GET', body, params, signal, version, responseType, session = false } = {},
) {
  const requiresVersion =
    ['PUT', 'DELETE'].includes(method) &&
    /^\/api\/(?:projects|issues)\/[^/]+(?:\/(?:parent|assignee|transition|comments\/[^/]+))?$/.test(
      path,
    );
  const timeVersion =
    (['PUT', 'DELETE'].includes(method) && /^\/api\/worklogs\/[^/]+$/.test(path)) ||
    (method === 'POST' && /^\/api\/timesheets\/(submit|approve|reject)$/.test(path));
  const precondition =
    Boolean(
      version &&
      (/^\/api\/employees\//.test(path) ||
        /^\/api\/sprints\//.test(path) ||
        /\/placement$/.test(path) ||
        /\/api\/projects\/[^/]+\/(components|versions)\/[^/]+$/.test(path)),
    ) ||
    timeVersion ||
    requiresVersion ||
    (['PUT', 'DELETE'].includes(method) && /^\/api\/projects\/accounts\/[^/]+$/.test(path))
      ? { 'If-Match': versionHeader(version) }
      : {};
  const retryable =
    ['POST', 'PUT'].includes(method) &&
    /^\/api\/issues(?:\/[^/]+(?:\/(?:comments|assignee|transition))?)?$/.test(path);
  const timeRetry =
    ['POST', 'PUT'].includes(method) &&
    /^\/api\/(worklogs(?:\/[^/]+)?|timesheets\/(submit|approve|reject))$/.test(path);
  const fingerprint = retryable || timeRetry ? JSON.stringify([method, path, body, version]) : null;
  if (fingerprint && !retryKeys.has(fingerprint)) {
    if (retryKeys.size >= 100) retryKeys.delete(retryKeys.keys().next().value);
    retryKeys.set(fingerprint, crypto.randomUUID());
  }
  let response;
  try {
    response = await fetch(`${apiBase}${path}${params ? `?${queryString(params)}` : ''}`, {
      method,
      signal,
      credentials: 'include',
      redirect: 'manual',
      headers: {
        Accept: 'application/json',
        ...(apiBase.includes('ngrok') ||
        (typeof window !== 'undefined' && window.location?.hostname?.includes('ngrok'))
          ? { 'ngrok-skip-browser-warning': 'true' }
          : {}),
        ...precondition,
        ...(fingerprint ? { 'Idempotency-Key': retryKeys.get(fingerprint) } : {}),
        ...(body !== undefined && !(body instanceof FormData)
          ? { 'Content-Type': 'application/json' }
          : {}),
        ...(!session && accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      ...(body !== undefined
        ? { body: body instanceof FormData ? body : JSON.stringify(body) }
        : {}),
    });
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw { status: 'NETWORK', message: 'Unable to connect. Check your connection and try again.' };
  }
  if (response.type === 'opaqueredirect' || (response.status >= 300 && response.status < 400)) {
    if (!session) expired();
    throw { status: 401, message: messages[401] };
  }
  if (!response.ok) {
    if (response.status < 500 && fingerprint) retryKeys.delete(fingerprint);
    if (response.status === 401 && !session) expired();
    // Only recognize documented codes; never expose arbitrary exception bodies.
    let code;
    let absenceMessage;
    let employeeMessage;
    let workflowMessage;
    let knowledgeMessage;
    let planMessage;
    try {
      const payload = await response.json();
      const expected = { 428: 'VERSION_REQUIRED', 412: 'STALE_VERSION', 400: 'INVALID_VERSION' }[
        response.status
      ];
      if (path.startsWith('/api/employees/setup') && response.status === 400 && ['LAST_ADMIN_REQUIRED','SETUP_DEPARTMENT_IN_USE','SETUP_TITLE_IN_USE','SETUP_ROLE_IN_USE','SETUP_ADMIN_REQUIRED'].includes(payload.code)) code = payload.code;
      if (expected && payload.code === expected) code = expected;
      // Workflow refusals are written for people ("not allowed by the project workflow"), so show them as they are.
      if (/^\/api\/issues\/[^/]+\/transition$/.test(path) && [400, 409].includes(response.status)) {
        const text = [payload.error, payload.message, payload.detail, payload.title].find((t) => typeof t === 'string');
        if (text && /^(Confirm skipping workflow|Unknown issue status|This status transition is not allowed)/.test(text)) workflowMessage = text;
      }
      // The company-documents endpoints write their refusals for people ("That file type can't be used here"), so show them as they are.
      if (path.startsWith('/api/v1/agent/knowledge') && [400, 403, 409, 503].includes(response.status) && typeof payload.error === 'string') knowledgeMessage = payload.error;
      // Plan requests are refused in plain words ("Your plan already includes this."), so show them as they are.
      if (path.startsWith('/api/tenants/current/plan') && [400, 409].includes(response.status) && typeof payload.error === 'string') {
        planMessage = payload.error;
        if (payload.code === 'ALREADY_INCLUDED') code = 'ALREADY_INCLUDED';
      }
      if (path.startsWith('/api/employees/absence') && [400, 409].includes(response.status) && ['ABSENCE_VALIDATION', 'ABSENCE_CONFLICT'].includes(payload.code) && typeof payload.title === 'string') absenceMessage = payload.title;
      if ((path.startsWith('/api/employees') || path === '/api/issues/employee-requests' || path.includes('/employee-request/')) && [400, 404, 409, 422].includes(response.status)) {
        if (payload.errors && typeof payload.errors === 'object') {
          const flat = Object.values(payload.errors).flat().filter((x) => typeof x === 'string' && x.length > 0);
          if (flat.length > 0) employeeMessage = flat.join(' ');
        }
        if (!employeeMessage && typeof payload.detail === 'string' && payload.detail.length > 0) employeeMessage = payload.detail;
        if (!employeeMessage && typeof payload.title === 'string' && payload.title.length > 0 && payload.title !== 'One or more validation errors occurred.') employeeMessage = payload.title;
        if (!employeeMessage && typeof payload.message === 'string' && payload.message.length > 0) employeeMessage = payload.message;
      }
      if (path.startsWith('/api/employees') && response.status === 409 && ['EMPLOYEE_ALREADY_EXISTS', 'EMPLOYEE_CODE_EXISTS'].includes(payload.code)) code = payload.code;
    } catch {
      /* A non-JSON error still gets safe status-based text. */
    }
    throw {
      status: response.status,
      ...(code ? { code } : {}),
      message:
        employeeMessage ||
        knowledgeMessage ||
        planMessage ||
        absenceMessage ||
        workflowMessage ||
        ({SETUP_DEPARTMENT_IN_USE: 'This department is linked to employees or job titles. Reassign employees and delete its title mappings first.', SETUP_TITLE_IN_USE: 'This title is linked to employees or skill requirements. Remove those links first.', SETUP_ROLE_IN_USE: 'This role is assigned to users. Remove their role assignments first.', SETUP_ADMIN_REQUIRED: 'The Administrator role cannot be deleted.'}[code]) ||
        (code === 'LAST_ADMIN_REQUIRED' ? 'Keep at least one active administrator. Assign another admin before removing this role.' : null) ||
        (code === 'EMPLOYEE_CODE_EXISTS' ? 'This employee code is already assigned to another profile. Enter the employee’s correct, unique code.' : null) ||
        (code === 'EMPLOYEE_ALREADY_EXISTS' ? 'This login account already has an employee profile. Open Employee management and edit the existing profile.' : null) ||
        versionMessages[code] ||
        messages[response.status] ||
        'The service could not complete your request. Please try again.',
    };
  }
  if (response.status === 204) return null;
  if (responseType === 'blob') return response.blob();
  if (!response.headers.get('content-type')?.includes('application/json')) {
    throw {
      status: 'FORMAT',
      message: 'The service returned an unexpected response. Please try signing in again.',
    };
  }
  const result = await response.json();
  if (fingerprint) retryKeys.delete(fingerprint);
  return result;
}
