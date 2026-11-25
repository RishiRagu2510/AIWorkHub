/**
 * Interactive Visual Report Viewer with Print / Save PDF, Excel, and CSV export.
 * Generates an executive-styled visual document with comparison transition cards,
 * metric stat cards, employee avatars, and full data grid.
 */

function escapeHtml(str) {
  return String(str ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

const AVATAR_PALETTE = [
  { bg: '#fef3c7', text: '#92400e' }, // amber
  { bg: '#dbeafe', text: '#1e40af' }, // blue
  { bg: '#e0e7ff', text: '#3730a3' }, // indigo
  { bg: '#dcfce7', text: '#166534' }, // green
  { bg: '#fce7f3', text: '#9d174d' }, // pink
  { bg: '#ede9fe', text: '#5b21b6' }, // purple
  { bg: '#ccfbf1', text: '#115e59' }, // teal
  { bg: '#ffedd5', text: '#9a3412' }, // orange
];

function getAvatarColor(name) {
  if (!name) return AVATAR_PALETTE[0];
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  return AVATAR_PALETTE[Math.abs(hash) % AVATAR_PALETTE.length];
}

function getInitials(firstName, lastName) {
  const f = firstName ? firstName.charAt(0).toUpperCase() : '';
  const l = lastName ? lastName.charAt(0).toUpperCase() : '';
  return `${f}${l}` || 'EM';
}

function cleanDuration(str) {
  if (!str || str === 'Not recorded' || str === '—') return str;
  let text = String(str);
  text = text.replace(/\b0\s*year\(s\),?\s*/gi, '');
  text = text.replace(/\b0\s*month\(s\),?\s*/gi, '');
  text = text.replace(/\b0\s*day\(s\),?\s*/gi, '');
  text = text.replace(/(?<!\d)1\s*year\(s\)/gi, '1 year');
  text = text.replace(/(\d+)\s*year\(s\)/gi, '$1 years');
  text = text.replace(/(?<!\d)1\s*month\(s\)/gi, '1 month');
  text = text.replace(/(\d+)\s*month\(s\)/gi, '$1 months');
  text = text.replace(/(?<!\d)1\s*day\(s\)/gi, '1 day');
  text = text.replace(/(\d+)\s*day\(s\)/gi, '$1 days');
  return text.replace(/^,\s*/, '').replace(/,\s*$/, '').trim() || '—';
}

export function openVisualReport({
  title = 'Career History Report',
  headerTitle = '',
  stats = [],
  cards = [],
  tableHeaders = [],
  tableRows = [],
  exportXlsxData: _exportXlsxData = null
}) {
  const finalHeaderTitle = headerTitle || title;
  const exportDateStr = new Date().toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });

  // Default 4 stat cards if not provided
  const statCards = stats.length > 0 ? stats : [
    { label: 'Total Employees', value: cards.length > 0 ? String(cards.length) : '1', icon: 'users' },
    { label: 'Employee ID', value: cards.length === 1 ? (cards[0].empCode || '—') : 'Multiple', icon: 'id' },
    { label: 'Export Date', value: exportDateStr, icon: 'calendar' },
    { label: 'Records Count', value: String(tableRows.length || cards.length), icon: 'document' },
  ];

  const getStatIcon = (type) => {
    switch (type) {
      case 'users':
        return `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>`;
      case 'id':
        return `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="16" rx="2"/><circle cx="9" cy="10" r="2"/><line x1="15" y1="8" x2="17" y2="8"/><line x1="15" y1="12" x2="17" y2="12"/><line x1="7" y1="16" x2="17" y2="16"/></svg>`;
      case 'calendar':
        return `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>`;
      default:
        return `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>`;
    }
  };

  const statCardsHtml = `
    <div class="stats-row">
      ${statCards.map(s => `
        <div class="stat-card">
          <div class="stat-icon-wrapper">
            ${getStatIcon(s.icon)}
          </div>
          <div class="stat-content">
            <span class="stat-label">${escapeHtml(s.label)}</span>
            <span class="stat-value">${escapeHtml(s.value)}</span>
          </div>
        </div>
      `).join('')}
    </div>`;

  // Career Cards HTML
  let cardsHtml = '';
  if (cards && cards.length > 0) {
    cardsHtml = `
      <div class="cards-list">
        ${cards.map((c, idx) => {
          const empName = c.empName || `${c.emp?.firstName || ''} ${c.emp?.lastName || ''}`.trim() || 'Employee';
          const initials = c.initials || getInitials(c.emp?.firstName, c.emp?.lastName);
          const color = c.avatarColor || getAvatarColor(empName);
          const empCode = c.empCode || c.emp?.employeeCode || (c.emp?.id ? `EMP${c.emp?.id}` : '—');
          const dept = c.dept || c.emp?.department || 'Developer';

          const prev = c.previous || {
            grade: 'T0 · Intern',
            jobTitle: 'Intern Software Engineer',
            department: dept,
            periodStart: '—',
            periodEnd: '—',
            duration: '—'
          };

          const curr = c.current || {
            grade: 'T1 · Junior',
            jobTitle: 'Junior Software Engineer',
            department: dept,
            periodStart: '—',
            periodEnd: 'Present',
            duration: '—'
          };

          return `
          <div class="career-card" id="card-${idx}">
            <div class="emp-profile-col">
              <div class="emp-avatar" style="background:${color.bg}; color:${color.text};">
                ${escapeHtml(initials)}
              </div>
              <div class="emp-details">
                <div class="emp-name">${escapeHtml(empName)}</div>
                <div class="emp-badge"># ${escapeHtml(empCode)}</div>
                <div class="emp-dept">${escapeHtml(dept)}</div>
              </div>
            </div>

            <div class="comparison-row">
              <!-- Previous Details -->
              <div class="details-box previous-box">
                <div class="box-header prev-header">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
                  Previous Working Details
                </div>
                <div class="box-grid">
                  <div class="grid-cell"><span class="lbl">Grade:</span><strong>${escapeHtml(prev.grade)}</strong></div>
                  <div class="grid-cell"><span class="lbl">Period:</span><strong>${escapeHtml(prev.periodStart)} &rarr; ${escapeHtml(prev.periodEnd)}</strong></div>
                  <div class="grid-cell"><span class="lbl">Job Title:</span><strong>${escapeHtml(prev.jobTitle)}</strong></div>
                  <div class="grid-cell"><span class="lbl">Period Length:</span><strong>${escapeHtml(cleanDuration(prev.duration))}</strong></div>
                  <div class="grid-cell"><span class="lbl">Department:</span><strong>${escapeHtml(prev.department)}</strong></div>
                </div>
              </div>

              <!-- Arrow -->
              <div class="transition-arrow">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="5" y1="12" x2="19" y2="12"/><polyline points="12 5 19 12 12 19"/></svg>
              </div>

              <!-- Current Details -->
              <div class="details-box current-box">
                <div class="box-header curr-header">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="7" width="20" height="14" rx="2" ry="2"/><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"/></svg>
                  Current Working Details
                </div>
                <div class="box-grid">
                  <div class="grid-cell"><span class="lbl">Grade:</span><strong>${escapeHtml(curr.grade)}</strong></div>
                  <div class="grid-cell"><span class="lbl">Period:</span><strong>${escapeHtml(curr.periodStart)} &rarr; ${escapeHtml(curr.periodEnd)}</strong></div>
                  <div class="grid-cell"><span class="lbl">Job Title:</span><strong>${escapeHtml(curr.jobTitle)}</strong></div>
                  <div class="grid-cell"><span class="lbl">Period Length:</span><strong>${escapeHtml(cleanDuration(curr.duration))}</strong></div>
                  <div class="grid-cell"><span class="lbl">Department:</span><strong>${escapeHtml(curr.department)}</strong></div>
                </div>
              </div>
            </div>

            <div class="card-toggle">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"/></svg>
            </div>
          </div>`;
        }).join('')}
      </div>`;
  }

  // Data Table HTML (Only rendered when visual cards are not present)
  let tableHtml = '';
  if ((!cards || cards.length === 0) && tableHeaders && tableHeaders.length > 0 && tableRows && tableRows.length > 0) {
    tableHtml = `
      <div class="table-container">
        <h3 class="table-section-title">Detailed Records</h3>
        <table class="report-table">
          <thead>
            <tr>${tableHeaders.map(h => `<th>${escapeHtml(h)}</th>`).join('')}</tr>
          </thead>
          <tbody>
            ${tableRows.map(row => `
              <tr>
                ${row.map(cell => `<td>${escapeHtml(typeof cell === 'string' ? cleanDuration(cell) : cell)}</td>`).join('')}
              </tr>
            `).join('')}
          </tbody>
        </table>
      </div>`;
  }

  const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>${escapeHtml(finalHeaderTitle)}</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: 'Roboto', sans-serif;
      background: #f8fafc;
      color: #0f172a;
      padding: 24px 32px;
      line-height: 1.5;
    }

    /* Top App Bar */
    .excel-app-bar {
      background: #0f766e;
      color: white;
      padding: 14px 24px;
      border-radius: 12px 12px 0 0;
      display: flex;
      align-items: center;
      justify-content: space-between;
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.08);
    }
    .excel-title {
      display: flex;
      align-items: center;
      gap: 12px;
      font-weight: 700;
      font-size: 16px;
      letter-spacing: -0.01em;
    }
    .excel-actions {
      display: flex;
      gap: 10px;
      align-items: center;
    }
    .excel-btn {
      background: rgba(255, 255, 255, 0.16);
      color: white;
      border: 1px solid rgba(255, 255, 255, 0.3);
      padding: 7px 16px;
      border-radius: 6px;
      font-size: 12.5px;
      font-weight: 600;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 6px;
      transition: background 0.15s;
    }
    .excel-btn:hover {
      background: rgba(255, 255, 255, 0.28);
    }

    /* Main Container */
    .report-container {
      background: white;
      border: 1px solid #cbd5e1;
      border-top: none;
      border-radius: 0 0 12px 12px;
      padding: 24px;
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.06);
    }

    /* Stat Cards Row */
    .stats-row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }
    .stat-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 10px;
      padding: 14px 18px;
      display: flex;
      align-items: center;
      gap: 14px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.03);
    }
    .stat-icon-wrapper {
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: #f0fdf4;
      color: #0f766e;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .stat-content {
      display: flex;
      flex-direction: column;
    }
    .stat-label {
      font-size: 11.5px;
      color: #64748b;
      font-weight: 600;
    }
    .stat-value {
      font-size: 17px;
      font-weight: 700;
      color: #0f172a;
      margin-top: 2px;
    }

    /* Career Cards List */
    .cards-list {
      display: flex;
      flex-direction: column;
      gap: 16px;
      margin-bottom: 28px;
    }
    .career-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 18px 22px;
      display: flex;
      align-items: center;
      gap: 20px;
      box-shadow: 0 1px 4px rgba(0, 0, 0, 0.04);
      transition: border-color 0.15s, box-shadow 0.15s;
    }
    .career-card:hover {
      border-color: #cbd5e1;
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.06);
    }

    .emp-profile-col {
      display: flex;
      align-items: center;
      gap: 14px;
      width: 220px;
      flex-shrink: 0;
    }
    .emp-avatar {
      width: 44px;
      height: 44px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 14.5px;
      flex-shrink: 0;
    }
    .emp-details {
      display: flex;
      flex-direction: column;
      gap: 3px;
      overflow: hidden;
    }
    .emp-name {
      font-weight: 700;
      font-size: 14px;
      color: #0f172a;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .emp-badge {
      display: inline-flex;
      width: fit-content;
      padding: 2px 8px;
      font-size: 11px;
      font-weight: 700;
      color: #2563eb;
      background: #eff6ff;
      border: 1px solid #bfdbfe;
      border-radius: 12px;
    }
    .emp-dept {
      font-size: 11.5px;
      color: #64748b;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .comparison-row {
      display: flex;
      align-items: center;
      gap: 14px;
      flex: 1;
    }
    .details-box {
      flex: 1;
      padding: 12px 16px;
      border-radius: 10px;
    }
    .previous-box {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
    }
    .current-box {
      background: #f0fdf4;
      border: 1px solid #bbf7d0;
    }

    .box-header {
      font-size: 12px;
      font-weight: 700;
      margin-bottom: 8px;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .prev-header { color: #2563eb; }
    .curr-header { color: #16a34a; }

    .box-grid {
      display: grid;
      grid-template-columns: 1.15fr 1fr;
      gap: 6px 14px;
      font-size: 11.5px;
    }
    .grid-cell {
      color: #64748b;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .grid-cell .lbl {
      margin-right: 4px;
    }
    .grid-cell strong {
      color: #1e293b;
    }

    .transition-arrow {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      background: #e0f2fe;
      color: #0284c7;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .card-toggle {
      color: #94a3b8;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      padding: 4px;
    }

    /* Table */
    .table-container {
      margin-top: 24px;
      overflow-x: auto;
    }
    .table-section-title {
      font-size: 14px;
      font-weight: 700;
      color: #0f172a;
      margin-bottom: 12px;
    }
    .report-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 12.5px;
      border: 1px solid #cbd5e1;
    }
    .report-table th {
      background: #f8fafc;
      color: #475569;
      font-weight: 700;
      text-align: left;
      padding: 10px 14px;
      border: 1px solid #cbd5e1;
      white-space: nowrap;
      font-size: 11px;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .report-table td {
      padding: 10px 14px;
      border: 1px solid #e2e8f0;
      color: #1e293b;
      white-space: nowrap;
    }
    .report-table tr:nth-child(even) td {
      background: #f8fafc;
    }
    .report-table tr:hover td {
      background: #f0fdf4;
    }

    @media print {
      body { background: white; padding: 0; }
      .excel-app-bar { display: none; }
      .report-container { box-shadow: none; border: none; padding: 0; }
    }
  </style>
</head>
<body>
  <div class="excel-app-bar">
    <div class="excel-title">
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>
      ${escapeHtml(finalHeaderTitle)}
    </div>
    <div class="excel-actions">
      <button class="excel-btn" onclick="window.print()">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
        Print / Save PDF
      </button>
      <button class="excel-btn" onclick="downloadCsv()">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
        Download CSV
      </button>
    </div>
  </div>

  <div class="report-container">
    ${statCardsHtml}
    ${cardsHtml}
    ${tableHtml}
  </div>

  <script>
    const rawHeaders = ${JSON.stringify(tableHeaders)};
    const rawRows = ${JSON.stringify(tableRows)};

    function downloadCsv() {
      function escapeCell(val) {
        const str = String(val ?? '');
        if (str.includes(',') || str.includes('"') || str.includes('\\n') || str.includes('\\r')) {
          return '"' + str.replace(/"/g, '""') + '"';
        }
        return str;
      }
      const csvContent = '\\uFEFF' + [
        rawHeaders.map(escapeCell).join(','),
        ...rawRows.map(row => row.map(escapeCell).join(','))
      ].join('\\r\\n');
      const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = '${escapeHtml(title).replace(/[^a-zA-Z0-9_-]/g, '_')}_' + new Date().toISOString().slice(0, 10) + '.csv';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }
  </script>
</body>
</html>`;

  const blob = new Blob([html], { type: 'text/html;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  window.open(url, '_blank', 'noopener,noreferrer');
}
