// Files attached in the Athenna chat. They are uploaded to the AI agent service, which keeps them in MinIO under the
// signed-in user; only the id, name, type and size travel with the message. The server checks everything again, so the
// checks here are only to tell the person early.
import { request } from '../services/api/client';

const base = '/api/v1/agent/attachments';

export const MAX_FILES = 5;
export const MAX_BYTES = 10 * 1024 * 1024;

const TYPES = {
  png: 'image/png',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  gif: 'image/gif',
  webp: 'image/webp',
  pdf: 'application/pdf',
  txt: 'text/plain',
  csv: 'text/csv',
  md: 'text/markdown',
  json: 'application/json',
  doc: 'application/msword',
  docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  xls: 'application/vnd.ms-excel',
  xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  ppt: 'application/vnd.ms-powerpoint',
  pptx: 'application/vnd.openxmlformats-officedocument.presentationml.presentation',
};

/** For the file picker's accept attribute. */
export const ACCEPT = Object.keys(TYPES).map((ext) => `.${ext}`).join(',');

const extensionOf = (name) => {
  const dot = String(name || '').lastIndexOf('.');
  return dot < 0 ? '' : String(name).slice(dot + 1).toLowerCase();
};

export const isImageFile = (file) => /^image\//.test(file?.contentType || TYPES[extensionOf(file?.name)] || '');

/** Why this file cannot be attached, or null when it can. */
export function checkFile(file) {
  if (!file) return 'No file was chosen.';
  if (!TYPES[extensionOf(file.name)]) return `${file.name}: this type of file is not allowed.`;
  if (!file.size) return `${file.name}: the file is empty.`;
  if (file.size > MAX_BYTES) return `${file.name}: the file is too large (the limit is ${formatSize(MAX_BYTES)}).`;
  return null;
}

export function formatSize(bytes) {
  const n = Number(bytes) || 0;
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${Math.round(n / 1024)} KB`;
  return `${(n / (1024 * 1024)).toFixed(1).replace(/\.0$/, '')} MB`;
}

/** Uploads one file. Resolves to { id, name, contentType, size }. */
export function uploadAttachment(file) {
  const body = new FormData();
  body.append('file', file, file.name);
  return request(base, { method: 'POST', body });
}

export const deleteAttachment = (id) => request(`${base}/${encodeURIComponent(id)}`, { method: 'DELETE' });

/** The file's bytes as a Blob, fetched with the user's own token. */
export const fetchAttachmentBlob = (id) => request(`${base}/${encodeURIComponent(id)}`, { responseType: 'blob' });

/** What is kept on a message: no more than the id, name, type and size. */
export const toStored = (a) => ({ id: a.id, name: a.name, contentType: a.contentType, size: a.size });

/**
 * What the assistant is told when files come with a message. The files themselves travel as ids (see
 * `attachmentRefs`) and the server reads them, so this only names them and, when the person typed nothing,
 * says what to do.
 */
export function withAttachmentNote(text, attachments) {
  if (!attachments?.length) return text;
  const note = `[Attached ${attachments.length === 1 ? 'file' : 'files'}: ${attachments.map((a) => a.name).join(', ')}]`;
  return text
    ? `${text}\n\n${note}`
    : `${note}\nI sent no question with it. Summarise what ${attachments.length === 1 ? 'it contains' : 'they contain'} in a few lines, then ask what I would like to do with ${attachments.length === 1 ? 'it' : 'them'}.`;
}

/** The ids the server needs to read the files. Names and types are not sent: the server looks them up itself. */
export const attachmentRefs = (attachments) => (attachments || []).filter((a) => a?.id).map((a) => ({ id: a.id }));

/** Saves a fetched file to disk under its own name. */
export async function downloadAttachment(attachment) {
  const blob = await fetchAttachmentBlob(attachment.id);
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = attachment.name;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
