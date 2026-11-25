// Saved Athenna chats live on the server (MongoDB), under the signed-in user. This talks to the history endpoints of the
// AI agent service. A chat that an earlier version kept in this browser is moved up once, then removed from the browser.
import { request } from '../services/api/client';
import { clearLocalHistory, loadChats, readConsent } from './athennaChatHistory';

const base = '/api/v1/agent/history';

export async function fetchConsent() {
  const result = await request(`${base}/consent`);
  return result?.consent === 'yes' || result?.consent === 'no' ? result.consent : null;
}

export const putConsent = (value) => request(`${base}/consent`, { method: 'PUT', body: { value } });

/** Newest first, without the messages: { id, title, updatedAt, messageCount }. */
export async function fetchChats() {
  const result = await request(`${base}/chats`);
  return Array.isArray(result?.chats) ? result.chats : [];
}

/** One whole chat: { id, title, updatedAt, messages }. */
export const fetchChat = (id) => request(`${base}/chats/${encodeURIComponent(id)}`);

export const uploadChat = ({ id, title, updatedAt, messages }) =>
  request(`${base}/chats/${encodeURIComponent(id)}`, { method: 'PUT', body: { title, updatedAt, messages } });

export const removeChat = (id) => request(`${base}/chats/${encodeURIComponent(id)}`, { method: 'DELETE' });

export const clearAllChats = () => request(`${base}/chats`, { method: 'DELETE' });

/**
 * Moves what an earlier version stored in this browser to the server. The browser copy is removed only after every chat
 * was accepted, so a failure loses nothing and is retried on the next visit. Returns how many chats were moved.
 */
export async function migrateLocalHistory(userId, storage) {
  const localConsent = readConsent(userId, storage);
  const localChats = loadChats(userId, storage);
  if (!localConsent && localChats.length === 0) return 0;

  if (localConsent && (await fetchConsent()) === null) await putConsent(localConsent);

  let moved = 0;
  for (const chat of [...localChats].reverse()) {
    try {
      await uploadChat(chat);
      moved += 1;
    } catch (error) {
      // A chat the server refuses (too large, say) must not block the others or be lost silently.
      if (error?.status === 400) continue;
      throw error;
    }
  }
  if (moved === localChats.length) clearLocalHistory(userId, storage);
  return moved;
}
