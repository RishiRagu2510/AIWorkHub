import { syncPreference } from '../../utils/preferenceSync.js';
export const draftFields = [
  'requestKind',
  ...['firstName','lastName','preferredName','phone','employeeCode','departmentId','careerProfileId','careerLevelId','catalogGradeId','designation','managerEmployeeId','joiningDate','employmentType','employmentStatus','location'].map(name=>`employee.${name}`),
  'onboardingEmail','onboardingReason','onboardingNotes','onboardingAccess','onboardingTeam',
  'labels',
  'fixVersionIds',
  'affectsVersionIds',
  'originalEstimateMinutes',
  'remainingEstimateMinutes',
  'clearParentIssue',
  'clearComponent',
  'clearStoryPoints',
  'clearDueDate',
  'projectId',
  'issueType',
  'summary',
  'description',
  'priority',
  'dueDate',
  'storyPoints',
  'parentIssueId',
  'componentId',
];
export function draftValues(values) {
  return Object.fromEntries(
    draftFields
      .filter((key) => ['string', 'number'].includes(typeof values?.[key]))
      .map((key) => [key, values[key]]),
  );
}
export function readDraft(key) {
  try {
    const saved = JSON.parse(localStorage.getItem(key));
    return saved && saved.values && Date.now() - saved.savedAt < 7 * 86400000
      ? { ...saved, values: draftValues(saved.values) }
      : null;
  } catch {
    return null;
  }
}
export function saveDraft(key, values, version) {
  try {
    const data = { values: draftValues(values), version, savedAt: Date.now() };
    localStorage.setItem(key, JSON.stringify(data));
    syncPreference(key, data);
  } catch {
    /* Storage may be unavailable. */
  }
}
export function removeDraft(key) {
  try {
    localStorage.removeItem(key);
    syncPreference(key, null);
  } catch {
    /* Storage is optional. */
  }
}
