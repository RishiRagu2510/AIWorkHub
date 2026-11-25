// Pure helpers for the Athenna chat. Kept free of React so they are easy to test.
import { xlsxBlobFromTable } from './athennaSpreadsheet';

/** Tool results can arrive as an object, a JSON string, or a JSON string wrapped in { Data }. Returns a plain value. */
export function unwrapToolResult(result) {
  let value = result;
  for (let pass = 0; pass < 4; pass += 1) {
    if (typeof value === 'string') {
      const trimmed = value.trim();
      if (!trimmed || !'{["'.includes(trimmed[0])) return value;
      try {
        value = JSON.parse(trimmed);
      } catch {
        return value;
      }
      continue;
    }
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      const keys = Object.keys(value);
      const inner = value.Data ?? value.data;
      // Only the { Success, Data, Error } envelope is unwrapped, never a real record that has a `data` field.
      const isEnvelope = inner !== undefined && keys.every((k) => ['success', 'data', 'error'].includes(k.toLowerCase()));
      if (isEnvelope) {
        value = inner;
        continue;
      }
    }
    return value;
  }
  return value;
}

/** Splits the closing "Suggested actions: a | b | c" line off a reply and returns the chips. */
export function splitSuggestedActions(content) {
  if (!content) return { body: content || '', chips: [] };
  const lines = content.replace(/\s+$/, '').split('\n');
  for (let i = lines.length - 1; i >= 0 && i >= lines.length - 3; i -= 1) {
    const match = lines[i].trim().match(/^[*_\s]*suggested actions?\s*[*_]*\s*:\s*[*_]*\s*(.+)$/i);
    if (!match) continue;
    const chips = match[1]
      .split('|')
      .map((s) => s.replace(/^[\s*_`-]+|[\s*_`]+$/g, ''))
      .filter(Boolean)
      .slice(0, 4);
    return { body: lines.slice(0, i).join('\n').replace(/\s+$/, ''), chips };
  }
  return { body: content, chips: [] };
}

/**
 * Models often put a blank line between table rows, which splits one table into paragraphs.
 * Drop blank lines that sit between two table lines.
 */
export function joinTableRows(text) {
  if (!text || !text.includes('|')) return text;
  const lines = text.split('\n');
  const isPipe = (l) => l.trim().startsWith('|');
  const out = [];
  for (let i = 0; i < lines.length; i += 1) {
    if (lines[i].trim() === '') {
      let next = i + 1;
      while (next < lines.length && lines[next].trim() === '') next += 1;
      const prev = out.length ? out[out.length - 1] : '';
      if (isPipe(prev) && next < lines.length && isPipe(lines[next])) {
        i = next - 1;
        continue;
      }
    }
    out.push(lines[i]);
  }
  return out.join('\n');
}

const isTableRow = (line) => /^\s*\|.*\|\s*$/.test(line);
const isSeparator = (line) => /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/.test(line) && line.includes('-');

const cells = (line) =>
  line
    .trim()
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map((c) => c.trim());

/** Reads a markdown table that starts at lines[start]. Returns null when there is none. */
export function parseMarkdownTable(lines, start) {
  if (start + 1 >= lines.length || !isTableRow(lines[start]) || !isSeparator(lines[start + 1])) return null;
  const headers = cells(lines[start]);
  const rows = [];
  let next = start + 2;
  while (next < lines.length && isTableRow(lines[next])) {
    const row = cells(lines[next]);
    while (row.length < headers.length) row.push('');
    rows.push(row.slice(0, headers.length));
    next += 1;
  }
  return { headers, rows, nextIndex: next };
}

/** Files a tool produced (for example a CSV dossier) so the chat can offer them as downloads. */
export function extractFileDownloads(actions) {
  if (!Array.isArray(actions)) return [];
  const files = [];
  for (const act of actions) {
    if (act?.success === false) continue;
    const parsed = unwrapToolResult(act?.result);
    const file = parsed && typeof parsed === 'object' ? parsed.file : null;
    if (file && typeof file.content === 'string' && file.fileName) {
      files.push({ fileName: String(file.fileName), mimeType: file.mimeType || 'text/plain', content: file.content, format: file.format === 'table' ? 'table' : 'text' });
    }
  }
  return files;
}

/** Knowledge base citations retrieved from company documents via search_company_docs. */
export function extractKnowledgeCitations(actions) {
  if (!Array.isArray(actions)) return [];
  const citations = [];
  for (const act of actions) {
    if (act?.toolName !== 'search_company_docs' || act?.success === false) continue;
    const parsed = unwrapToolResult(act?.result);
    if (parsed && Array.isArray(parsed.passages) && parsed.passages.length > 0) {
      for (const p of parsed.passages) {
        citations.push({
          document: p.document || 'Company Document',
          section: p.section || null,
          updated: p.updated || null,
          text: p.text || '',
        });
      }
    }
  }
  return citations;
}

/** Issue-shaped records in tool results. Leave rows, projects and the like are not issues. */
export function extractIssueRecords(actions) {
  if (!Array.isArray(actions)) return [];
  const isIssue = (x) => x && typeof x === 'object' && (x.issueKey || (x.id && x.summary));
  const found = [];
  for (const act of actions) {
    if (!act || act.success === false) continue;
    const parsed = unwrapToolResult(act.result);
    if (!parsed || typeof parsed !== 'object') continue;
    if (Array.isArray(parsed.items)) found.push(...parsed.items.filter(isIssue));
    else if (isIssue(parsed)) found.push(parsed);
  }
  const seen = new Set();
  return found.filter((item) => {
    const key = item.id || item.issueKey;
    if (!key || seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

/** Statuses the assistant just changed, so cards in the chat and on the board can follow without a refresh. */
export function extractTransitions(actions) {
  if (!Array.isArray(actions)) return [];
  return actions
    .filter((a) => a?.toolName === 'transition_issue' && a.success !== false)
    .map((a) => unwrapToolResult(a.result))
    .filter((r) => r && typeof r === 'object' && r.statusCode && (r.id || r.issueKey))
    .map((r) => ({ id: r.id, issueKey: r.issueKey, statusCode: String(r.statusCode).toUpperCase() }));
}

const TONES = {
  ACTIVE: 'success', ON_LEAVE: 'info', INACTIVE: 'neutral', TERMINATED: 'danger', CANCELLED: 'neutral',
  DONE: 'success', APPROVED: 'success', COMPLETED: 'success', LOW: 'success',
  IN_PROGRESS: 'info', SUBMITTED: 'info', PENDING: 'info', MEDIUM: 'info',
  CODE_REVIEW: 'review', TESTING: 'review',
  HIGH: 'danger', CRITICAL: 'danger', REJECTED: 'danger', OVERDUE: 'danger', BLOCKED: 'danger',
  TODO: 'neutral', DRAFT: 'neutral', NOT_STARTED: 'neutral',
};

/** If `text` is a status or priority code such as IN_PROGRESS, returns how to style and label it. */
export function statusPill(text) {
  if (!/^[A-Z][A-Z0-9_]{1,24}$/.test(text || '')) return null;
  const tone = TONES[text];
  return tone ? { tone, label: text.replace(/_/g, ' ') } : null;
}

/** Saves text as a file in the browser. */
export function downloadTextFile({ fileName, mimeType, content, format }) {
  let blob;
  if (format === 'table') {
    // A table the tool sent is built into a real Excel workbook here, in the browser.
    blob = xlsxBlobFromTable(content);
    if (!blob) return false;
  } else {
    // A byte-order mark makes Excel read CSV as UTF-8, so names with accents stay intact.
    const body = /csv/i.test(mimeType) ? `\uFEFF${content}` : content;
    blob = new Blob([body], { type: `${mimeType};charset=utf-8` });
  }
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
  return true;
}

/** Leave requests waiting for the signed-in manager, from a leave lookup or the approval list. */
export function extractLeaveDecisions(actions) {
  if (!Array.isArray(actions)) return [];
  const found = [];
  for (const act of actions) {
    if (!act || act.success === false) continue;
    const r = unwrapToolResult(act.result);
    if (!r || typeof r !== 'object') continue;
    if (act.toolName === 'check_employee_leaves' && Array.isArray(r.items)) {
      r.items
        .filter((i) => i.canDecide && i.requestId)
        .forEach((i) => found.push({ requestId: i.requestId, employee: i.employeeName, type: i.kind, from: i.startDate, to: i.endDate, days: i.days }));
    }
    if (act.toolName === 'approve_leave_request' && Array.isArray(r.requests)) {
      r.requests
        .filter((i) => i.requestId)
        .forEach((i) => found.push({ requestId: i.requestId, employee: i.employee, type: i.leaveType, from: i.startDate, to: i.endDate, days: i.days, reason: i.reason }));
    }
  }
  const seen = new Set();
  return found.filter((f) => !seen.has(f.requestId) && seen.add(f.requestId));
}

/** Decisions the assistant just carried out, so the buttons on earlier messages can show the outcome. */
export function extractLeaveOutcomes(actions) {
  if (!Array.isArray(actions)) return {};
  const outcomes = {};
  for (const act of actions) {
    if (act?.toolName !== 'approve_leave_request' || act.success === false) continue;
    const r = unwrapToolResult(act.result);
    if (r && r.decided && r.request?.requestId) outcomes[r.request.requestId] = r.decision;
  }
  return outcomes;
}

/** True when an answer asks the user to pick from what it lists (so the numbered options can be clicked). */
export function isChoicePrompt(text) {
  if (!text) return false;
  const lines = text.split('\n').map((l) => l.trim()).filter((l) => l && !/^suggested actions?:/i.test(l));
  const isItem = (l) => /^\d+\.\s/.test(l);
  const first = lines.findIndex(isItem);
  if (first === -1) return false;
  let last = first;
  lines.forEach((l, i) => { if (isItem(l)) last = i; });

  const asks = (l) => /\?\s*$/.test(l) || /\b(choose|select|pick|reply with|which one|by number|let me know which|tell me which)\b/i.test(l);
  // The line that introduces the list, or the line that closes the message.
  const intro = lines.slice(0, first).slice(-1)[0] || '';
  const outro = lines.slice(last + 1).slice(-2).join(' ');
  return asks(intro) || asks(outro);
}

/** The plain text of a numbered option, with a repeated "(NAME)" suffix dropped: "Google04 (GOOGLE04)" -> "Google04". */
export function choiceLabel(raw) {
  const plain = String(raw || '').replace(/\*\*|__|`/g, '').replace(/\s+/g, ' ').trim();
  const m = plain.match(/^(.*?)\s*\(([^()]*)\)\s*$/);
  if (m && m[1].trim().toLowerCase() === m[2].trim().toLowerCase()) return m[1].trim();
  if (m && m[2].trim().toLowerCase().replace(/\s+/g, ' ') === m[1].trim().toLowerCase().replace(/\s+/g, ' ')) return m[1].trim();
  return plain;
}

/** The Log time form the assistant opened with `prepare_work_log`, or null when there is none in these actions. */
export function extractWorkLogForm(actions) {
  if (!Array.isArray(actions)) return null;
  for (const act of actions) {
    if (!act || act.toolName !== 'prepare_work_log' || act.success === false) continue;
    const r = unwrapToolResult(act.result);
    if (!r || typeof r !== 'object' || !r.workLogForm) continue;
    const issues = (Array.isArray(r.issues) ? r.issues : []).filter((i) => i && i.id != null && i.issueKey);
    return { today: r.today || null, selectedIssueId: r.selectedIssueId ?? null, issues, note: r.note || null };
  }
  return null;
}

/** The Update my skills form from a prepare_skills result: the person's skills, the skills they can add, and the profile version. */
export function extractSkillsForm(actions) {
  if (!Array.isArray(actions)) return null;
  for (const act of actions) {
    if (!act || act.toolName !== 'prepare_skills' || act.success === false) continue;
    const r = unwrapToolResult(act.result);
    if (!r || typeof r !== 'object' || r.form !== 'skills' || r.version == null) continue;
    return {
      version: r.version,
      skills: (Array.isArray(r.skills) ? r.skills : [])
        .filter((s) => s && s.id != null && s.skill)
        .map((s) => ({
          id: s.id,
          skillId: s.skillId ?? null,
          skill: s.skill,
          category: s.category || null,
          proficiency: Number(s.proficiency) || 1,
          yearsOfExperience: Number(s.yearsOfExperience) || 0,
          lastUsed: s.lastUsed || null,
        })),
      available: (Array.isArray(r.available) ? r.available : [])
        .filter((a) => a && a.id != null && a.name)
        .map((a) => ({ id: a.id, name: a.name, category: a.category || null })),
    };
  }
  return null;
}

/**
 * The form for one profile tab (personal, experience, education, certifications, languages) from a prepare_profile_section
 * result: what is already on the tab, any details the person had already said, and the profile version to save against.
 */
export function extractProfileSectionForm(actions) {
  if (!Array.isArray(actions)) return null;
  const sections = ['personal', 'experience', 'education', 'certifications', 'languages'];
  for (const act of actions) {
    if (!act || act.toolName !== 'prepare_profile_section' || act.success === false) continue;
    const r = unwrapToolResult(act.result);
    if (!r || typeof r !== 'object' || r.form !== 'profileSection' || r.version == null || !sections.includes(r.section)) continue;
    const prefill = {};
    if (r.prefill && typeof r.prefill === 'object') {
      for (const [key, value] of Object.entries(r.prefill)) if (typeof value === 'string' && value.trim()) prefill[key] = value;
    }
    return {
      section: r.section,
      version: r.version,
      items: (Array.isArray(r.items) ? r.items : []).filter((item) => item && item.id != null),
      values: r.values && typeof r.values === 'object' ? r.values : {},
      prefill,
    };
  }
  return null;
}

/** The Delete form from a prepare_delete result: which kind to open on (or none), and an issue key for comments. */
export function extractDeleteForm(actions) {
  if (!Array.isArray(actions)) return null;
  for (const act of actions) {
    if (!act || act.toolName !== 'prepare_delete' || act.success === false) continue;
    const r = unwrapToolResult(act.result);
    if (!r || typeof r !== 'object' || r.form !== 'delete') continue;
    return { kind: typeof r.kind === 'string' ? r.kind : '', issueKey: typeof r.issueKey === 'string' ? r.issueKey : '' };
  }
  return null;
}

/** The Apply for leave form from a prepare_leave result: the five leave types with the days left, and the policy facts. */
export function extractLeaveForm(actions) {
  if (!Array.isArray(actions)) return null;
  for (const act of actions) {
    if (!act || act.toolName !== 'prepare_leave' || act.success === false) continue;
    const r = unwrapToolResult(act.result);
    if (!r || typeof r !== 'object' || r.form !== 'leave') continue;
    const types = (Array.isArray(r.types) ? r.types : []).filter((t) => t && t.kind && t.label);
    if (types.length === 0) continue;
    return {
      year: Number(r.year) || new Date().getFullYear(),
      policyConfigured: r.policyConfigured !== false,
      manager: r.manager || null,
      selectedKind: r.selectedKind || null,
      holidays: Array.isArray(r.holidays) ? r.holidays.map((h) => String(h).slice(0, 10)) : [],
      types: types.map((t) => ({
        kind: t.kind,
        label: t.label,
        allowance: typeof t.allowance === 'number' ? t.allowance : null,
        used: typeof t.used === 'number' ? t.used : null,
        pending: typeof t.pending === 'number' ? t.pending : null,
        available: typeof t.available === 'number' ? t.available : null,
      })),
    };
  }
  return null;
}

/** The New issue form from a prepare_issue result: the projects to choose from and whatever the user already said. */
export function extractIssueForm(actions) {
  if (!Array.isArray(actions)) return null;
  for (const act of actions) {
    if (!act || act.toolName !== 'prepare_issue' || act.success === false) continue;
    const r = unwrapToolResult(act.result);
    if (!r || typeof r !== 'object' || !r.issueForm) continue;
    const projects = (Array.isArray(r.projects) ? r.projects : []).filter((p) => p && p.id != null && (p.key || p.name));
    return {
      today: r.today || null,
      summary: r.summary || '',
      description: r.description || '',
      issueType: r.issueType || 'TASK',
      priority: r.priority || 'MEDIUM',
      labels: Array.isArray(r.labels) ? r.labels : [],
      projects: projects.map((p) => ({ id: p.id, key: p.key || p.name, name: p.name || p.key })),
      suggestedProjectId: r.suggestedProjectId ?? null,
      note: r.note || null,
    };
  }
  return null;
}

/** "90", "90m", "1.5h", "1h 30m", "1:30" -> minutes (1 to 1440), or null when it cannot be read as a duration. */
export function parseDuration(text) {
  const raw = String(text ?? '').trim().toLowerCase();
  if (!raw) return null;
  let minutes = null;
  let m;
  if ((m = raw.match(/^(\d{1,2}):([0-5]\d)$/))) minutes = Number(m[1]) * 60 + Number(m[2]);
  else if ((m = raw.match(/^(\d+(?:\.\d+)?)\s*(?:h|hrs?|hours?)\s*(\d+)\s*(?:m|mins?|minutes?)?$/))) minutes = Number(m[1]) * 60 + Number(m[2]);
  else if ((m = raw.match(/^(\d+(?:\.\d+)?)\s*(?:h|hrs?|hours?)$/))) minutes = Number(m[1]) * 60;
  else if ((m = raw.match(/^(\d+)\s*(?:m|mins?|minutes?)?$/))) minutes = Number(m[1]);
  if (minutes == null || !Number.isFinite(minutes)) return null;
  minutes = Math.round(minutes);
  return minutes >= 1 && minutes <= 1440 ? minutes : null;
}

/** 90 -> "1h 30m", 120 -> "2h", 45 -> "45m". */
export function formatMinutes(minutes) {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return [h ? `${h}h` : '', m ? `${m}m` : ''].filter(Boolean).join(' ') || '0m';
}

/** Clean up long verbose project prefixes like "FLP360 ONGOING FEATURES AND IMPROVEMENTS-17" -> "FLP360-17" */
export function formatDisplayKey(key) {
  if (!key) return 'ISSUE';
  if (key.includes(' ') || key.length > 20) {
    const match = key.match(/^([A-Z0-9]+)\b.*-(\d+)$/i);
    if (match) return `${match[1]}-${match[2]}`.toUpperCase();
  }
  return key;
}
