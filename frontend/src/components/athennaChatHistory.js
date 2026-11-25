// Saved Athenna chats. Nothing is stored unless the user said yes, and it only ever lives in this browser,
// under their own user id, so another account on the same machine does not see it.

export const MAX_CHATS = 30;
export const MAX_MESSAGES = 80;
// Tool results (tables, file contents) can be large; a message whose actions exceed this keeps its text only.
export const MAX_ACTIONS_CHARS = 20000;

export const consentKey = (userId) => `athenna_history_consent_${userId ?? 'anonymous'}`;
export const chatsKey = (userId) => `athenna_history_chats_${userId ?? 'anonymous'}`;

function defaultStorage() {
  try {
    if (typeof window !== 'undefined' && window.localStorage && typeof window.localStorage.getItem === 'function') {
      return window.localStorage;
    }
  } catch {
    // Storage can be blocked by the browser; history simply stays off.
  }
  return null;
}

/** 'yes', 'no', or null when the user has not been asked yet. */
export function readConsent(userId, storage = defaultStorage()) {
  const value = storage?.getItem(consentKey(userId));
  return value === 'yes' || value === 'no' ? value : null;
}

export function writeConsent(userId, value, storage = defaultStorage()) {
  if (!storage) return;
  storage.setItem(consentKey(userId), value === 'yes' ? 'yes' : 'no');
}

export function loadChats(userId, storage = defaultStorage()) {
  try {
    const parsed = JSON.parse(storage?.getItem(chatsKey(userId)) || '[]');
    if (!Array.isArray(parsed)) return [];
    return parsed
      .filter((c) => c && typeof c.id === 'string' && Array.isArray(c.messages))
      .sort((a, b) => (b.updatedAt || 0) - (a.updatedAt || 0));
  } catch {
    return [];
  }
}

function persist(userId, chats, storage) {
  try {
    storage?.setItem(chatsKey(userId), JSON.stringify(chats));
  } catch {
    // Storage full: drop the oldest chat and try once more.
    if (chats.length > 1) persist(userId, chats.slice(0, -1), storage);
  }
}

/** Adds or updates one chat, keeps the newest MAX_CHATS, and returns the new list. */
export function saveChat(userId, chat, storage = defaultStorage()) {
  if (!storage || !chat?.id || !Array.isArray(chat.messages) || chat.messages.length === 0) return loadChats(userId, storage);
  const others = loadChats(userId, storage).filter((c) => c.id !== chat.id);
  const next = [chat, ...others].sort((a, b) => (b.updatedAt || 0) - (a.updatedAt || 0)).slice(0, MAX_CHATS);
  persist(userId, next, storage);
  return next;
}

export function deleteChat(userId, id, storage = defaultStorage()) {
  const next = loadChats(userId, storage).filter((c) => c.id !== id);
  persist(userId, next, storage);
  return next;
}

/** Removes this browser's copy of the user's history, once it has been moved to the server. */
export function clearLocalHistory(userId, storage = defaultStorage()) {
  storage?.removeItem(chatsKey(userId));
  storage?.removeItem(consentKey(userId));
}

export function clearChats(userId, storage = defaultStorage()) {
  storage?.removeItem(chatsKey(userId));
  return [];
}

/** The first thing the user asked, shortened, as the chat's name. */
export function chatTitle(messages) {
  const first = (messages || []).find((m) => m.role === 'user' && (m.content?.trim() || m.attachments?.length));
  if (!first) return 'New chat';
  const text = (first.content?.trim() || first.attachments[0].name).replace(/\s+/g, ' ');
  return text.length > 60 ? `${text.slice(0, 57)}...` : text;
}

/** What gets stored: recent messages, without screen-only flags, and without oversized tool payloads. */
export function compactMessages(messages) {
  return (messages || []).slice(-MAX_MESSAGES).map(({ restored: _restored, ...m }) => {
    if (m.actions) {
      let size = 0;
      try {
        size = JSON.stringify(m.actions).length;
      } catch {
        size = Infinity;
      }
      if (size > MAX_ACTIONS_CHARS) {
        const { actions: _actions, ...rest } = m;
        return rest;
      }
    }
    return m;
  });
}

/** "Today", "Yesterday" or a short date, for grouping the list. */
export function dayLabel(timestamp, now = new Date()) {
  const day = (d) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
  const diff = Math.round((day(now) - day(new Date(timestamp))) / 86400000);
  if (diff <= 0) return 'Today';
  if (diff === 1) return 'Yesterday';
  return new Date(timestamp).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}
