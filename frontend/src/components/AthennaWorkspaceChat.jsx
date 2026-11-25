import React, { useState, useRef, useEffect, useMemo } from 'react';
import {
  useChatWithAthennaMutation,
  useClearAgentMemoryMutation,
  useDeleteAgentMemoryMutation,
  useGetAgentMemoryQuery,
  useGetAgentKnowledgeQuery,
  useUploadAgentKnowledgeMutation,
  useDeleteAgentKnowledgeMutation,
  useGetAgentModelsQuery,
  useGetAgentNudgesQuery,
} from '../services/api/agent';
import { listenToChat } from '../services/api/athennaStream';
import { emptyLive, liveDraft, liveReply } from './athennaLive';
import { api, useSearchIssuesQuery, useTransitionIssueMutation } from '../services/api/api';
import { useWorkLogsQuery } from '../services/api/timesheets';
import { useAuth } from '../features/auth/context';
import { liveQueryOptions } from '../services/api/live';
import { Icon } from './Icon';
import Link from './PortalLink';
import {
  choiceLabel,
  downloadTextFile,
  extractFileDownloads,
  extractKnowledgeCitations,
  extractIssueRecords,
  extractLeaveDecisions,
  extractLeaveForm,
  extractSkillsForm,
  extractProfileSectionForm,
  extractDeleteForm,
  extractWorkLogForm,
  extractIssueForm,
  extractLeaveOutcomes,
  extractTransitions,
  isChoicePrompt,
  joinTableRows,
  parseMarkdownTable,
  splitSuggestedActions,
  statusPill,
} from './athennaChatUtils';
import LeaveDecisionCards from './LeaveDecisionCards';
import LeaveApplyCard from './LeaveApplyCard';
import SkillsEditCard from './SkillsEditCard';
import ProfileSectionCard from './ProfileSectionCard';
import DeleteCard from './DeleteCard';
import WorkLogCard from './WorkLogCard';
import IssueCreateCard from './IssueCreateCard';
import ChatHistoryCard from './ChatHistoryCard';
import { ComposerAttachments, MessageAttachments } from './ChatAttachments';
import ChatQueue from './ChatQueue';
import {
  ACCEPT,
  MAX_FILES,
  checkFile,
  deleteAttachment,
  toStored,
  uploadAttachment,
  withAttachmentNote,
  attachmentRefs,
} from './athennaAttachments';
import ModelPicker from './ModelPicker';
import ChatIssueCard from './ChatIssueCard';
import { recordModelUsage, usageStatus } from './athennaModelUsage';
import { MAX_CHATS, chatTitle, compactMessages } from './athennaChatHistory';
import {
  clearAllChats,
  fetchChat,
  fetchChats,
  fetchConsent,
  migrateLocalHistory,
  putConsent,
  removeChat,
  uploadChat,
} from './athennaChatHistoryApi';
import { starterPromptsFor } from './athennaStarterPrompts';
import AthennaNudges from './AthennaNudges';
import AthennaMemory from './AthennaMemory';
import AthennaKnowledge from './AthennaKnowledge';
import { canManageKnowledge } from './knowledgeUploads';
import './AthennaWorkspaceChat.css';

// "-latest" aliases always point at Google's current model, so they keep working when a version is retired
// (pinned ids such as gemini-1.5-* and gemini-2.5-flash now answer 404).
const DEFAULT_MODEL = 'gemini-flash-latest';

const AVAILABLE_MODELS = [
  // The server decides per message: Flash-Lite for a short, plain lookup, Flash for everything else.
  {
    id: 'auto',
    name: 'Auto',
    provider: 'Google',
    badge: 'Smart Routing',
    desc: 'Fast model for simple lookups, the standard model for everything else',
    icon: '🧭',
  },
  {
    id: 'gemini-pro-latest',
    name: 'Gemini Pro',
    provider: 'Google',
    badge: 'Deep Reasoning',
    desc: 'Complex multi-tool ReAct workflows & long context',
    icon: '✨',
  },
  {
    id: 'gemini-flash-latest',
    name: 'Gemini Flash',
    provider: 'Google',
    badge: 'Fast & Capable',
    desc: 'Quick, reliable tool use for everyday workspace tasks',
    icon: '⚡',
  },
  {
    id: 'gemini-flash-lite-latest',
    name: 'Gemini Flash-Lite',
    provider: 'Google',
    badge: 'Fastest',
    desc: 'Lowest latency for simple lookups & short answers',
    icon: '🚀',
  },
  // OpenAI models are answered by the server when OPENAI_API_KEY is set there; if not, Gemini answers and the chat says so.
  {
    id: 'gpt-4.1',
    name: 'GPT-4.1',
    provider: 'OpenAI',
    badge: 'Reliable Tools',
    desc: 'Strong multi-step tool use and instruction following',
    icon: '🧠',
  },
  {
    id: 'gpt-4.1-mini',
    name: 'GPT-4.1 mini',
    provider: 'OpenAI',
    badge: 'Fast',
    desc: 'Quicker, lighter OpenAI model for everyday tasks',
    icon: '💨',
  },
  // Groq serves open models quickly through the server's Groq key; if it cannot answer, Gemini does and the chat says so.
  {
    id: 'openai/gpt-oss-120b',
    name: 'GPT-OSS 120B',
    provider: 'Groq',
    badge: 'Open Model',
    desc: 'Large open model on Groq with solid multi-step tool use',
    icon: '⚡',
  },
  {
    id: 'openai/gpt-oss-20b',
    name: 'GPT-OSS 20B',
    provider: 'Groq',
    badge: 'Very Fast',
    desc: 'Small open model on Groq for quick lookups',
    icon: '🏎️',
  },
];

function getSavedModel() {
  try {
    if (typeof window !== 'undefined' && window.localStorage && typeof window.localStorage.getItem === 'function') {
      const saved = window.localStorage.getItem('aiworkhub_model');
      if (AVAILABLE_MODELS.some((m) => m.id === saved)) {
        return saved;
      }
    }
  } catch {}
  return DEFAULT_MODEL;
}

function saveModel(modelId) {
  try {
    if (typeof window !== 'undefined' && window.localStorage && typeof window.localStorage.setItem === 'function') {
      window.localStorage.setItem('aiworkhub_model', modelId);
    }
  } catch {}
}

// Employee codes such as EMP-001 look like issue keys but are not tickets.
const isIssueKey = (k) => /^[A-Za-z][A-Za-z0-9]*-\d+$/.test(k) && !/^EMP(LOYEE)?-/i.test(k);

function formatDisplayKey(key) {
  if (!key) return 'ISSUE';
  // Clean up long verbose project prefixes like "FLP360 ONGOING FEATURES AND IMPROVEMENTS-17" -> "FLP360-17"
  if (key.includes(' ') || key.length > 20) {
    const match = key.match(/^([A-Z0-9]+)\b.*-(\d+)$/i);
    if (match) return `${match[1]}-${match[2]}`.toUpperCase();
  }
  return key;
}

function extractActionIssues(actions) {
  return extractIssueRecords(actions);
}

function getInitials(name) {
  if (!name) return 'U';
  const parenMatch = name.match(/\(([^)]+)\)/);
  const target = parenMatch ? parenMatch[1] : name;
  const parts = target.trim().split(/\s+/).filter(Boolean);
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }
  return target.substring(0, 2).toUpperCase();
}

function getSectionMeta(title) {
  const t = title.toLowerCase();
  if (t.includes('overview') || t.includes('detail')) return { icon: '📋', label: title };
  if (t.includes('telemetry') || t.includes('sprint') || t.includes('metric')) return { icon: '📊', label: title };
  if (t.includes('scope') || t.includes('description') || t.includes('finding')) return { icon: '🔍', label: title };
  if (t.includes('next step') || t.includes('recommend') || t.includes('action')) return { icon: '🛠️', label: title };
  return { icon: '📌', label: title };
}

function extractKv(text) {
  if (!text) return null;
  const m = text.match(/^(\*{0,2})([A-Za-z\s]+?)\1\s*:\s*(.+)$/);
  if (!m) return null;
  const label = m[2].trim();
  const rawVal = m[3].trim().replace(/^`+|`+$/g, '');
  const lower = label.toLowerCase();
  const known = [
    'summary', 'project', 'issue type', 'type', 'status', 'priority',
    'assignee', 'reporter', 'created at', 'created', 'due date', 'target due date',
    'time logged', 'effort', 'remaining', 'story points', 'component'
  ];
  if (known.includes(lower) || (label.length <= 25 && !label.includes('.'))) {
    return { label, value: rawVal, rawText: text };
  }
  return null;
}

// Exported for tests. Components and helpers share this file, so hot reload falls back to a full refresh here.
export function renderFormattedContent(text, onActionClick) {
  if (!text) return null;

  // Clean verbose ticket keys in text (e.g. FLP360 ONGOING FEATURES AND IMPROVEMENTS-43 -> FLP360-43)
  const cleanedText = joinTableRows(text).replace(/([A-Z0-9]+)\b[A-Za-z0-9\s]+-(\d+)/g, (full, p1, p2) => {
    if (full.includes(' ') || full.length > 20) {
      return `${p1}-${p2}`.toUpperCase();
    }
    return full;
  });

  const lines = cleanedText.split('\n');
  const elements = [];
  let currentList = [];
  let currentOrderedList = [];
  const choosable = typeof onActionClick === 'function' && isChoicePrompt(text);
  // Show the cleaned label when it differs from the markdown text (a repeated "(NAME)" suffix is dropped).
  const choiceNode = (item, label) => (label === String(item.raw).replace(/\*\*|__|`/g, '').replace(/\s+/g, ' ').trim() ? item.node : label);

  const parseInline = (line, lineIdx) => {
    const parts = line.split(/(\*\*[^*]+\*\*|`[^`]+`|\*[^*]+\*|\b[A-Za-z][A-Za-z0-9]*-\d+\b)/g);
    return parts.map((part, pIdx) => {
      const key = `${lineIdx}-${pIdx}`;
      if (part.startsWith('**') && part.endsWith('**')) {
        const inner = part.slice(2, -2);
        const keyMatch = inner.match(/^([A-Za-z][A-Za-z0-9]*-\d+)$/);
        if (keyMatch && isIssueKey(keyMatch[1])) {
          const dispKey = formatDisplayKey(keyMatch[1]);
          return (
            <button
              key={key}
              type="button"
              className="chat-inline-key-pill"
              title={`Ask AI to inspect ${dispKey}`}
              onClick={() => onActionClick?.(`Give me the full details and next steps for issue ${keyMatch[1]}`)}
            >
              <span className="key-pill-label">{dispKey}</span>
              <span className="key-pill-arrow">↗</span>
            </button>
          );
        }
        return <strong key={key}>{inner}</strong>;
      }
      if (part.startsWith('`') && part.endsWith('`')) {
        const codeText = part.slice(1, -1);
        const codeKeyMatch = codeText.match(/^([A-Za-z][A-Za-z0-9]*-\d+)$/);
        if (codeKeyMatch && isIssueKey(codeKeyMatch[1])) {
          const dispKey = formatDisplayKey(codeKeyMatch[1]);
          return (
            <button
              key={key}
              type="button"
              className="chat-inline-key-pill"
              title={`Ask AI to inspect ${dispKey}`}
              onClick={() => onActionClick?.(`Give me the full details and next steps for issue ${codeKeyMatch[1]}`)}
            >
              <span className="key-pill-label">{dispKey}</span>
              <span className="key-pill-arrow">↗</span>
            </button>
          );
        }
        const pill = statusPill(codeText);
        if (pill) {
          return (
            <span key={key} className={`chat-status-pill tone-${pill.tone}`}>
              {pill.label}
            </span>
          );
        }
        return (
          <code key={key} className="chat-inline-code">
            {codeText}
          </code>
        );
      }
      if (part.startsWith('*') && part.endsWith('*')) {
        return <em key={key}>{part.slice(1, -1)}</em>;
      }
      const rawKeyMatch = part.match(/^([A-Za-z][A-Za-z0-9]*-\d+)$/);
      if (rawKeyMatch && isIssueKey(rawKeyMatch[1])) {
        const dispKey = formatDisplayKey(rawKeyMatch[1]);
        return (
          <button
            key={key}
            type="button"
            className="chat-inline-key-pill"
            title={`Ask AI to inspect ${dispKey}`}
            onClick={() => onActionClick?.(`Give me the full details and next steps for issue ${rawKeyMatch[1]}`)}
          >
            <span className="key-pill-label">{dispKey}</span>
            <span className="key-pill-arrow">↗</span>
          </button>
        );
      }
      return part;
    });
  };

  const renderKvCard = (kvItems, cardIdx) => {
    let summaryItem = null;
    const metaItems = [];

    kvItems.forEach((item) => {
      if (item.label.toLowerCase() === 'summary') {
        summaryItem = item;
      } else {
        metaItems.push(item);
      }
    });

    return (
      <div key={`kv-card-${cardIdx}`} className="chat-issue-card">
        {summaryItem && (
          <div className="issue-card-summary-row">
            <span className="summary-badge">Summary</span>
            <div className="summary-text">{parseInline(summaryItem.value, `sum-${cardIdx}`)}</div>
          </div>
        )}
        <div className="issue-card-grid">
          {metaItems.map((m, mIdx) => {
            const lowerLabel = m.label.toLowerCase();
            const val = m.value;

            // Status chip
            if (lowerLabel.includes('status')) {
              const cleanStatus = val.replace(/[^A-Za-z_]/g, '').toUpperCase() || 'TODO';
              return (
                <div key={mIdx} className="issue-meta-item">
                  <span className="meta-item-label">Status</span>
                  <span className={`meta-status-pill ${cleanStatus.toLowerCase().replace('_', '-')}`}>
                    <span className="status-indicator-dot" />
                    {cleanStatus.replace('_', ' ')}
                  </span>
                </div>
              );
            }

            // Priority chip
            if (lowerLabel.includes('priority')) {
              const cleanPri = val.replace(/[^A-Za-z]/g, '').toUpperCase() || 'MEDIUM';
              return (
                <div key={mIdx} className="issue-meta-item">
                  <span className="meta-item-label">Priority</span>
                  <span className={`meta-priority-pill ${cleanPri.toLowerCase()}`}>
                    <span className="priority-indicator-dot" />
                    {cleanPri}
                  </span>
                </div>
              );
            }

            // Project chip
            if (lowerLabel.includes('project')) {
              return (
                <div key={mIdx} className="issue-meta-item">
                  <span className="meta-item-label">Project</span>
                  <span className="meta-project-pill" title={val}>
                    <span className="meta-icon">📁</span>
                    <span className="meta-text">{val}</span>
                  </span>
                </div>
              );
            }

            // Issue Type
            if (lowerLabel.includes('type')) {
              const isBug = val.toLowerCase().includes('bug');
              return (
                <div key={mIdx} className="issue-meta-item">
                  <span className="meta-item-label">Type</span>
                  <span className="meta-type-pill">
                    <span className="meta-icon">{isBug ? '🐞' : '📌'}</span>
                    <span className="meta-text">{val}</span>
                  </span>
                </div>
              );
            }

            // Assignee or Reporter
            if (lowerLabel.includes('assignee') || lowerLabel.includes('reporter')) {
              const initials = getInitials(val);
              return (
                <div key={mIdx} className="issue-meta-item">
                  <span className="meta-item-label">{m.label}</span>
                  <span className="meta-user-pill" title={val}>
                    <span className="user-avatar-circle">{initials}</span>
                    <span className="meta-text">{val}</span>
                  </span>
                </div>
              );
            }

            // Dates
            if (lowerLabel.includes('date') || lowerLabel.includes('created')) {
              return (
                <div key={mIdx} className="issue-meta-item">
                  <span className="meta-item-label">{m.label}</span>
                  <span className="meta-date-pill">
                    <span className="meta-icon">📅</span>
                    <span className="meta-text">{val}</span>
                  </span>
                </div>
              );
            }

            // Default attribute
            return (
              <div key={mIdx} className="issue-meta-item">
                <span className="meta-item-label">{m.label}</span>
                <span className="meta-generic-pill">{parseInline(val, `kv-${cardIdx}-${mIdx}`)}</span>
              </div>
            );
          })}
        </div>
      </div>
    );
  };

  const flushLists = () => {
    if (currentList.length > 0) {
      // Check if items are structured key-values
      const kvParsed = currentList.map((item) => (item.checklist ? null : extractKv(item.raw)));
      const kvCount = kvParsed.filter(Boolean).length;

      if (kvCount >= 2 && kvCount >= Math.floor(currentList.length * 0.6)) {
        // Render executive issue card
        elements.push(renderKvCard(kvParsed.filter(Boolean), elements.length));
      } else {
        elements.push(
          <ul key={`list-${elements.length}`} className="chat-bullet-list">
            {currentList.map((item, iIdx) => (
              <li key={iIdx} className="chat-bullet-item">
                {item.node}
              </li>
            ))}
          </ul>,
        );
      }
      currentList = [];
    }

    if (currentOrderedList.length > 0) {
      elements.push(
        <div key={`olist-${elements.length}`} className="chat-step-list">
          {currentOrderedList.map((item, iIdx) => {
            const label = choiceLabel(item.raw);
            // A list the assistant offers as options (it ends in a question) is clickable: the choice is sent as the reply.
            if (choosable && label.length > 0 && label.length <= 200) {
              return (
                <button
                  key={iIdx}
                  type="button"
                  className="chat-step-item chat-choice-item"
                  title={`Choose ${label}`}
                  onClick={() => onActionClick(label)}
                >
                  <span className="step-num-badge">{iIdx + 1}</span>
                  <div className="step-content">{choiceNode(item, label)}</div>
                </button>
              );
            }
            return (
              <div key={iIdx} className="chat-step-item">
                <span className="step-num-badge">{iIdx + 1}</span>
                <div className="step-content">{item.node}</div>
              </div>
            );
          })}
        </div>,
      );
      currentOrderedList = [];
    }
  };

  // A bare status such as PENDING or ACTIVE inside a table cell gets the same pill as one in backticks.
  const renderCell = (cell, cellKey) => {
    const pill = statusPill(cell.replace(/^`|`$/g, '').trim());
    if (pill) return <span className={`chat-status-pill tone-${pill.tone}`}>{pill.label}</span>;
    return cell === '' ? <span className="chat-table-empty">—</span> : parseInline(cell, cellKey);
  };

  let skipUntil = 0;
  let skippingNextSteps = false;

  lines.forEach((line, idx) => {
    if (idx < skipUntil) return;
    const trimmed = line.trim();

    // Check if this line is an action suggestion or next-steps section header
    const isNextStepsHeader = /^(###\s+|##\s+)?(🛠️\s*)?(recommended|suggested)?\s*(engineering\s*)?next\s*steps/i.test(trimmed) || 
                              /^(quick|suggested)\s*actions\s*:/i.test(trimmed);

    if (isNextStepsHeader) {
      flushLists();
      skippingNextSteps = true;
      return;
    }

    // Ignore standalone prompt suggestions or footers
    if (/^(suggested|quick)\s*actions\s*:/i.test(trimmed) || /^\*?Need an action\?/i.test(trimmed)) {
      return;
    }

    // Reset next-steps suppression if a new major section header starts
    if (trimmed.startsWith('### ') || trimmed.startsWith('## ') || trimmed.startsWith('---')) {
      skippingNextSteps = false;
    }

    if (skippingNextSteps) {
      // Continue suppressing lines until next section
      if (trimmed.startsWith('- ') || trimmed.startsWith('• ') || trimmed.startsWith('* ') || 
          trimmed.match(/^(\d+)\.\s+/) || trimmed.startsWith('[') || trimmed === '') {
        return;
      }
      skippingNextSteps = false;
    }

    // Markdown table: header row, separator row, then body rows.
    const table = parseMarkdownTable(lines, idx);
    if (table) {
      flushLists();
      skipUntil = table.nextIndex;
      elements.push(
        <div key={`table-${idx}`} className="chat-table-wrap">
          <table className={`chat-table${table.headers.length === 2 && /^(field|detail|item)/i.test(table.headers[0]) ? ' kv' : ''}`}>
            <thead>
              <tr>
                {table.headers.map((h, hIdx) => (
                  <th key={hIdx}>{parseInline(h, `th-${idx}-${hIdx}`)}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {table.rows.map((row, rIdx) => (
                <tr key={rIdx}>
                  {row.map((cell, cIdx) => (
                    <td key={cIdx}>{renderCell(cell, `td-${idx}-${rIdx}-${cIdx}`)}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>,
      );
      return;
    }

    if (trimmed === '---' || trimmed === '***' || trimmed === '___') {
      flushLists();
      elements.push(<hr key={`hr-${idx}`} className="chat-divider" />);
      return;
    }

    // Markdown H3 / H4
    if (trimmed.startsWith('### ')) {
      flushLists();
      const meta = getSectionMeta(trimmed.slice(4).trim());
      elements.push(
        <div key={`h4-${idx}`} className="chat-section-header">
          <span className="chat-section-icon">{meta.icon}</span>
          <span className="chat-section-title">{meta.label}</span>
        </div>,
      );
      return;
    }

    if (trimmed.startsWith('## ')) {
      flushLists();
      const meta = getSectionMeta(trimmed.slice(3).trim());
      elements.push(
        <div key={`h3-${idx}`} className="chat-section-header major">
          <span className="chat-section-icon">{meta.icon}</span>
          <span className="chat-section-title">{meta.label}</span>
        </div>,
      );
      return;
    }

    // Unstyled Section Title (e.g. "Issue Overview", "Scope & Description", "Current Sprint Telemetry")
    const sectionMatch = trimmed.match(/^(Issue Overview|Scope & Description|Current Sprint Telemetry|Issue Details|Summary of Findings|Resolution Plan):?$/i);
    if (sectionMatch) {
      flushLists();
      const meta = getSectionMeta(sectionMatch[1]);
      elements.push(
        <div key={`section-h-${idx}`} className="chat-section-header">
          <span className="chat-section-icon">{meta.icon}</span>
          <span className="chat-section-title">{meta.label}</span>
        </div>,
      );
      return;
    }

    const orderedMatch = trimmed.match(/^(\d+)\.\s+(.*)$/);
    if (orderedMatch) {
      if (currentList.length > 0) flushLists();
      currentOrderedList.push({ node: parseInline(orderedMatch[2].trim(), idx), raw: orderedMatch[2].trim() });
      return;
    }

    if (trimmed.startsWith('- ') || trimmed.startsWith('• ') || (trimmed.startsWith('* ') && !trimmed.endsWith('*'))) {
      if (currentOrderedList.length > 0) flushLists();
      const itemContent = trimmed.slice(2).trim();
      const check = itemContent.match(/^\[( |x|X)\]\s+(.*)$/);
      if (check) {
        const done = check[1].toLowerCase() === 'x';
        currentList.push({
          raw: check[2],
          checklist: true,
          node: (
            <span className={`chat-check-item${done ? ' done' : ''}`}>
              <span className="chat-checkbox" aria-hidden="true">{done ? '☑' : '☐'}</span>
              <span>{parseInline(check[2], idx)}</span>
            </span>
          ),
        });
        return;
      }
      currentList.push({
        raw: itemContent,
        node: parseInline(itemContent, idx),
      });
      return;
    }

    flushLists();
    if (trimmed === '') {
      elements.push(<div key={`space-${idx}`} className="chat-line-break" />);
    } else {
      elements.push(
        <p key={`p-${idx}`} className="chat-paragraph">
          {parseInline(trimmed, idx)}
        </p>,
      );
    }
  });

  flushLists();
  return <div className="formatted-chat-body">{elements}</div>;
}

export default function AthennaWorkspaceChat() {
  const { session, can } = useAuth();
  const userName = session?.user?.name || session?.user?.email?.split('@')[0] || 'Rishi Ragupathi';
  const tenantName = session?.user?.tenantName || 'Grid Dynamics';
  const userRole = session?.user?.role || 'DEVELOPER';
  const starterCards = useMemo(() => starterPromptsFor({ role: userRole, can }), [userRole, can]);

  // Model Selection State
  const [selectedModel, setSelectedModel] = useState(() => {
    return getSavedModel();
  });
  const [modelMenuOpen, setModelMenuOpen] = useState(false);
  const modelMenuRef = useRef(null);

  const activeModelObj = useMemo(() => {
    return AVAILABLE_MODELS.find((m) => m.id === selectedModel) || AVAILABLE_MODELS[0];
  }, [selectedModel]);

  // Which providers the server can actually call. Until it answers, nothing is greyed out.
  const modelsQuery = useGetAgentModelsQuery();
  // The server keeps this answer for two minutes, so asking again when the chat opens is cheap.
  const nudgesQuery = useGetAgentNudgesQuery(undefined, { refetchOnMountOrArgChange: 120 });
  const memoryQuery = useGetAgentMemoryQuery();
  const [deleteMemory] = useDeleteAgentMemoryMutation();
  const [clearMemory] = useClearAgentMemoryMutation();
  // Only Admin and HR manage the company documents; nobody else asks the server for the list.
  const manageKnowledge = canManageKnowledge(userRole);
  const knowledgeQuery = useGetAgentKnowledgeQuery(undefined, { skip: !manageKnowledge });
  const [uploadKnowledge] = useUploadAgentKnowledgeMutation();
  const [deleteKnowledge] = useDeleteAgentKnowledgeMutation();
  const providerAvailability = useMemo(() => {
    const byName = {};
    (modelsQuery?.data?.providers || []).forEach((p) => {
      byName[p.name] = { available: p.available, reason: p.reason };
    });
    return byName;
  }, [modelsQuery?.data]);

  // A saved choice the server cannot serve (for example OpenAI without a key) goes back to the default.
  useEffect(() => {
    const chosen = AVAILABLE_MODELS.find((m) => m.id === selectedModel);
    if (chosen && providerAvailability[chosen.provider]?.available === false && selectedModel !== DEFAULT_MODEL) {
      setSelectedModel(DEFAULT_MODEL);
      saveModel(DEFAULT_MODEL);
    }
  }, [providerAvailability, selectedModel]);

  const handleSelectModel = (modelId) => {
    setSelectedModel(modelId);
    saveModel(modelId);
    setModelMenuOpen(false);
  };

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (modelMenuRef.current && !modelMenuRef.current.contains(e.target)) {
        setModelMenuOpen(false);
      }
    };
    if (modelMenuOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      return () => document.removeEventListener('mousedown', handleClickOutside);
    }
  }, [modelMenuOpen]);

  // Transition issue mutation for one-click status transitions
  const [cardStatusOverrides, setCardStatusOverrides] = useState({});
  const [pendingConfirmIssue, setPendingConfirmIssue] = useState(null);
  // Leave requests the assistant has approved or rejected in this chat, keyed by request number.
  const [leaveOutcomes, setLeaveOutcomes] = useState({});
  // Messages whose issue form was submitted: their lead-in text ("pick the project first") no longer applies.
  const [createdForms, setCreatedForms] = useState({});
  const [fetchIssueDetails] = api.useLazyIssueQuery();
  const [transitionIssue, { isLoading: isTransitioning }] = useTransitionIssueMutation();

  // Escape key listener for custom confirmation modal
  useEffect(() => {
    if (!pendingConfirmIssue) return;
    const handleKeyDown = (e) => {
      if (e.key === 'Escape') {
        setPendingConfirmIssue(null);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [pendingConfirmIssue]);

  const handleQuickTransition = async (issue, targetStatusCode, confirmSkip = false) => {
    try {
      let version = issue.version;
      if (version == null) {
        const fresh = await fetchIssueDetails(issue.id).unwrap();
        version = fresh?.version;
      }
      if (version == null) {
        console.warn('Cannot transition issue without version:', issue);
        return;
      }

      try {
        await transitionIssue({
          id: String(issue.id),
          statusCode: targetStatusCode,
          version: version,
          ...(confirmSkip ? { confirmSkip: true } : {}),
        }).unwrap();
      } catch (innerErr) {
        if (innerErr?.status === 412) {
          // Stale version - refetch fresh issue and retry with latest version
          const fresh = await fetchIssueDetails(issue.id).unwrap();
          if (fresh?.version != null) {
            await transitionIssue({
              id: String(issue.id),
              statusCode: targetStatusCode,
              version: fresh.version,
              ...(confirmSkip ? { confirmSkip: true } : {}),
            }).unwrap();
          }
        } else {
          throw innerErr;
        }
      }

      setCardStatusOverrides((prev) => ({
        ...prev,
        [issue.id]: targetStatusCode,
        [issue.issueKey]: targetStatusCode,
      }));

      recentIssuesQuery.refetch();
      allIssuesQuery.refetch();
      doneIssuesQuery.refetch();
    } catch {
      console.error('Failed to transition issue status:', err);
    }
  };

  // 1. Live Query: Daily Updated Issues assigned strictly to the current user (re-polls every 3s)
  const recentIssuesQuery = useSearchIssuesQuery(
    {
      AssigneeUserId: session?.user?.id,
      AssignedOnly: true,
      Page: 1,
      PageSize: 15,
      SortBy: 'updatedAt',
      SortDirection: 'desc',
    },
    {
      pollingInterval: 3000,
      refetchOnFocus: true,
      refetchOnMountOrArgChange: true,
      skip: !can('ISSUE_VIEW') || !session?.user?.id,
    }
  );

  // 2. Live Query: Total Issues & Completed percentage for ASSIGNED issues
  const allIssuesQuery = useSearchIssuesQuery(
    {
      AssigneeUserId: session?.user?.id,
      AssignedOnly: true,
      Page: 1,
      PageSize: 100,
    },
    {
      pollingInterval: 3000,
      refetchOnFocus: true,
      refetchOnMountOrArgChange: true,
      skip: !can('ISSUE_VIEW') || !session?.user?.id,
    }
  );
  const doneIssuesQuery = useSearchIssuesQuery(
    {
      AssigneeUserId: session?.user?.id,
      AssignedOnly: true,
      Status: 'DONE',
      Page: 1,
      PageSize: 100,
    },
    {
      pollingInterval: 3000,
      refetchOnFocus: true,
      refetchOnMountOrArgChange: true,
      skip: !can('ISSUE_VIEW') || !session?.user?.id,
    }
  );

  // 3. Live Query: Daily Work Logs from Chronos
  const todayStr = useMemo(() => {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }, []);

  const workLogsQuery = useWorkLogsQuery(
    {
      From: todayStr,
      To: todayStr,
      Page: 1,
      PageSize: 20,
    },
    { ...liveQueryOptions, skip: !can('WORKLOG_VIEW') && !can('TIMESHEET_VIEW') }
  );

  // Calculate real issue completion statistics for assigned issues
  const { completionPct, totalIssuesCount, doneCount, inProgressCount } = useMemo(() => {
    if (!allIssuesQuery.data) {
      return { completionPct: 0, totalIssuesCount: 0, doneCount: 0, inProgressCount: 0 };
    }
    const total = allIssuesQuery.data.totalCount ?? allIssuesQuery.data.items?.length ?? 0;
    const done = doneIssuesQuery.data?.totalCount ?? doneIssuesQuery.data?.items?.length ?? 0;
    const items = allIssuesQuery.data.items || [];
    const inProgress = items.filter(i => ['IN_PROGRESS', 'CODE_REVIEW', 'TESTING'].includes(i.statusCode)).length;
    const pct = total > 0 ? Math.round((done / total) * 100) : 0;
    return { completionPct: pct, totalIssuesCount: total, doneCount: done, inProgressCount: inProgress };
  }, [allIssuesQuery.data, doneIssuesQuery.data]);

  // Calculate real daily log work totals directly from Chronos
  const { todayTotalHours, workLogItems, totalMinutes } = useMemo(() => {
    const items = workLogsQuery.data?.items || [];
    const totalMins = items.reduce((acc, curr) => acc + (Number(curr.durationMinutes || curr.minutes || 0)), 0);
    const hours = (totalMins / 60).toFixed(1);
    return {
      todayTotalHours: `${hours}h`,
      workLogItems: items,
      totalMinutes: totalMins,
    };
  }, [workLogsQuery.data]);

  // Right Chat & Copilot state
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState('');
  // Files chosen for the next message: { key, name, size, contentType, status: 'uploading' | 'ready' | 'error', id?, error? }.
  const [pendingFiles, setPendingFiles] = useState([]);
  const [attachNotice, setAttachNotice] = useState('');
  const fileInputRef = useRef(null);
  // Messages typed while an answer is still coming. They are sent one by one, in order, and can be edited or removed first.
  const MAX_QUEUED = 10;
  const [queue, setQueue] = useState([]);
  const [queuePaused, setQueuePaused] = useState(false);
  const [editingQueueId, setEditingQueueId] = useState(null);
  const [sending, setSending] = useState(false);
  const [sessionId, setSessionId] = useState(() => 'sess_' + Math.random().toString(36).substring(2, 11));
  const [chatWithAthenna, { isLoading }] = useChatWithAthennaMutation();

  // Saved chats: nothing is stored until the user accepts. They are kept on the server under this user's account.
  const historyUserId = session?.user?.id;
  const [historyConsent, setHistoryConsent] = useState(null);
  const [historyReady, setHistoryReady] = useState(false);
  const [historyError, setHistoryError] = useState(null);
  const [savedChats, setSavedChats] = useState([]);
  const historyMessage = (error, fallback) => error?.message || fallback;

  useEffect(() => {
    let cancelled = false;
    setHistoryReady(false);
    setHistoryConsent(null);
    setSavedChats([]);
    if (!historyUserId) return undefined;
    (async () => {
      try {
        // Chats an earlier version kept in this browser are moved to the server first.
        await migrateLocalHistory(historyUserId);
        const [consent, chats] = await Promise.all([fetchConsent(), fetchChats()]);
        if (cancelled) return;
        setHistoryConsent(consent);
        setSavedChats(chats);
        setHistoryError(null);
      } catch (error) {
        if (!cancelled) setHistoryError(historyMessage(error, 'Chat history could not be loaded.'));
      } finally {
        if (!cancelled) setHistoryReady(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [historyUserId]);

  useEffect(() => {
    if (historyConsent !== 'yes' || !historyUserId || messages.length === 0) return undefined;
    // Opening an old chat is not a new message, so it does not move to the top by itself.
    if (messages.every((m) => m.restored)) return undefined;
    // Wait a moment so a quick run of messages is one save, not several.
    const timer = setTimeout(async () => {
      const chat = { id: sessionId, title: chatTitle(messages), updatedAt: Date.now(), messages: compactMessages(messages) };
      try {
        await uploadChat(chat);
        setSavedChats((prev) =>
          [{ id: chat.id, title: chat.title, updatedAt: chat.updatedAt, messageCount: chat.messages.length }, ...prev.filter((c) => c.id !== chat.id)].slice(0, MAX_CHATS),
        );
        setHistoryError(null);
      } catch (error) {
        setHistoryError(historyMessage(error, 'This chat could not be saved.'));
      }
    }, 600);
    return () => clearTimeout(timer);
  }, [messages, historyConsent, historyUserId, sessionId]);

  const handleHistoryConsent = async (value) => {
    // Turning saving off only stops new chats from being saved; earlier ones stay until the user clears them.
    try {
      await putConsent(value);
      setHistoryConsent(value);
      setHistoryError(null);
    } catch (error) {
      setHistoryError(historyMessage(error, 'Your choice could not be saved.'));
    }
  };

  const startNewChat = () => {
    if (isLoading) return;
    setMessages([]);
    setCreatedForms({});
    setInput('');
    discardPending();
    clearQueue();
    setSessionId('sess_' + Math.random().toString(36).substring(2, 11));
  };

  // Saving was off: turn it on, keep the conversation being left, then start the new one.
  const saveAndStartNew = async () => {
    try {
      await putConsent('yes');
      setHistoryConsent('yes');
      if (messages.length > 0) {
        const chat = { id: sessionId, title: chatTitle(messages), updatedAt: Date.now(), messages: compactMessages(messages) };
        await uploadChat(chat);
        setSavedChats((prev) =>
          [{ id: chat.id, title: chat.title, updatedAt: chat.updatedAt, messageCount: chat.messages.length }, ...prev.filter((c) => c.id !== chat.id)].slice(0, MAX_CHATS),
        );
      }
      setHistoryError(null);
      startNewChat();
    } catch (error) {
      setHistoryError(historyMessage(error, 'This chat could not be saved.'));
    }
  };

  const openSavedChat = async (chat) => {
    if (isLoading) return;
    try {
      // The list holds only titles; the messages are fetched when a chat is opened.
      const full = Array.isArray(chat.messages) ? chat : await fetchChat(chat.id);
      // Buttons from an old answer (such as Approve) are not offered again; ask afresh to act on current data.
      setMessages((full.messages || []).map((m) => ({ ...m, restored: true })));
      setSessionId(chat.id);
      setCreatedForms({});
      setInput('');
      discardPending();
      clearQueue();
      setHistoryError(null);
    } catch (error) {
      setHistoryError(historyMessage(error, 'That chat could not be opened.'));
    }
  };

  const removeSavedChat = async (id) => {
    try {
      await removeChat(id);
      setSavedChats((prev) => prev.filter((c) => c.id !== id));
      setHistoryError(null);
    } catch (error) {
      setHistoryError(historyMessage(error, 'That chat could not be deleted.'));
    }
  };

  const clearSavedChats = async () => {
    try {
      await clearAllChats();
      setSavedChats([]);
      setHistoryError(null);
    } catch (error) {
      setHistoryError(historyMessage(error, 'Your chats could not be deleted.'));
    }
  };
  const chatStageRef = useRef(null);
  const messagesEndRef = useRef(null);
  const inputRef = useRef(null);
  // The chat request in flight, so Stop can cancel it.
  const chatRequestRef = useRef(null);
  // The step Athenna is on and the answer so far, while a reply is being written.
  const [live, setLive] = useState(emptyLive);
  const stoppedRef = useRef(false);

  // Time & greeting
  const [currentTime, setCurrentTime] = useState('');
  const [currentDate, setCurrentDate] = useState('');
  useEffect(() => {
    const updateDateTime = () => {
      const now = new Date();
      setCurrentDate(now.toLocaleDateString('en-US', { weekday: 'long', month: 'short', day: 'numeric' }));
      setCurrentTime(now.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }) + ' GMT');
    };
    updateDateTime();
    const interval = setInterval(updateDateTime, 30000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    if (chatStageRef.current) {
      if (typeof chatStageRef.current.scrollTo === 'function') {
        chatStageRef.current.scrollTo({
          top: chatStageRef.current.scrollHeight,
          behavior: 'smooth',
        });
      } else {
        chatStageRef.current.scrollTop = chatStageRef.current.scrollHeight;
      }
    }
  }, [messages, isLoading, live.text]);

  const addFiles = (list) => {
    const chosen = Array.from(list || []);
    if (chosen.length === 0) return;
    setAttachNotice('');
    const room = MAX_FILES - pendingFiles.length;
    if (room <= 0) {
      setAttachNotice(`You can attach up to ${MAX_FILES} files to one message.`);
      return;
    }
    const problems = [];
    const accepted = [];
    chosen.forEach((file) => {
      const problem = checkFile(file);
      if (problem) problems.push(problem);
      else if (accepted.length < room) accepted.push(file);
      else problems.push(`${file.name}: only ${MAX_FILES} files fit in one message.`);
    });
    if (problems.length) setAttachNotice(problems.join(' '));

    accepted.forEach((file) => {
      const key = `f_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
      const entry = { key, name: file.name, size: file.size, contentType: file.type, status: 'uploading' };
      setPendingFiles((prev) => [...prev, entry]);
      uploadAttachment(file)
        .then((saved) => {
          setPendingFiles((prev) => {
            // Removed while it was uploading: the copy that just landed is not wanted.
            if (!prev.some((p) => p.key === key)) {
              deleteAttachment(saved.id).catch(() => {});
              return prev;
            }
            return prev.map((p) => (p.key === key ? { ...p, ...toStored(saved), status: 'ready' } : p));
          });
        })
        .catch((err) => {
          setPendingFiles((prev) =>
            prev.map((p) => (p.key === key ? { ...p, status: 'error', error: err?.message || 'Upload failed' } : p)),
          );
        });
    });
  };

  const removePending = (file) => {
    setPendingFiles((prev) => prev.filter((p) => p.key !== file.key));
    if (file.id) deleteAttachment(file.id).catch(() => {});
  };

  /** Drops what was chosen but never sent, including the copies already uploaded. */
  const discardPending = () => {
    pendingFiles.forEach((p) => p.id && deleteAttachment(p.id).catch(() => {}));
    setPendingFiles([]);
    setAttachNotice('');
  };

  /** Drops every queued message, including files already uploaded for them. */
  const clearQueue = () => {
    queue.forEach((item) => item.attachments?.forEach((a) => a.id && deleteAttachment(a.id).catch(() => {})));
    setQueue([]);
    setQueuePaused(false);
    setEditingQueueId(null);
  };

  const removeQueued = (id) => {
    const item = queue.find((q) => q.id === id);
    item?.attachments?.forEach((a) => a.id && deleteAttachment(a.id).catch(() => {}));
    setQueue((prev) => prev.filter((q) => q.id !== id));
  };

  const saveQueued = (id, text) => {
    const item = queue.find((q) => q.id === id);
    if (!text && !item?.attachments?.length) {
      removeQueued(id);
      return;
    }
    setQueue((prev) => prev.map((q) => (q.id === id ? { ...q, text } : q)));
  };

  const filesUploading = pendingFiles.some((p) => p.status === 'uploading');
  const readyFiles = pendingFiles.filter((p) => p.status === 'ready');
  const draft = liveDraft(live.text);
  const canSend = (input.trim().length > 0 || readyFiles.length > 0) && !filesUploading;

  const executePrompt = async (text, files = []) => {
    const trimmed = text?.trim() || '';
    if ((!trimmed && files.length === 0) || isLoading) return;

    setSending(true);
    const attachments = files.map(toStored);
    const userMsg = {
      role: 'user',
      content: trimmed,
      ...(attachments.length ? { attachments } : {}),
      time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    };

    setMessages((prev) => [...prev, userMsg]);
    setInput('');
    if (files.length) {
      setPendingFiles((prev) => prev.filter((p) => p.status === 'uploading'));
      setAttachNotice('');
    }
    recordModelUsage(session?.user?.id, activeModelObj.provider);

    stoppedRef.current = false;
    const progressKey = `chat_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
    const stopListening = listenToChat(progressKey, (evt) => setLive((prev) => liveReply(prev, evt)));
    try {
      const request = chatWithAthenna({
        progressKey,
        message: withAttachmentNote(trimmed, attachments),
        ...(attachments.length ? { attachments: attachmentRefs(attachments) } : {}),
        sessionId,
        contextPortal: 'AIWorkHub',
        history: messages.slice(-10),
        extraContext: {
          role: userRole,
          permissions: session?.user?.permissions || [],
          userId: session?.user?.id,
          userName: userName,
          model: selectedModel,
        },
      });
      chatRequestRef.current = request;
      const response = await request.unwrap();

      if (response?.message) {
        setMessages((prev) => [
          ...prev,
          {
            role: 'assistant',
            content: response.message,
            actions: response.executedActions,
            notice: response.notice,
            time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          },
        ]);
        const decided = extractLeaveOutcomes(response.executedActions);
        if (Object.keys(decided).length > 0) setLeaveOutcomes((prev) => ({ ...prev, ...decided }));

        // Cards on screen follow a status change made through chat straight away, before the refetch lands.
        const moved = extractTransitions(response.executedActions);
        if (moved.length > 0) {
          setCardStatusOverrides((prev) => {
            const next = { ...prev };
            moved.forEach((t) => {
              if (t.id != null) next[t.id] = t.statusCode;
              if (t.issueKey) next[t.issueKey] = t.statusCode;
            });
            return next;
          });
        }
        // Trigger live query refetches so any issue transitions or worklogs reflect immediately in the UI
        recentIssuesQuery.refetch();
        allIssuesQuery.refetch();
        doneIssuesQuery.refetch();
        workLogsQuery.refetch();
      }
    } catch {
      if (stoppedRef.current) {
        setMessages((prev) => [
          ...prev,
          {
            role: 'assistant',
            content: 'Stopped. Ask again whenever you like.',
            isStopped: true,
            time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          },
        ]);
        return;
      }
      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          content: 'Sorry, I encountered an issue communicating with the Athenna backend engine. Please check service status.',
          isError: true,
          time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        },
      ]);
    } finally {
      stopListening();
      setLive(emptyLive);
      chatRequestRef.current = null;
      setSending(false);
    }
  };

  // Sends the next queued message once the answer to the previous one is in. A message being edited waits.
  useEffect(() => {
    if (isLoading || sending || queuePaused || queue.length === 0) return;
    const [next] = queue;
    if (next.id === editingQueueId) return;
    setQueue((prev) => prev.slice(1));
    executePrompt(next.text, next.attachments || []);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isLoading, sending, queuePaused, queue, editingQueueId]);

  const stopChat = () => {
    // Stop pauses the queue instead of firing the next message straight away.
    if (queue.length > 0) setQueuePaused(true);
    stoppedRef.current = true;
    chatRequestRef.current?.abort?.();
  };

  const handleSend = (e) => {
    e?.preventDefault();
    if (!canSend) return;
    if (isLoading || sending) {
      if (queue.length >= MAX_QUEUED) {
        setAttachNotice(`You can queue up to ${MAX_QUEUED} messages.`);
        return;
      }
      setQueue((prev) => [
        ...prev,
        { id: `q_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`, text: input.trim(), attachments: readyFiles.map(toStored) },
      ]);
      setInput('');
      setPendingFiles((prev) => prev.filter((p) => p.status !== 'ready'));
      setAttachNotice('');
      return;
    }
    executePrompt(input, readyFiles);
  };

  const hasLoadedData = Boolean(recentIssuesQuery.data);
  const assignedIssuesList = useMemo(() => {
    // Nothing is shown until the real list arrives; no placeholder tickets.
    if (!recentIssuesQuery.data?.items) return [];
    const userId = session?.user?.id;
    const items = recentIssuesQuery.data.items;
    return items.filter((issue) => {
      const status = String(
        cardStatusOverrides[issue.id] ||
        cardStatusOverrides[issue.issueKey] ||
        issue.statusCode ||
        issue.status ||
        ''
      ).toUpperCase();
      // Requirement 1: show except done, resolved, closed
      if (status === 'DONE' || status === 'RESOLVED' || status === 'CLOSED') {
        return false;
      }
      if (!userId) return true;
      const aId = issue.assigneeUserId ?? issue.assigneeId ?? issue.assignee?.id;
      return aId != null ? String(aId) === String(userId) : true;
    });
  }, [recentIssuesQuery.data, session?.user?.id, cardStatusOverrides]);

  return (
    <div className="agentic-dashboard-container">
      {/* ================= LEFT COLUMN: MULTI-AGENT METRIC & STATUS CARDS ================= */}
      <div className="agentic-cards-column">
        {/* Card 1: Daily Updated Issues (Assigned to Me) */}
        <div className="agentic-card scheduler-card daily-issues-card">
          <div className="agentic-card-header">
            <div className="agent-identity">
              <div className="agent-icon-badge orange-sun">
                <span>⚡</span>
              </div>
              <div className="agent-info">
                <h4 className="agent-name">Daily Updated Issues</h4>
                <span className="agent-dept">Assigned to Me • GridHub</span>
              </div>
            </div>
            <div className="agent-status-badge pending">
              <span className="status-dot-amber"></span>
              <span>Live Synced</span>
            </div>
          </div>

          <div className="agentic-card-body">
            <div className="daily-issues-header-row">
              <h3 className="proposal-title">My Assigned Issues</h3>
              <Link to="/my-work" className="issues-view-all-link">View all →</Link>
            </div>
            <p className="proposal-desc">
              Tickets currently assigned to you with recent updates across active sprints.
            </p>

            {hasLoadedData && assignedIssuesList.length === 0 ? (
              <div className="daily-issues-empty">
                <span className="empty-check-icon">✓</span>
                <span className="empty-text">No open issues currently assigned to you.</span>
              </div>
            ) : (
              <div className="daily-issues-list">
                {assignedIssuesList.slice(0, 3).map((issue) => {
                  const displayKey = formatDisplayKey(issue.issueKey);
                  const statusUpper = String(
                    cardStatusOverrides[issue.id] ||
                    cardStatusOverrides[issue.issueKey] ||
                    issue.statusCode ||
                    'TODO'
                  ).toUpperCase();
                  const nextTarget = statusUpper === 'TODO' ? 'IN_PROGRESS' : statusUpper === 'IN_PROGRESS' ? 'DONE' : null;
                  const nextLabel = statusUpper === 'TODO' ? '▶ Start' : statusUpper === 'IN_PROGRESS' ? '✓ Done' : null;

                  return (
                    <div key={issue.id} className={`daily-issue-item status-${statusUpper.toLowerCase().replace('_', '-')}`}>
                      <div className="daily-issue-top">
                        <div className="daily-issue-key-group">
                          <span className="issue-key-badge" title={issue.issueKey}>
                            {displayKey}
                          </span>
                          {issue.projectName && (
                            <span className="issue-project-tag" title={issue.projectName}>
                              {issue.projectName}
                            </span>
                          )}
                        </div>
                        <div className="issue-status-control-group">
                          <span className={`issue-status-pill ${statusUpper.toLowerCase().replace('_', '-')}`}>
                            <span className="status-indicator-dot"></span>
                            {statusUpper.replace('_', ' ')}
                          </span>
                          {nextTarget && (
                            <button
                              type="button"
                              className={`quick-status-transition-btn ${nextTarget.toLowerCase().replace('_', '-')}`}
                              disabled={isTransitioning}
                              title={`Advance status to ${nextTarget.replace('_', ' ')}`}
                              onClick={(e) => {
                                e.stopPropagation();
                                // Going straight to Done skips Code Review and Testing, which the workflow wants confirmed via custom popup.
                                const skipsSteps = nextTarget === 'DONE';
                                if (skipsSteps) {
                                  setPendingConfirmIssue({
                                    issue,
                                    targetStatusCode: nextTarget,
                                    displayKey,
                                  });
                                  return;
                                }
                                handleQuickTransition(issue, nextTarget, skipsSteps);
                              }}
                            >
                              {nextLabel}
                            </button>
                          )}
                        </div>
                      </div>
                      <div className="daily-issue-summary" title={issue.summary}>
                        {issue.summary}
                      </div>
                      <div className="daily-issue-footer">
                        <span className="issue-priority-tag">
                          <span className={`priority-dot ${String(issue.priority || 'medium').toLowerCase()}`}></span>
                          {issue.priority || 'Normal'}
                        </span>
                        <button
                          type="button"
                          className="issue-copilot-ask-btn"
                          onClick={() => executePrompt(`Give me the full details and next steps for issue ${issue.issueKey}`)}
                        >
                          Ask AI ✦
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>

        {/* Chat history: saved only if the user accepts, shown above Issue Completion */}
        <ChatHistoryCard
          consent={historyConsent}
          loading={!historyReady}
          error={historyError}
          chats={savedChats}
          activeId={sessionId}
          disabled={isLoading}
          onConsent={handleHistoryConsent}
          onOpen={openSavedChat}
          onDelete={removeSavedChat}
          onClear={clearSavedChats}
          hasCurrentChat={messages.length > 0}
          onNew={startNewChat}
          onSaveAndNew={saveAndStartNew}
        />

        {/* Card 2: Completed Percentage of Assigned Issues */}
        <div className="agentic-card metric-card">
          <div className="agentic-card-header">
            <div className="agent-identity">
              <div className="agent-icon-badge purple-sparkle">
                <span>✓</span>
              </div>
              <div className="agent-info">
                <h4 className="agent-name">Issue Completion</h4>
                <span className="agent-dept">Assigned Tasks • GridHub</span>
              </div>
            </div>
            <div className="agent-status-badge analyzing">
              <span className="status-dot-purple"></span>
              <span>{doneCount} / {totalIssuesCount} Done</span>
            </div>
          </div>

          <div className="agentic-card-body metric-body">
            <div className="metric-context-row">
              <span className="metric-activity">{inProgressCount} in progress</span>
              <span className="metric-dot">•</span>
              <span className="metric-datapoints">{totalIssuesCount} assigned tickets</span>
            </div>
            <div className="metric-graph-row">
              <div className="metric-value-col">
                <span className="metric-caption">Completed Ratio</span>
                <span className="metric-large-stat">{completionPct}%</span>
              </div>
            </div>
            {/* Custom progress bar */}
            <div className="completion-bar-track">
              <div className="completion-bar-fill" style={{ width: `${Math.min(100, completionPct)}%` }}></div>
            </div>
          </div>
        </div>

        {/* Card 3: Daily Log Work (Live from Chronos) */}
        <div className="agentic-card metric-card">
          <div className="agentic-card-header">
            <div className="agent-identity">
              <div className="agent-icon-badge green-cross">
                <span>⏱</span>
              </div>
              <div className="agent-info">
                <h4 className="agent-name">Daily Work Log</h4>
                <span className="agent-dept">Chronos Hours Logged Today</span>
              </div>
            </div>
            <div className="agent-status-badge executing">
              <span className="status-dot-green"></span>
              <span>Logged Today</span>
            </div>
          </div>

          <div className="agentic-card-body metric-body">
            <div className="metric-context-row">
              <span className="metric-activity">Target: 8.0h / day</span>
              <span className="metric-dot">•</span>
              <span className="metric-datapoints">{workLogItems.length} {workLogItems.length === 1 ? 'entry' : 'entries'} recorded</span>
            </div>
            <div className="metric-graph-row">
              <div className="metric-value-col">
                <span className="metric-caption">Hours Logged</span>
                <span className="metric-large-stat">
                  <strong>{todayTotalHours}</strong>
                  <span className="budget-total">/ 8h</span>
                </span>
              </div>
              <div className="metric-sparkline green">
                <svg viewBox="0 0 100 30" preserveAspectRatio="none">
                  {totalMinutes === 0 ? (
                    <path d="M0,26 L100,26" fill="none" stroke="#e2e8f0" strokeWidth="2" strokeDasharray="3 3" />
                  ) : (
                    <path
                      d={`M0,26 L35,${Math.round(25 - Math.min(1, totalMinutes / 480) * 8)} L70,${Math.round(23 - Math.min(1, totalMinutes / 480) * 14)} L100,${Math.max(4, Math.round(26 - Math.min(1, totalMinutes / 480) * 22))}`}
                      fill="none"
                      stroke="#22c55e"
                      strokeWidth="2.5"
                      strokeLinecap="round"
                    />
                  )}
                </svg>
              </div>
            </div>

            {/* Quick Worklog list preview */}
            {workLogItems.length === 0 ? (
              <div className="daily-worklog-empty">
                <span className="empty-text">No hours logged yet today.</span>
                {can('TIMESHEET_VIEW') && (
                  <Link to="/timesheets/my" className="worklog-log-now-link">Log work in Chronos →</Link>
                )}
              </div>
            ) : (
              <div className="daily-worklog-entries">
                {workLogItems.slice(0, 3).map((item) => (
                  <div key={item.id} className="worklog-entry-row">
                    <span className="worklog-entry-key" title={item.issueKey}>
                      {formatDisplayKey(item.issueKey)}
                    </span>
                    <span className="worklog-entry-summary" title={item.issueSummary || item.description}>
                      {item.issueSummary || item.description || 'Engineering work'}
                    </span>
                    <span className="worklog-entry-time">
                      {item.durationMinutes ? `${(item.durationMinutes / 60).toFixed(1)}h` : '0.0h'}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ================= RIGHT COLUMN: AI READY & CONVERSATION HUB ================= */}
      <div className="agentic-ai-hub-column">
        {/* Top Header greeting */}
        <div className="hub-header-greeting-bar">
          <div className="greeting-left">
            <div className="hub-tenant-badge">
              <Icon name="shield" />
              <span>{tenantName} Workspace</span>
            </div>
            <h2 className="greeting-main">Welcome back, {userName}</h2>
            <div className="greeting-agent-meta">
              <span className="agent-title-text">Athenna Autonomous Workspace Agent</span>
              <span className="agent-status-pill-live">
                <span className="status-dot-green"></span>
                Online & Ready
              </span>
              <span className="agent-rbac-pill" title={`Role-Based Access Control active: ${userRole}`}>
                <Icon name="shield" />
                <span>RBAC: {userRole}</span>
              </span>
            </div>
          </div>
          <div className="greeting-right">
            <div className="greeting-date-text">{currentDate || 'Tuesday, May 14'}</div>
            <div className="greeting-time-text">{currentTime || '14:02 PM GMT'}</div>
          </div>
        </div>

        {/* Main Stage: If no chat history yet, show Glowing AI Orb stage */}
        <div className="hub-center-stage" ref={chatStageRef}>
          {messages.length === 0 ? (
            <div className="ai-ready-stage-box">
              {/* Glowing Aura Orb */}
              <div className="ai-glowing-orb-wrapper">
                <div className="orb-spiral-halo"></div>
                <div className="orb-glowing-core">
                  <div className="orb-inner-dots">
                    <span className="orb-dot d1"></span>
                    <span className="orb-dot d2"></span>
                    <span className="orb-dot d3"></span>
                  </div>
                </div>
              </div>

              <h1 className="ai-ready-title">AI is ready</h1>
              <p className="ai-ready-subtitle">What would you like to accomplish across GridHub, Persona, and Chronos?</p>

              <AthennaNudges nudges={nudgesQuery?.data?.items} onAsk={executePrompt} />

              <div className="ai-starter-cards-grid">
                {starterCards.map((card) => (
                  <button
                    key={card.id}
                    type="button"
                    className="ai-starter-card"
                    onClick={() => executePrompt(card.prompt)}
                  >
                    <div className="starter-card-header">
                      <span className="starter-card-icon">{card.icon}</span>
                      <span className={`starter-card-tag ${card.badgeClass}`}>{card.system}</span>
                    </div>
                    <div className="starter-card-title">{card.title}</div>
                    <div className="starter-card-desc">{card.desc}</div>
                    <div className="starter-card-footer">
                      <span className="starter-card-action">Ask Athenna</span>
                      <span className="starter-card-arrow">→</span>
                    </div>
                  </button>
                ))}
              </div>

              <AthennaMemory
                notes={memoryQuery?.data?.notes}
                onForget={(id) => deleteMemory(id)}
                onForgetAll={() => clearMemory()}
              />

              {manageKnowledge && (
                <AthennaKnowledge
                  documents={knowledgeQuery?.data?.documents}
                  error={knowledgeQuery?.error?.message}
                  onUpload={(input) => uploadKnowledge(input).unwrap()}
                  onDelete={(id) => deleteKnowledge(id).unwrap()}
                />
              )}
            </div>
          ) : (
            <div className="agentic-chat-stream">
              {messages.map((m, idx) => {
                const actionIssues = m.role === 'assistant' ? extractActionIssues(m.actions) : [];
                const reply = m.role === 'assistant' ? splitSuggestedActions(m.content) : { body: m.content, chips: [] };
                // Chips the assistant chose for this reply win; the keyword suggestions are only a fallback.
                const downloads = m.role === 'assistant' ? extractFileDownloads(m.actions) : [];
                const citations = m.role === 'assistant' ? extractKnowledgeCitations(m.actions) : [];
                const leaveRequests = m.role === 'assistant' && !m.restored ? extractLeaveDecisions(m.actions) : [];
                const workLogForm = m.role === 'assistant' && !m.restored ? extractWorkLogForm(m.actions) : null;
                const issueForm = m.role === 'assistant' && !m.restored ? extractIssueForm(m.actions) : null;
                const leaveForm = m.role === 'assistant' && !m.restored ? extractLeaveForm(m.actions) : null;
                const skillsForm = m.role === 'assistant' && !m.restored ? extractSkillsForm(m.actions) : null;
                const profileSectionForm = m.role === 'assistant' && !m.restored ? extractProfileSectionForm(m.actions) : null;
                const deleteForm = m.role === 'assistant' && !m.restored ? extractDeleteForm(m.actions) : null;

                return (
                  <div key={idx} className={`stream-bubble-row ${m.role}`}>
                    {m.role === 'assistant' && (
                      <div className="stream-avatar assistant-avatar" title="Athenna AI">
                        <Icon name="sparkle" />
                      </div>
                    )}
                    <div className="stream-bubble-column">
                      <div className={`stream-bubble ${m.role} ${m.isError ? 'error' : ''}`}>
                        <div className="stream-bubble-text">
                          {m.role === 'user' ? (
                            <>
                              <MessageAttachments attachments={m.attachments} />
                              {m.content && <div className="user-plain-text">{m.content}</div>}
                            </>
                          ) : createdForms[idx] ? (
                            <div className="assistant-success-intro">
                              <span className="success-sparkle-icon" aria-hidden="true">✨</span>
                              <span>I've created the issue for you in <strong>GridHub</strong>:</span>
                            </div>
                          ) : (
                            renderFormattedContent(reply.body, executePrompt)
                          )}
                        </div>
                        {m.notice && <div className="stream-bubble-notice" role="status">{m.notice}</div>}

                        {workLogForm && <WorkLogCard form={workLogForm} disabled={isLoading} />}

                        {issueForm && (
                          <IssueCreateCard
                            form={issueForm}
                            disabled={isLoading}
                            onCreated={() => setCreatedForms((prev) => ({ ...prev, [idx]: true }))}
                          />
                        )}

                        {skillsForm && <SkillsEditCard form={skillsForm} disabled={isLoading} />}
                        {profileSectionForm && <ProfileSectionCard form={profileSectionForm} disabled={isLoading} />}
                        {deleteForm && <DeleteCard form={deleteForm} disabled={isLoading} />}

                        {leaveForm && (
                          <LeaveApplyCard
                            form={leaveForm}
                            disabled={isLoading}
                            onApplied={() => setCreatedForms((prev) => ({ ...prev, [idx]: true }))}
                          />
                        )}

                        <LeaveDecisionCards
                          requests={leaveRequests}
                          outcomes={leaveOutcomes}
                          disabled={isLoading}
                          onSend={executePrompt}
                        />

                        {citations.length > 0 && (
                          <div className="chat-citations-card">
                            <div className="chat-citations-header">
                              <span className="citations-icon">📚</span>
                              <span className="citations-title">Verified Knowledge Citations ({citations.length})</span>
                            </div>
                            <div className="chat-citations-list">
                              {citations.map((cite, cIdx) => (
                                <details key={cIdx} className="citation-item-collapse">
                                  <summary className="citation-summary">
                                    <span className="citation-doc-name">{cite.document}</span>
                                    {cite.section && <span className="citation-section-badge">{cite.section}</span>}
                                    <span className="citation-view-hint">View excerpt ›</span>
                                  </summary>
                                  <div className="citation-content-box">
                                    <p className="citation-passage-text">{cite.text}</p>
                                    {cite.updated && <span className="citation-updated-date">Indexed: {cite.updated}</span>}
                                  </div>
                                </details>
                              ))}
                            </div>
                          </div>
                        )}

                        {downloads.length > 0 && (
                          <div className="chat-file-downloads">
                            {downloads.map((file) => (
                              <button
                                key={file.fileName}
                                type="button"
                                className="chat-file-download-btn"
                                onClick={() => downloadTextFile(file)}
                              >
                                <span aria-hidden="true">⬇</span>
                                <span>Download {file.fileName}</span>
                              </button>
                            ))}
                          </div>
                        )}

                        {/* Embedded Interactive Issue Cards if search returned issues */}
                        {actionIssues.length > 0 && (
                          <div className="chat-embedded-issue-cards">
                            <div className="embedded-cards-header">
                              <span className="embedded-cards-badge">GridHub Live Sprints</span>
                              <span className="embedded-cards-count">{actionIssues.length} tickets found</span>
                            </div>
                            <div className="embedded-cards-grid">
                              {actionIssues.slice(0, 4).map((issue) => {
                                const currentStatus = String(
                                  cardStatusOverrides[issue.id] ||
                                  cardStatusOverrides[issue.issueKey] ||
                                  issue.statusCode ||
                                  'TODO'
                                ).toUpperCase();
                                const displayKey = formatDisplayKey(issue.issueKey || `ISSUE-${issue.id}`);

                                return (
                                  <ChatIssueCard
                                    key={issue.id || issue.issueKey}
                                    issue={issue}
                                    status={currentStatus}
                                    displayKey={displayKey}
                                    busy={isTransitioning}
                                    onInspect={() => executePrompt(`Give me the full details for issue ${displayKey}`)}
                                    onMoved={(moved, target) =>
                                      setCardStatusOverrides((prev) => ({
                                        ...prev,
                                        [moved.id]: target,
                                        [moved.issueKey]: target,
                                      }))
                                    }
                                  />
                                );
                              })}
                            </div>
                          </div>
                        )}

                        <span className="stream-bubble-time">{m.time}</span>
                      </div>
                    </div>
                  </div>
                );
              })}
              {isLoading && (
                <div className="stream-bubble-row assistant">
                  <div className="stream-avatar assistant-avatar" title="Athenna AI">
                    <Icon name="sparkle" />
                  </div>
                  <div className="stream-bubble assistant loading">
                    {draft ? (
                      <div className="stream-bubble-text live-draft">{renderFormattedContent(draft, () => {})}</div>
                    ) : (
                      <>
                        <div className="typing-dots">
                          <span></span>
                          <span></span>
                          <span></span>
                        </div>
                        {live.status && (
                          <div className="stream-status" role="status" aria-live="polite">
                            {live.status}…
                          </div>
                        )}
                      </>
                    )}
                  </div>
                </div>
              )}
              <div ref={messagesEndRef} className="chat-bottom-anchor" />
            </div>
          )}
        </div>

        {/* ================= BOTTOM FLOATING PROMPT DOCK ================= */}
        <div className="agentic-prompt-dock">
          <form className="agentic-prompt-card" onSubmit={handleSend}>
            <ChatQueue
              items={queue}
              paused={queuePaused}
              onEditing={setEditingQueueId}
              onSave={saveQueued}
              onRemove={removeQueued}
              onResume={() => setQueuePaused(false)}
            />
            <ComposerAttachments items={pendingFiles} onRemove={removePending} />
            {attachNotice && <div className="composer-attach-notice" role="alert">{attachNotice}</div>}
            <input
              ref={inputRef}
              type="text"
              className="agentic-prompt-input"
              placeholder={isLoading || sending ? 'Type your next message. It is sent when Athenna finishes...' : 'Ask Athenna across GridHub, Persona, and Chronos...'}
              value={input}
              onChange={(e) => setInput(e.target.value)}
              autoFocus
            />

            <div className="prompt-card-bottom-bar">
              <div className="bar-left">
                <input
                  ref={fileInputRef}
                  type="file"
                  multiple
                  hidden
                  accept={ACCEPT}
                  data-testid="chat-file-input"
                  onChange={(e) => {
                    addFiles(e.target.files);
                    e.target.value = '';
                  }}
                />
                <button
                  type="button"
                  className="attach-file-btn"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={pendingFiles.length >= MAX_FILES}
                  title={`Attach files (up to ${MAX_FILES}, 10 MB each)`}
                  aria-label="Attach files"
                >
                  <Icon name="paperclip" />
                </button>
                <div className="intelligence-model-dropdown-wrap" ref={modelMenuRef}>
                  <button
                    type="button"
                    className={`intelligence-selector-btn ${modelMenuOpen ? 'active' : ''}`}
                    onClick={() => setModelMenuOpen((prev) => !prev)}
                    title="Select AI Model & Intelligence Engine"
                  >
                    <span className="icon-intelligence">🗂</span>
                    <span className="intelligence-label">
                      <strong>{activeModelObj.name}</strong>
                    </span>
                    <span className="model-chip-tag">{activeModelObj.badge}</span>
                    <span className="icon-chevron">{modelMenuOpen ? '▴' : '▾'}</span>
                  </button>

                  {modelMenuOpen && (
                    <ModelPicker
                      models={AVAILABLE_MODELS}
                      selectedId={selectedModel}
                      availability={providerAvailability}
                      usage={usageStatus(session?.user?.id)}
                      onSelect={handleSelectModel}
                    />
                  )}
                </div>
              </div>

              <div className="bar-right">
                {isLoading && (
                  <button
                    type="button"
                    className="agentic-send-btn stop"
                    onClick={stopChat}
                    title="Stop"
                    aria-label="Stop response"
                  >
                    <span className="stop-square" aria-hidden="true" />
                  </button>
                )}
                {(!isLoading || canSend) && (
                  <button
                    type="submit"
                    className={`agentic-send-btn ${canSend ? 'active' : 'idle'} ${isLoading || sending ? 'queue' : ''}`}
                    disabled={!canSend}
                    title={
                      filesUploading
                        ? 'Waiting for files to upload'
                        : canSend
                          ? isLoading || sending ? 'Add to queue' : 'Send prompt'
                          : 'Enter prompt to send'
                    }
                    aria-label={isLoading || sending ? 'Add to queue' : 'Send prompt'}
                  >
                    {isLoading || sending ? '+' : '→'}
                  </button>
                )}
              </div>
            </div>
          </form>
        </div>
      </div>

      {/* ================= CUSTOM WORKFLOW TRANSITION CONFIRMATION MODAL ================= */}
      {pendingConfirmIssue && (
        <div
          className="custom-confirm-modal-overlay"
          role="dialog"
          aria-modal="true"
          aria-labelledby="confirm-modal-title"
          onClick={() => setPendingConfirmIssue(null)}
        >
          <div className="custom-confirm-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="custom-confirm-modal-header">
              <div className="custom-confirm-modal-icon-wrap">
                <span className="custom-confirm-modal-icon">⚠️</span>
              </div>
              <div className="custom-confirm-modal-header-text">
                <h3 id="confirm-modal-title" className="custom-confirm-modal-title">Skip Workflow Steps?</h3>
                <p className="custom-confirm-modal-subtitle">Confirmation required for non-sequential transition</p>
              </div>
              <button
                type="button"
                className="custom-confirm-modal-close"
                aria-label="Close"
                onClick={() => setPendingConfirmIssue(null)}
              >
                ×
              </button>
            </div>

            <div className="custom-confirm-modal-body">
              <div className="custom-confirm-issue-box">
                <div className="custom-confirm-issue-top">
                  <span className="custom-confirm-key-badge">
                    {pendingConfirmIssue.displayKey || pendingConfirmIssue.issue.issueKey}
                  </span>
                  {pendingConfirmIssue.issue.projectName && (
                    <span className="custom-confirm-project-tag">{pendingConfirmIssue.issue.projectName}</span>
                  )}
                </div>
                <div className="custom-confirm-issue-summary">
                  {pendingConfirmIssue.issue.summary}
                </div>
              </div>

              <div className="custom-confirm-warning-banner">
                <p>
                  Moving directly to <strong>Done</strong> skips the <strong>Code Review</strong> and <strong>Testing</strong> stages.
                </p>
              </div>

              <div className="custom-confirm-flow-track">
                <span className="flow-step current">IN PROGRESS</span>
                <span className="flow-arrow">➔</span>
                <span className="flow-step skipped" title="Skipped">CODE REVIEW</span>
                <span className="flow-arrow">➔</span>
                <span className="flow-step skipped" title="Skipped">TESTING</span>
                <span className="flow-arrow">➔</span>
                <span className="flow-step target">DONE</span>
              </div>
            </div>

            <div className="custom-confirm-modal-footer">
              <button
                type="button"
                className="custom-confirm-btn cancel"
                onClick={() => setPendingConfirmIssue(null)}
                disabled={isTransitioning}
              >
                Cancel
              </button>
              <button
                type="button"
                className="custom-confirm-btn confirm"
                disabled={isTransitioning}
                onClick={async () => {
                  const { issue, targetStatusCode } = pendingConfirmIssue;
                  setPendingConfirmIssue(null);
                  await handleQuickTransition(issue, targetStatusCode, true);
                }}
              >
                {isTransitioning ? 'Moving…' : 'Confirm & Move to Done'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
