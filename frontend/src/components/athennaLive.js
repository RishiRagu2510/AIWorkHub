// What the chat shows while Athenna is still working. Pure, so it is easy to test.
import { splitSuggestedActions } from './athennaChatUtils';

/** What the person sees while Athenna is working: a line about the step, and the answer so far. */
export const emptyLive = { status: '', text: '' };

export function liveReply(live, { event, data }) {
  switch (event) {
    case 'status':
      return { ...live, status: data?.text || '' };
    case 'delta':
      return { ...live, text: live.text + (data?.text || '') };
    case 'reset':
      return { ...live, text: '' };
    default:
      return live;
  }
}

const OPENING = 'suggested actions:';

/**
 * The answer so far, ready to show. The "Suggested actions:" line the model ends with becomes buttons once the reply is
 * whole, so it is hidden here, including while it is only half typed.
 */
export function liveDraft(text) {
  if (!text) return '';
  const { body } = splitSuggestedActions(text);
  const lines = body.split('\n');
  const last = lines[lines.length - 1].trim().replace(/^[*_\s]+/, '').toLowerCase();
  if (last.length >= 3 && (OPENING.startsWith(last) || last.startsWith(OPENING.slice(0, -1)))) lines.pop();
  return lines.join('\n').replace(/\s+$/, '');
}
