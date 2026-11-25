// Company documents that Admin and HR upload for Athenna to answer from. The server checks everything again (who may manage
// documents, the file type and size, the roles); the checks here only tell the person early.
import { roleGroup } from './athennaStarterPrompts';

export const KNOWLEDGE_MAX_BYTES = 10 * 1024 * 1024;
export const KNOWLEDGE_EXTENSIONS = ['pdf', 'docx', 'txt', 'md'];
export const KNOWLEDGE_ACCEPT = KNOWLEDGE_EXTENSIONS.map((e) => `.${e}`).join(',');

/** Roles a document can be limited to. Admin and HR can always read every document, so they are not listed. */
export const AUDIENCE_ROLES = [
  { code: 'MANAGER', label: 'Managers' },
  { code: 'ENGINEERING_MANAGER', label: 'Engineering managers' },
  { code: 'DEVELOPER', label: 'Developers' },
  { code: 'TALENT_ACQUISITION', label: 'Talent acquisition' },
];

/** Only Admin and HR manage the documents. */
export const canManageKnowledge = (role) => ['admin', 'hr'].includes(roleGroup(role));

const extensionOf = (name) => {
  const dot = String(name || '').lastIndexOf('.');
  return dot < 0 ? '' : String(name).slice(dot + 1).toLowerCase();
};

/** Why this file cannot be uploaded, or null when it can. */
export function checkKnowledgeFile(file) {
  if (!file) return 'Choose a file to upload.';
  if (!KNOWLEDGE_EXTENSIONS.includes(extensionOf(file.name))) return `${file.name}: upload a PDF, Word (.docx), text or Markdown file.`;
  if (!file.size) return `${file.name}: the file is empty.`;
  if (file.size > KNOWLEDGE_MAX_BYTES) return `${file.name}: the file is too large (the limit is 10 MB).`;
  return null;
}

/** "Everyone", or the roles it is limited to, in words. */
export function audienceLabel(doc) {
  const roles = Array.isArray(doc?.roles) ? doc.roles : [];
  if (doc?.everyone || roles.length === 0) return 'Everyone';
  return roles
    .map((code) => AUDIENCE_ROLES.find((r) => r.code === code)?.label || code.replace(/_/g, ' ').toLowerCase().replace(/^./, (c) => c.toUpperCase()))
    .join(', ');
}

/** What to tell the person after an upload, from the server's status. */
export function uploadMessage(status, title) {
  if (status === 'replaced') return `"${title}" was updated.`;
  if (status === 'unchanged') return `"${title}" is already up to date; nothing changed.`;
  return `"${title}" was added. Athenna can answer from it now.`;
}
