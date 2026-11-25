// Builds a real Excel (.xlsx) workbook in the browser from a plain table, in the same look as the Career History report:
// a teal title banner, summary cards, a teal header row, zebra rows, a frozen header and filters on every column.
// An .xlsx file is a ZIP of XML parts; the parts are stored uncompressed, which Excel, Numbers and LibreOffice all open.

const crcTable = new Uint32Array(256).map((_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});

function crc32(bytes) {
  let crc = -1;
  for (let i = 0; i < bytes.length; i++) crc = (crc >>> 8) ^ crcTable[(crc ^ bytes[i]) & 0xff];
  return (crc ^ -1) >>> 0;
}

function zip(files) {
  const encoder = new TextEncoder();
  const chunks = [];
  const central = [];
  let offset = 0;
  for (const file of files) {
    const name = encoder.encode(file.name);
    const data = encoder.encode(file.data);
    const crc = crc32(data);

    const local = new Uint8Array(30 + name.length);
    const lv = new DataView(local.buffer);
    lv.setUint32(0, 0x04034b50, true);
    lv.setUint16(4, 20, true);
    lv.setUint16(6, 0x0800, true); // names are UTF-8
    lv.setUint16(10, 0x4a00, true);
    lv.setUint16(12, 0x5621, true);
    lv.setUint32(14, crc, true);
    lv.setUint32(18, data.length, true);
    lv.setUint32(22, data.length, true);
    lv.setUint16(26, name.length, true);
    local.set(name, 30);
    chunks.push(local, data);

    const entry = new Uint8Array(46 + name.length);
    const ev = new DataView(entry.buffer);
    ev.setUint32(0, 0x02014b50, true);
    ev.setUint16(4, 20, true);
    ev.setUint16(6, 20, true);
    ev.setUint16(8, 0x0800, true);
    ev.setUint16(12, 0x4a00, true);
    ev.setUint16(14, 0x5621, true);
    ev.setUint32(16, crc, true);
    ev.setUint32(20, data.length, true);
    ev.setUint32(24, data.length, true);
    ev.setUint16(28, name.length, true);
    ev.setUint32(42, offset, true);
    entry.set(name, 46);
    central.push(entry);
    offset += local.length + data.length;
  }
  const centralSize = central.reduce((n, c) => n + c.length, 0);
  const end = new Uint8Array(22);
  const dv = new DataView(end.buffer);
  dv.setUint32(0, 0x06054b50, true);
  dv.setUint16(8, files.length, true);
  dv.setUint16(10, files.length, true);
  dv.setUint32(12, centralSize, true);
  dv.setUint32(16, offset, true);

  const all = [...chunks, ...central, end];
  const out = new Uint8Array(all.reduce((n, c) => n + c.length, 0));
  let at = 0;
  for (const c of all) {
    out.set(c, at);
    at += c.length;
  }
  return out;
}

const esc = (value) =>
  String(value ?? '')
    // Characters XML cannot hold at all are dropped; the rest are escaped.
    .replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/g, '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');

export function columnLetter(index) {
  let letter = '';
  for (let n = index; n >= 0; n = Math.floor(n / 26) - 1) letter = String.fromCharCode((n % 26) + 65) + letter;
  return letter;
}

// ---------------------------------------------------------------------------------------------------------------------
// Styles. Fonts, fills, borders and cell formats are registered as they are used, so a colour is one line to add.
// ---------------------------------------------------------------------------------------------------------------------
const FONT = 'Segoe UI';

function createStyles() {
  const fonts = [];
  const fills = ['<fill><patternFill patternType="none"/></fill>', '<fill><patternFill patternType="gray125"/></fill>'];
  const borders = ['<border><left/><right/><top/><bottom/><diagonal/></border>'];
  const formats = [];
  const seen = { font: new Map(), fill: new Map(), border: new Map(), xf: new Map() };

  const register = (list, map, xml) => {
    if (!map.has(xml)) {
      map.set(xml, list.length);
      list.push(xml);
    }
    return map.get(xml);
  };
  const font = ({ bold, italic, size = 10, color = '1E293B' } = {}) =>
    register(fonts, seen.font, `<font>${bold ? '<b/>' : ''}${italic ? '<i/>' : ''}<sz val="${size}"/><color rgb="FF${color}"/><name val="${FONT}"/><family val="2"/></font>`);
  const fill = (color) =>
    color ? register(fills, seen.fill, `<fill><patternFill patternType="solid"><fgColor rgb="FF${color}"/><bgColor indexed="64"/></patternFill></fill>`) : 0;
  const side = (name, color) => (color ? `<${name} style="thin"><color rgb="FF${color}"/></${name}>` : `<${name}/>`);
  const border = (color) =>
    color ? register(borders, seen.border, `<border>${side('left', color)}${side('right', color)}${side('top', color)}${side('bottom', color)}<diagonal/></border>`) : 0;

  font(); // font 0 is the default
  formats.push('<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>');
  seen.xf.set('default', 0);

  /** Returns the index of a cell format, registering it the first time it is asked for. */
  const style = ({ bold, italic, size, color, background, line, h = 'left', v = 'center', wrap = false, indent = 0, numFmt = 0 } = {}) => {
    const key = JSON.stringify([bold, italic, size, color, background, line, h, v, wrap, indent, numFmt]);
    if (seen.xf.has(key)) return seen.xf.get(key);
    const id = formats.length;
    formats.push(
      `<xf numFmtId="${numFmt}" fontId="${font({ bold, italic, size, color })}" fillId="${fill(background)}" borderId="${border(line)}" xfId="0" applyFont="1"${numFmt ? ' applyNumberFormat="1"' : ''}${background ? ' applyFill="1"' : ''}${line ? ' applyBorder="1"' : ''} applyAlignment="1"><alignment horizontal="${h}" vertical="${v}"${wrap ? ' wrapText="1"' : ''}${indent ? ` indent="${indent}"` : ''}/></xf>`,
    );
    seen.xf.set(key, id);
    return id;
  };

  const xml = () => `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="${fonts.length}">${fonts.join('')}</fonts><fills count="${fills.length}">${fills.join('')}</fills><borders count="${borders.length}">${borders.join('')}</borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="${formats.length}">${formats.join('')}</cellXfs></styleSheet>`;

  return { style, xml };
}

// Colours. Each group of columns has a deep colour (the band and the title) and a pale one (the column headers).
const GROUPS = [
  { deep: '0F766E', pale: 'CCFBF1', ink: '115E59' }, // teal
  { deep: '1D4ED8', pale: 'DBEAFE', ink: '1E3A8A' }, // blue
  { deep: '7C3AED', pale: 'EDE9FE', ink: '5B21B6' }, // violet
  { deep: 'C2410C', pale: 'FFEDD5', ink: '9A3412' }, // orange
  { deep: 'BE185D', pale: 'FCE7F3', ink: '9D174D' }, // pink
  { deep: '15803D', pale: 'DCFCE7', ink: '166534' }, // green
];

// Soft fill with a darker ink, for values that repeat (department, grade, ...): the same word always gets the same colour.
const TAGS = [
  ['DBEAFE', '1E40AF'], ['DCFCE7', '166534'], ['FEF3C7', '92400E'], ['FFE4E6', '9F1239'], ['EDE9FE', '5B21B6'],
  ['CFFAFE', '155E75'], ['FFEDD5', '9A3412'], ['ECFCCB', '3F6212'], ['FCE7F3', '9D174D'], ['E0E7FF', '3730A3'],
];

export function tagColors(value) {
  const text = String(value ?? '').trim().toLowerCase();
  let hash = 0;
  for (let i = 0; i < text.length; i++) hash = (hash * 31 + text.charCodeAt(i)) >>> 0;
  return TAGS[hash % TAGS.length];
}

const STATUS = {
  ACTIVE: ['DCFCE7', '166534'],
  PROBATION: ['FEF3C7', '92400E'],
  ON_LEAVE: ['FEF3C7', '92400E'],
  NOTICE: ['FFEDD5', '9A3412'],
  // Issue statuses, for the issues report.
  TODO: ['F1F5F9', '475569'],
  IN_PROGRESS: ['DBEAFE', '1E40AF'],
  TESTING: ['FEF3C7', '92400E'],
  DONE: ['DCFCE7', '166534'],
  CANCELLED: ['F1F5F9', '64748B'],
};
// A code may arrive as IN_PROGRESS or as the words, IN PROGRESS.
const statusColors = (value) => STATUS[String(value ?? '').trim().toUpperCase().replace(/\s+/g, '_')] || ['FEE2E2', '991B1B'];

const CARDS = ['0F766E', '15803D', '1D4ED8', '7C3AED'];
const LINE = 'E2E8F0';

// ---------------------------------------------------------------------------------------------------------------------
// One worksheet
// ---------------------------------------------------------------------------------------------------------------------
function createSheetWriter() {
  const strings = [];
  const index = new Map();
  const sst = (text) => {
    const key = String(text ?? '');
    if (!index.has(key)) {
      index.set(key, strings.length);
      strings.push(key);
    }
    return index.get(key);
  };
  const text = (ref, s, value) => `<c r="${ref}" s="${s}" t="s"><v>${sst(value)}</v></c>`;
  const blank = (ref, s) => `<c r="${ref}" s="${s}"/>`;

  function sheetXml({ rows: body, columns, merges, freeze, filter, tab, selected }) {
    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  ${tab ? `<sheetPr><tabColor rgb="FF${tab}"/><pageSetUpPr fitToPage="1"/></sheetPr>` : ''}
  <sheetViews><sheetView ${selected ? 'tabSelected="1" ' : ''}workbookViewId="0" showGridLines="0">${freeze ? `<pane xSplit="${freeze.x}" ySplit="${freeze.y}" topLeftCell="${columnLetter(freeze.x)}${freeze.y + 1}" activePane="bottomRight" state="frozen"/>` : ''}</sheetView></sheetViews>
  <cols>${columns.map((c, i) => `<col min="${i + 1}" max="${i + 1}" width="${c.width || 18}" customWidth="1"/>`).join('')}</cols>
  <sheetData>${body}</sheetData>
  ${filter ? `<autoFilter ref="${filter}"/>` : ''}
  ${merges.length ? `<mergeCells count="${merges.length}">${merges.map((m) => `<mergeCell ref="${m}"/>`).join('')}</mergeCells>` : ''}
  <pageMargins left="0.4" right="0.4" top="0.5" bottom="0.5" header="0.3" footer="0.3"/>
  <pageSetup orientation="landscape" fitToWidth="1" fitToHeight="0"/>
</worksheet>`;
  }

  return { sst, text, blank, sheetXml, strings };
}

function banner(w, styles, { title, subtitle, width, state }) {
  if (!title && !subtitle) return '';
  const last = columnLetter(width - 1);
  const big = styles.style({ bold: true, size: 18, color: 'FFFFFF', background: GROUPS[0].deep, indent: 1 });
  const small = styles.style({ italic: true, size: 10, color: '134E4A', background: GROUPS[0].pale, indent: 1 });
  const fillRow = (r, s, label) => {
    let cells = label ? w.text(`A${r}`, s, label) : w.blank(`A${r}`, s);
    for (let c = 1; c < width; c++) cells += w.blank(`${columnLetter(c)}${r}`, s);
    return cells;
  };
  let out = `<row r="${state.r}" ht="44" customHeight="1">${fillRow(state.r, big, title)}</row>`;
  if (width > 1) state.merges.push(`A${state.r}:${last}${state.r}`);
  state.r++;
  if (subtitle) {
    out += `<row r="${state.r}" ht="22" customHeight="1">${fillRow(state.r, small, subtitle)}</row>`;
    if (width > 1) state.merges.push(`A${state.r}:${last}${state.r}`);
    state.r++;
  }
  out += `<row r="${state.r}" ht="8" customHeight="1"/>`;
  state.r++;
  return out;
}

function summaryCards(w, styles, { summary, width, state }) {
  if (!summary.length) return '';
  const perRow = Math.min(4, summary.length);
  const span = Math.max(Math.floor(width / perRow), 2);
  let out = '';
  for (let i = 0; i < summary.length; i += perRow) {
    let cells = '';
    let at = 0;
    summary.slice(i, i + perRow).forEach((item, k, all) => {
      const end = k === all.length - 1 ? Math.max(width, at + 2) : at + span;
      const accent = CARDS[(i + k) % CARDS.length];
      const label = styles.style({ bold: true, size: 10, color: 'FFFFFF', background: accent, h: 'center', indent: 0, wrap: true });
      const value = styles.style({ bold: true, size: 16, color: accent, background: 'F8FAFC', line: accent, h: 'center' });
      cells += w.text(`${columnLetter(at)}${state.r}`, label, item.label);
      cells += w.text(`${columnLetter(at + 1)}${state.r}`, value, item.value);
      for (let c = at + 2; c < end; c++) cells += w.blank(`${columnLetter(c)}${state.r}`, value);
      if (end - at > 2) state.merges.push(`${columnLetter(at + 1)}${state.r}:${columnLetter(end - 1)}${state.r}`);
      at = end;
    });
    out += `<row r="${state.r}" ht="38" customHeight="1">${cells}</row>`;
    state.r++;
  }
  out += `<row r="${state.r}" ht="12" customHeight="1"/>`;
  state.r++;
  return out;
}

function tableSheet(w, styles, spec) {
  const columns = spec.columns || [];
  const rows = spec.rows || [];
  const width = Math.max(columns.length, 1);
  const state = { r: 1, merges: [] };
  let body = banner(w, styles, { title: spec.title || '', subtitle: spec.subtitle, width, state });
  body += summaryCards(w, styles, { summary: spec.summary || [], width, state });

  // Colour per group of columns, in the order the groups first appear.
  const order = [];
  const colorOf = (group) => {
    if (!order.includes(group)) order.push(group);
    return GROUPS[order.indexOf(group) % GROUPS.length];
  };
  const hasGroups = columns.some((c) => c.group);

  if (hasGroups) {
    let cells = '';
    let i = 0;
    while (i < columns.length) {
      let j = i;
      while (j + 1 < columns.length && (columns[j + 1].group || '') === (columns[i].group || '')) j++;
      const color = colorOf(columns[i].group || '');
      const s = styles.style({ bold: true, size: 11, color: 'FFFFFF', background: color.deep, line: 'FFFFFF', h: 'center' });
      cells += w.text(`${columnLetter(i)}${state.r}`, s, (columns[i].group || '').toUpperCase());
      for (let c = i + 1; c <= j; c++) cells += w.blank(`${columnLetter(c)}${state.r}`, s);
      if (j > i) state.merges.push(`${columnLetter(i)}${state.r}:${columnLetter(j)}${state.r}`);
      i = j + 1;
    }
    body += `<row r="${state.r}" ht="24" customHeight="1">${cells}</row>`;
    state.r++;
  }

  const headerRow = state.r;
  body += `<row r="${headerRow}" ht="34" customHeight="1">${columns
    .map((c, i) => {
      const color = colorOf(c.group || '');
      const s = styles.style({ bold: true, size: 11, color: color.ink, background: color.pale, line: color.deep, h: 'center', wrap: true });
      return w.text(`${columnLetter(i)}${headerRow}`, s, c.header);
    })
    .join('')}</row>`;
  state.r++;

  // Tall enough for the longest cell in the row: wrapped text is counted line by line at the column's width.
  const linesIn = (value, col) => {
    if (value === null || value === undefined || value === '' || typeof value === 'number') return 1;
    const chars = Math.max(((col.width || 18) - 2) * 0.95, 4);
    return String(value)
      .split('\n')
      .reduce((n, part) => n + Math.max(1, Math.ceil(part.length / chars)), 0);
  };

  rows.forEach((row, n) => {
    const band = n % 2 === 0 ? 'FFFFFF' : 'F8FAFC';
    const lines = Math.max(1, ...columns.map((col, i) => (col.type === 'list' || col.type === 'text' ? linesIn(row[i], col) : 1)));
    const height = Math.max(24, lines * 15 + 8);
    const cells = columns
      .map((col, i) => {
        const ref = `${columnLetter(i)}${state.r}`;
        const value = row[i];
        const empty = value === null || value === undefined || value === '';
        const plain = { background: band, line: LINE, v: 'center' };
        if (col.type === 'number') {
          // 4 is #,##0.00 (hours, amounts); 3 is a whole number.
          const s = styles.style({ ...plain, numFmt: col.decimals ? 4 : 3, h: 'right', color: '166534', bold: true, indent: 1 });
          return typeof value === 'number' && Number.isFinite(value) ? `<c r="${ref}" s="${s}"><v>${value}</v></c>` : w.blank(ref, s);
        }
        if (col.type === 'code') return text(styles.style({ ...plain, bold: true, color: '0D9488', h: 'center' }));
        if (col.type === 'strong') return text(styles.style({ ...plain, bold: true, size: 11, color: '0F172A', indent: 1 }));
        if (col.type === 'tag' || col.type === 'status') {
          if (empty) return w.blank(ref, styles.style({ ...plain }));
          const [fillColor, ink] = col.type === 'status' ? statusColors(value) : tagColors(value);
          return text(styles.style({ background: fillColor, color: ink, line: 'FFFFFF', bold: true, h: 'center', v: 'center', wrap: true }));
        }
        if (col.type === 'list') return text(styles.style({ ...plain, v: 'top', wrap: true, indent: 1 }));
        return text(styles.style({ ...plain, wrap: true, indent: 1 }));

        function text(s) {
          return empty ? w.blank(ref, s) : w.text(ref, s, value);
        }
      })
      .join('');
    body += `<row r="${state.r}" ht="${height}" customHeight="1">${cells}</row>`;
    state.r++;
  });

  const lastRow = Math.max(state.r - 1, headerRow);
  return w.sheetXml({
    rows: body,
    columns,
    merges: state.merges,
    freeze: null,
    selected: true,
    filter: `A${headerRow}:${columnLetter(width - 1)}${lastRow}`,
    tab: GROUPS[0].deep,
  });
}

/** How many rows share each value of a column, largest first, with a bar. Blank values count as "Not set". The unit defaults to Employees. */
function breakdownSheet(w, styles, spec) {
  const names = (spec.columns || []).map((c) => c.header);
  const wanted = (spec.breakdowns || []).filter((b) => names.includes(b));
  if (!wanted.length) return null;

  const columns = [{ width: 36 }, { width: 14 }, { width: 11 }, { width: 36 }];
  const state = { r: 1, merges: [] };
  const total = (spec.rows || []).length;
  const unit = spec.unit || 'Employees';
  let body = banner(w, styles, { title: `Summary · ${spec.breakdownTitle || 'Headcount at a glance'}`, subtitle: `${total} ${unit.toLowerCase()}`, width: 4, state });

  wanted.forEach((name, n) => {
    const color = GROUPS[n % GROUPS.length];
    const at = names.indexOf(name);
    const counts = new Map();
    for (const row of spec.rows) {
      const v = row[at] === null || row[at] === undefined || row[at] === '' ? 'Not set' : String(row[at]);
      counts.set(v, (counts.get(v) || 0) + 1);
    }

    const band = styles.style({ bold: true, size: 11, color: 'FFFFFF', background: color.deep, indent: 1 });
    body += `<row r="${state.r}" ht="24" customHeight="1">${w.text(`A${state.r}`, band, `BY ${name.toUpperCase()}`)}${['B', 'C', 'D'].map((c) => w.blank(`${c}${state.r}`, band)).join('')}</row>`;
    state.merges.push(`A${state.r}:D${state.r}`);
    state.r++;

    const head = styles.style({ bold: true, color: color.ink, background: color.pale, line: color.deep, h: 'center' });
    body += `<row r="${state.r}" ht="22" customHeight="1">${['', unit, 'Share', ''].map((h, k) => (k === 0 ? w.text(`A${state.r}`, head, name) : w.text(`${'ABCD'[k]}${state.r}`, head, h))).join('')}</row>`;
    state.r++;

    [...counts.entries()]
      .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
      .forEach(([value, count]) => {
        const share = total ? count / total : 0;
        const [fillColor, ink] = value === 'Not set' ? ['F1F5F9', '64748B'] : tagColors(value);
        const label = styles.style({ background: fillColor, color: ink, bold: true, line: 'FFFFFF', indent: 1 });
        const number = styles.style({ bold: true, color: '0F172A', line: LINE, h: 'center' });
        const percent = styles.style({ color: '475569', line: LINE, h: 'center' });
        const bar = styles.style({ color: color.deep, line: LINE, size: 11 });
        body += `<row r="${state.r}" ht="22" customHeight="1">${w.text(`A${state.r}`, label, value)}<c r="B${state.r}" s="${number}"><v>${count}</v></c>${w.text(`C${state.r}`, percent, `${Math.round(share * 100)}%`)}${w.text(`D${state.r}`, bar, '█'.repeat(Math.max(1, Math.round(share * 24))))}</row>`;
        state.r++;
      });
    body += `<row r="${state.r}" ht="14" customHeight="1"/>`;
    state.r++;
  });

  return w.sheetXml({ rows: body, columns, merges: state.merges, freeze: null, filter: null, tab: GROUPS[1].deep });
}

/**
 * @param {{ title?: string, subtitle?: string, sheetName?: string, summary?: {label:string,value:string}[],
 *           columns: {header:string,width?:number,type?:'text'|'list'|'number'|'code'|'strong'|'tag'|'status',group?:string,decimals?:boolean}[],
 *           rows: unknown[][], breakdowns?: string[], unit?: string, breakdownTitle?: string }} spec
 * @returns {Uint8Array} the .xlsx file
 */
export function buildXlsx(spec) {
  const styles = createStyles();
  const w = createSheetWriter();
  const sheets = [{ name: spec.sheetName || 'Report', xml: tableSheet(w, styles, spec) }];
  const summary = breakdownSheet(w, styles, spec);
  if (summary) sheets.push({ name: 'Summary', xml: summary });

  const sharedStrings = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="${w.strings.length}" uniqueCount="${w.strings.length}">${w.strings
    .map((s) => `<si><t xml:space="preserve">${esc(s)}</t></si>`)
    .join('')}</sst>`;

  const NS = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships';
  const cleanName = (name) => esc(String(name).replace(/[\\/?*[\]:]/g, '').slice(0, 31) || 'Sheet');
  return zip([
    {
      name: '[Content_Types].xml',
      data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>${sheets
        .map((_, i) => `<Override PartName="/xl/worksheets/sheet${i + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>`)
        .join('')}<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/><Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/></Types>`,
    },
    {
      name: '_rels/.rels',
      data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="${NS}/officeDocument" Target="xl/workbook.xml"/></Relationships>`,
    },
    {
      name: 'xl/_rels/workbook.xml.rels',
      data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">${sheets
        .map((_, i) => `<Relationship Id="rId${i + 1}" Type="${NS}/worksheet" Target="worksheets/sheet${i + 1}.xml"/>`)
        .join('')}<Relationship Id="rId${sheets.length + 1}" Type="${NS}/styles" Target="styles.xml"/><Relationship Id="rId${sheets.length + 2}" Type="${NS}/sharedStrings" Target="sharedStrings.xml"/></Relationships>`,
    },
    {
      name: 'xl/workbook.xml',
      data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="${NS}"><sheets>${sheets
        .map((sheet, i) => `<sheet name="${cleanName(sheet.name)}" sheetId="${i + 1}" r:id="rId${i + 1}"/>`)
        .join('')}</sheets></workbook>`,
    },
    { name: 'xl/styles.xml', data: styles.xml() },
    { name: 'xl/sharedStrings.xml', data: sharedStrings },
    ...sheets.map((sheet, i) => ({ name: `xl/worksheets/sheet${i + 1}.xml`, data: sheet.xml })),
  ]);
}

export const XLSX_TYPE = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/** Turns the table a tool sent (as JSON text) into an .xlsx Blob; null when the table cannot be read. */
export function xlsxBlobFromTable(content) {
  let spec;
  try {
    spec = typeof content === 'string' ? JSON.parse(content) : content;
  } catch {
    return null;
  }
  if (!spec || !Array.isArray(spec.columns) || !Array.isArray(spec.rows)) return null;
  return new Blob([buildXlsx(spec)], { type: XLSX_TYPE });
}
