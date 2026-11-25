import { apiBase, getAccessToken, request } from './client';

const streamPath = '/api/v1/agent/chat/stream';
const plainPath = '/api/v1/agent/chat';

/** The stream could not be opened at all (an older server, a proxy that refuses it, an expired sign-in), so nothing was done yet. */
export class StreamUnavailable extends Error {
  constructor(status) {
    super('The reply could not be streamed.');
    this.status = status;
  }
}

/**
 * Turns pieces of a server-sent event stream into events. Pieces can end anywhere, so it keeps what is unfinished.
 * Comment lines (keep-alives) are ignored. `data` is parsed as JSON when it is JSON.
 */
export function createEventParser(onEvent) {
  let pending = '';
  const dispatch = (frame) => {
    let event = 'message';
    const data = [];
    for (const line of frame.split('\n')) {
      if (!line || line.startsWith(':')) continue;
      const colon = line.indexOf(':');
      const field = colon === -1 ? line : line.slice(0, colon);
      let value = colon === -1 ? '' : line.slice(colon + 1);
      if (value.startsWith(' ')) value = value.slice(1);
      if (field === 'event') event = value;
      else if (field === 'data') data.push(value);
    }
    if (data.length === 0) return;
    const text = data.join('\n');
    let parsed = text;
    try {
      parsed = JSON.parse(text);
    } catch {
      /* plain text data is passed on as it is */
    }
    onEvent({ event, data: parsed });
  };

  return {
    push(chunk) {
      pending += chunk.replace(/\r\n?/g, '\n');
      let boundary = pending.indexOf('\n\n');
      while (boundary !== -1) {
        dispatch(pending.slice(0, boundary));
        pending = pending.slice(boundary + 2);
        boundary = pending.indexOf('\n\n');
      }
    },
    end() {
      if (pending.trim()) dispatch(pending);
      pending = '';
    },
  };
}

// Progress is handed to whoever started the chat by a key, because the mutation's arguments must stay plain data.
const listeners = new Map();

export function listenToChat(key, listener) {
  listeners.set(key, listener);
  return () => {
    if (listeners.get(key) === listener) listeners.delete(key);
  };
}

const announce = (key, event) => {
  if (key) listeners.get(key)?.(event);
};

function streamHeaders() {
  const token = getAccessToken();
  const ngrok =
    apiBase.includes('ngrok') || (typeof window !== 'undefined' && window.location?.hostname?.includes('ngrok'));
  return {
    Accept: 'text/event-stream',
    'Content-Type': 'application/json',
    ...(ngrok ? { 'ngrok-skip-browser-warning': 'true' } : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

/** Sends the chat and reads the reply as it is written. Resolves to the complete reply, as the plain endpoint would. */
export async function streamChat(body, { signal, onEvent } = {}) {
  let response;
  try {
    response = await fetch(`${apiBase}${streamPath}`, {
      method: 'POST',
      signal,
      credentials: 'include',
      redirect: 'manual',
      headers: streamHeaders(),
      body: JSON.stringify(body),
    });
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw new StreamUnavailable('NETWORK');
  }

  if (!response.ok || !response.body || !response.headers.get('content-type')?.includes('text/event-stream')) {
    throw new StreamUnavailable(response.status);
  }

  let done = null;
  let failure = null;
  const parser = createEventParser((evt) => {
    onEvent?.(evt);
    if (evt.event === 'done') done = evt.data;
    if (evt.event === 'error') failure = evt.data?.message || 'Athenna could not finish this request. Please try again.';
  });

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  try {
    for (;;) {
      const { value, done: finished } = await reader.read();
      if (finished) break;
      parser.push(decoder.decode(value, { stream: true }));
    }
    parser.push(decoder.decode());
    parser.end();
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw { status: 'NETWORK', message: 'The connection dropped before Athenna finished. Please try again.' };
  }

  if (failure) throw { status: 'STREAM', message: failure };
  if (!done) throw { status: 'NETWORK', message: 'The connection closed before Athenna finished. Please try again.' };
  return done;
}

/**
 * Chats with streaming, and falls back to the plain request when the stream cannot be opened. It never falls back after the
 * stream has opened, because by then Athenna may already have acted, and asking again could do it twice.
 */
export async function chatWithStreaming({ progressKey, ...body }, { signal } = {}) {
  try {
    return await streamChat(body, { signal, onEvent: (evt) => announce(progressKey, evt) });
  } catch (error) {
    if (!(error instanceof StreamUnavailable)) throw error;
    return request(plainPath, { method: 'POST', body, signal });
  }
}
