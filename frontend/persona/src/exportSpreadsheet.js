/**
 * Native OpenXML Excel (.xlsx) Workbook Generator in pure JavaScript.
 * Produces 100% genuine Office Open XML binary ZIP archives (.xlsx)
 * featuring:
 *  - Unified executive styling with clean, light, elegant borders (no harsh dark bars)
 *  - Seamless transition column with floating ➔ arrow (no disjointed box lines)
 *  - Separate cells for Employee Name, Employee ID, and Department
 *  - Active filter on Employee Name, ID, and Department
 *  - Pure blank row gaps between cards with zero borders
 *  - Multi-tab workbook (Visual Cards & Tabular Search)
 */

// CRC32 Lookup Table
const crcTable = new Uint32Array(256);
for (let n = 0; n < 256; n++) {
  let c = n;
  for (let k = 0; k < 8; k++) {
    c = (c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1);
  }
  crcTable[n] = c;
}

function computeCrc32(uint8Array) {
  let crc = 0 ^ (-1);
  for (let i = 0; i < uint8Array.length; i++) {
    crc = (crc >>> 8) ^ crcTable[(crc ^ uint8Array[i]) & 0xFF];
  }
  return (crc ^ (-1)) >>> 0;
}

function createZipArchive(files) {
  const encoder = new TextEncoder();
  const fileEntries = [];
  let offset = 0;

  for (const file of files) {
    const nameBytes = encoder.encode(file.name);
    const dataBytes = typeof file.data === 'string' ? encoder.encode(file.data) : file.data;
    const crc = computeCrc32(dataBytes);
    const size = dataBytes.length;

    // 30-byte local file header
    const localHeader = new Uint8Array(30);
    const view = new DataView(localHeader.buffer);
    view.setUint32(0, 0x04034b50, true); // Local file header signature
    view.setUint16(4, 20, true);         // Version needed (2.0)
    view.setUint16(6, 0, true);          // General purpose bit flag
    view.setUint16(8, 0, true);          // Compression method: 0 (STORE / uncompressed)
    view.setUint16(10, 0x4a00, true);    // File last mod time
    view.setUint16(12, 0x5621, true);    // File last mod date
    view.setUint32(14, crc, true);       // CRC-32
    view.setUint32(18, size, true);      // Compressed size
    view.setUint32(22, size, true);      // Uncompressed size
    view.setUint16(26, nameBytes.length, true); // File name length
    view.setUint16(28, 0, true);         // Extra field length

    // Combine header + name + data
    const chunk = new Uint8Array(localHeader.length + nameBytes.length + dataBytes.length);
    chunk.set(localHeader, 0);
    chunk.set(nameBytes, localHeader.length);
    chunk.set(dataBytes, localHeader.length + nameBytes.length);

    fileEntries.push({
      nameBytes,
      crc,
      size,
      offset,
      chunk
    });
    offset += chunk.length;
  }

  // Central Directory Entries
  const cdChunks = [];
  let centralDirSize = 0;

  for (const entry of fileEntries) {
    const cdHeader = new Uint8Array(46);
    const view = new DataView(cdHeader.buffer);
    view.setUint32(0, 0x02014b50, true); // Central file header signature
    view.setUint16(4, 20, true);         // Version made by (2.0)
    view.setUint16(6, 20, true);         // Version needed to extract (2.0)
    view.setUint16(8, 0, true);          // General purpose bit flag
    view.setUint16(10, 0, true);         // Compression method: 0
    view.setUint16(12, 0x4a00, true);    // Last mod file time
    view.setUint16(14, 0x5621, true);    // Last mod file date
    view.setUint32(16, entry.crc, true); // CRC-32
    view.setUint32(20, entry.size, true);// Compressed size
    view.setUint32(24, entry.size, true);// Uncompressed size
    view.setUint16(28, entry.nameBytes.length, true); // File name length
    view.setUint16(30, 0, true);         // Extra field length
    view.setUint16(32, 0, true);         // File comment length
    view.setUint16(34, 0, true);         // Disk number where file starts
    view.setUint16(36, 0, true);         // Internal file attributes
    view.setUint32(38, 0, true);         // External file attributes
    view.setUint32(42, entry.offset, true); // Relative offset of local file header

    const cdChunk = new Uint8Array(cdHeader.length + entry.nameBytes.length);
    cdChunk.set(cdHeader, 0);
    cdChunk.set(entry.nameBytes, cdHeader.length);

    cdChunks.push(cdChunk);
    centralDirSize += cdChunk.length;
  }

  // End of Central Directory Record (22 bytes)
  const eocd = new Uint8Array(22);
  const eocdView = new DataView(eocd.buffer);
  eocdView.setUint32(0, 0x06054b50, true); // EOCD signature
  eocdView.setUint16(4, 0, true);          // Number of this disk
  eocdView.setUint16(6, 0, true);          // Disk where central directory starts
  eocdView.setUint16(8, fileEntries.length, true);  // Number of central directory records on this disk
  eocdView.setUint16(10, fileEntries.length, true); // Total number of central directory records
  eocdView.setUint32(12, centralDirSize, true);     // Size of central directory
  eocdView.setUint32(16, offset, true);             // Offset of start of central directory
  eocdView.setUint16(20, 0, true);                  // Comment length

  // Assemble full zip
  const totalLength = offset + centralDirSize + eocd.length;
  const finalZip = new Uint8Array(totalLength);

  let currentPos = 0;
  for (const entry of fileEntries) {
    finalZip.set(entry.chunk, currentPos);
    currentPos += entry.chunk.length;
  }
  for (const cd of cdChunks) {
    finalZip.set(cd, currentPos);
    currentPos += cd.length;
  }
  finalZip.set(eocd, currentPos);

  return finalZip;
}

function escapeXml(unsafe) {
  if (unsafe === null || unsafe === undefined) return '';
  return String(unsafe)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

function colToLetter(colIndex) {
  let letter = '';
  let temp = colIndex;
  while (temp >= 0) {
    letter = String.fromCharCode((temp % 26) + 65) + letter;
    temp = Math.floor(temp / 26) - 1;
  }
  return letter;
}

function cleanDuration(str) {
  if (!str) return '—';
  let s = String(str)
    .replace(/\s*ago$/i, '')
    .replace(/^about\s+/i, '')
    .trim();

  s = s.replace(/0\s*years?(\(s\))?,\s*/gi, '');
  s = s.replace(/,\s*0\s*months?(\(s\))?/gi, '');
  s = s.replace(/1\s*year\(s\)/gi, '1 year');
  s = s.replace(/(\d+)\s*year\(s\)/gi, '$1 years');
  s = s.replace(/1\s*month\(s\)/gi, '1 month');
  s = s.replace(/(\d+)\s*month\(s\)/gi, '$1 months');
  s = s.replace(/1\s*day\(s\)/gi, '1 day');
  s = s.replace(/(\d+)\s*day\(s\)/gi, '$1 days');

  if (!s || s === '0 month(s)' || s === '0 months') return 'Less than 1 month';
  return s;
}

/**
 * Exports data to a genuine multi-tab OpenXML Excel (.xlsx) file.
 */
export function exportToExcelSpreadsheet({
  data = [],
  columns = [],
  filename = 'Employee_Career_Report.xlsx',
  sheetName = 'Career Transitions',
  title = '',
  metadata = [],
  cards = []
}) {
  const sharedStrings = [];
  const stringMap = new Map();

  function addString(str) {
    const s = (str === null || str === undefined) ? '' : String(str);
    if (stringMap.has(s)) {
      return stringMap.get(s);
    }
    const idx = sharedStrings.length;
    sharedStrings.push(s);
    stringMap.set(s, idx);
    return idx;
  }

  const numCols = Math.max(9, (columns && columns.length > 0 ? columns.length : 9));
  const endColLetter = colToLetter(numCols - 1);

  // ==========================================
  // SHEET 1: VISUAL CAREER TRANSITION CARDS
  // ==========================================
  let s1RowNum = 1;
  let s1RowXml = '';
  const s1MergeCells = [];
  let s1FilterStartRow = 0;
  let s1FilterEndRow = 0;

  // 1. Executive Title Banner
  if (title) {
    const titleIdx = addString(title);
    let titleCells = `<c r="A${s1RowNum}" s="1" t="s"><v>${titleIdx}</v></c>`;
    for (let c = 1; c < numCols; c++) {
      titleCells += `<c r="${colToLetter(c)}${s1RowNum}" s="1"/>`;
    }
    s1RowXml += `<row r="${s1RowNum}" ht="38" customHeight="1">${titleCells}</row>`;
    s1MergeCells.push(`A${s1RowNum}:${endColLetter}${s1RowNum}`);
    s1RowNum++;

    // Spacer below title
    s1RowXml += `<row r="${s1RowNum}" ht="8" customHeight="1"/>`;
    s1RowNum++;
  }

  // 2. Executive Stat Summary Cards
  if (metadata && metadata.length > 0) {
    const itemsPerRow = Math.min(4, metadata.length);
    const colsPerCard = Math.floor(numCols / itemsPerRow);

    for (let i = 0; i < metadata.length; i += itemsPerRow) {
      const rowItems = metadata.slice(i, i + itemsPerRow);
      let cardCells = '';
      let colTracker = 0;

      rowItems.forEach((item, cardIdx) => {
        const isLastCard = (cardIdx === rowItems.length - 1);
        const cardColSpan = isLastCard ? (numCols - colTracker) : colsPerCard;

        const labelCol = colToLetter(colTracker);
        const valStartCol = colToLetter(colTracker + 1);
        const valEndCol = colToLetter(colTracker + cardColSpan - 1);

        const labelIdx = addString(item.label);
        const valIdx = addString(item.value);

        cardCells += `<c r="${labelCol}${s1RowNum}" s="2" t="s"><v>${labelIdx}</v></c>`;
        cardCells += `<c r="${valStartCol}${s1RowNum}" s="3" t="s"><v>${valIdx}</v></c>`;

        for (let c = colTracker + 2; c < colTracker + cardColSpan; c++) {
          cardCells += `<c r="${colToLetter(c)}${s1RowNum}" s="3"/>`;
        }

        if (cardColSpan > 2) {
          s1MergeCells.push(`${valStartCol}${s1RowNum}:${valEndCol}${s1RowNum}`);
        }

        colTracker += cardColSpan;
      });

      s1RowXml += `<row r="${s1RowNum}" ht="26" customHeight="1">${cardCells}</row>`;
      s1RowNum++;
    }

    // Spacer below stat cards
    s1RowXml += `<row r="${s1RowNum}" ht="10" customHeight="1"/>`;
    s1RowNum++;
  }

  // 3. Search & Filter Column Header Bar (Unified Deep Teal across all cols)
  s1FilterStartRow = s1RowNum;
  const fNameIdx = addString('Employee Name');
  const fIdIdx = addString('Employee ID');
  const fDeptIdx = addString('Department');
  const fPrevIdx = addString('Previous Working Details');
  const fCurrIdx = addString('Current Working Details');

  let filterBarCells = '';
  // Col A: Employee Name
  filterBarCells += `<c r="A${s1RowNum}" s="4" t="s"><v>${fNameIdx}</v></c>`;
  // Col B: Employee ID
  filterBarCells += `<c r="B${s1RowNum}" s="4" t="s"><v>${fIdIdx}</v></c>`;
  // Col C: Department
  filterBarCells += `<c r="C${s1RowNum}" s="4" t="s"><v>${fDeptIdx}</v></c>`;

  // Cols D-E: Previous Working Details Header
  filterBarCells += `<c r="D${s1RowNum}" s="5" t="s"><v>${fPrevIdx}</v></c>`;
  filterBarCells += `<c r="E${s1RowNum}" s="5"/>`;
  s1MergeCells.push(`D${s1RowNum}:E${s1RowNum}`);

  // Col F: Transition Gap (Unified Header Style)
  filterBarCells += `<c r="F${s1RowNum}" s="4"/>`;

  // Cols G-I: Current Working Details Header
  filterBarCells += `<c r="G${s1RowNum}" s="6" t="s"><v>${fCurrIdx}</v></c>`;
  filterBarCells += `<c r="H${s1RowNum}" s="6"/><c r="I${s1RowNum}" s="6"/>`;
  s1MergeCells.push(`G${s1RowNum}:I${s1RowNum}`);

  s1RowXml += `<row r="${s1RowNum}" ht="28" customHeight="1">${filterBarCells}</row>`;
  s1RowNum++;

  // Spacer below filter header
  s1RowXml += `<row r="${s1RowNum}" ht="8" customHeight="1"/>`;
  s1RowNum++;

  // 4. Career Transition Comparison Cards
  if (cards && cards.length > 0) {
    cards.forEach((c) => {
      const empName = c.empName || 'Employee';
      const empCode = c.empCode ? `#${c.empCode}` : '—';
      const dept = c.dept || 'Developer';
      const isSingle = !c.previous || c.isSingle;

      const nameIdx = addString(empName);
      const codeIdx = addString(empCode);
      const deptIdx = addString(dept);

      if (isSingle) {
        // ==========================================
        // SINGLE POSITION EMPLOYEE CARD (Cols D-I)
        // 3 Rows: Header, Grade & Period, Job Title & Period Length
        // ==========================================
        const curr = c.current || { grade: '—', jobTitle: '—', department: dept, periodStart: '—', periodEnd: 'Present', duration: '—' };
        const currHeaderIdx = addString('💼 Current Working Details');
        const startRow = s1RowNum;
        const endRow = s1RowNum + 2;

        // Row 1: Header Row
        let hCells = `<c r="A${s1RowNum}" s="10" t="s"><v>${nameIdx}</v></c>`;
        hCells += `<c r="B${s1RowNum}" s="11" t="s"><v>${codeIdx}</v></c>`;
        hCells += `<c r="C${s1RowNum}" s="12" t="s"><v>${deptIdx}</v></c>`;

        hCells += `<c r="D${s1RowNum}" s="24" t="s"><v>${currHeaderIdx}</v></c>`;
        hCells += `<c r="E${s1RowNum}" s="25"/><c r="F${s1RowNum}" s="25"/><c r="G${s1RowNum}" s="25"/><c r="H${s1RowNum}" s="25"/><c r="I${s1RowNum}" s="26"/>`;
        s1MergeCells.push(`D${s1RowNum}:I${s1RowNum}`);

        s1RowXml += `<row r="${s1RowNum}" ht="26" customHeight="1">${hCells}</row>`;
        s1RowNum++;

        // Row 2: Grade & Period
        const gradeLblIdx = addString('Grade:');
        const gradeValIdx = addString(curr.grade || '—');
        const periodLblIdx = addString('Period:');
        const periodValIdx = addString(`${curr.periodStart} → ${curr.periodEnd}`);

        let r1 = `<c r="A${s1RowNum}" s="13"/><c r="B${s1RowNum}" s="14"/><c r="C${s1RowNum}" s="15"/>`;

        r1 += `<c r="D${s1RowNum}" s="27" t="s"><v>${gradeLblIdx}</v></c>`;
        r1 += `<c r="E${s1RowNum}" s="28" t="s"><v>${gradeValIdx}</v></c>`;
        r1 += `<c r="F${s1RowNum}" s="28"/>`;
        s1MergeCells.push(`E${s1RowNum}:F${s1RowNum}`);

        r1 += `<c r="G${s1RowNum}" s="28" t="s"><v>${periodLblIdx}</v></c>`;
        r1 += `<c r="H${s1RowNum}" s="28" t="s"><v>${periodValIdx}</v></c>`;
        r1 += `<c r="I${s1RowNum}" s="29"/>`;
        s1MergeCells.push(`H${s1RowNum}:I${s1RowNum}`);

        s1RowXml += `<row r="${s1RowNum}" ht="23" customHeight="1">${r1}</row>`;
        s1RowNum++;

        // Row 3: Job Title & Period Length (Bottom Closed Row)
        const titleLblIdx = addString('Job Title:');
        const titleValIdx = addString(curr.jobTitle || '—');
        const lenLblIdx = addString('Period Length:');
        const lenValIdx = addString(cleanDuration(curr.duration));

        let r2 = `<c r="A${s1RowNum}" s="16"/><c r="B${s1RowNum}" s="17"/><c r="C${s1RowNum}" s="18"/>`;

        r2 += `<c r="D${s1RowNum}" s="30" t="s"><v>${titleLblIdx}</v></c>`;
        r2 += `<c r="E${s1RowNum}" s="31" t="s"><v>${titleValIdx}</v></c>`;
        r2 += `<c r="F${s1RowNum}" s="31"/>`;
        s1MergeCells.push(`E${s1RowNum}:F${s1RowNum}`);

        r2 += `<c r="G${s1RowNum}" s="31" t="s"><v>${lenLblIdx}</v></c>`;
        r2 += `<c r="H${s1RowNum}" s="31" t="s"><v>${lenValIdx}</v></c>`;
        r2 += `<c r="I${s1RowNum}" s="32"/>`;
        s1MergeCells.push(`H${s1RowNum}:I${s1RowNum}`);

        s1RowXml += `<row r="${s1RowNum}" ht="23" customHeight="1">${r2}</row>`;
        s1RowNum++;

        // Vertically merge Col A, Col B, Col C
        s1MergeCells.push(`A${startRow}:A${endRow}`);
        s1MergeCells.push(`B${startRow}:B${endRow}`);
        s1MergeCells.push(`C${startRow}:C${endRow}`);

      } else {
        // ==========================================
        // TWO-PERIOD COMPARISON TRANSITION CARD
        // 5 Rows: Header, Grade, Job Title (with arrow), Period, Period Length
        // ==========================================
        const prev = c.previous;
        const curr = c.current;

        const prevHeaderIdx = addString('🕒 Previous Working Details');
        const currHeaderIdx = addString('💼 Current Working Details');
        const startRow = s1RowNum;
        const endRow = s1RowNum + 4;

        // Row 1: Header Row
        let hCells = `<c r="A${s1RowNum}" s="10" t="s"><v>${nameIdx}</v></c>`;
        hCells += `<c r="B${s1RowNum}" s="11" t="s"><v>${codeIdx}</v></c>`;
        hCells += `<c r="C${s1RowNum}" s="12" t="s"><v>${deptIdx}</v></c>`;

        hCells += `<c r="D${s1RowNum}" s="19" t="s"><v>${prevHeaderIdx}</v></c>`;
        hCells += `<c r="E${s1RowNum}" s="20"/>`;
        s1MergeCells.push(`D${s1RowNum}:E${s1RowNum}`);

        hCells += `<c r="F${s1RowNum}" s="0"/>`; // Zero-border clean spacer

        hCells += `<c r="G${s1RowNum}" s="24" t="s"><v>${currHeaderIdx}</v></c>`;
        hCells += `<c r="H${s1RowNum}" s="25"/><c r="I${s1RowNum}" s="26"/>`;
        s1MergeCells.push(`G${s1RowNum}:I${s1RowNum}`);

        s1RowXml += `<row r="${s1RowNum}" ht="26" customHeight="1">${hCells}</row>`;
        s1RowNum++;

        // Row 2: Grade
        const gradeLblIdx = addString('Grade:');
        const prevGradeValIdx = addString(prev.grade || '—');
        const currGradeValIdx = addString(curr.grade || '—');

        let r1 = `<c r="A${s1RowNum}" s="13"/><c r="B${s1RowNum}" s="14"/><c r="C${s1RowNum}" s="15"/>`;

        r1 += `<c r="D${s1RowNum}" s="21" t="s"><v>${gradeLblIdx}</v></c>`;
        r1 += `<c r="E${s1RowNum}" s="22" t="s"><v>${prevGradeValIdx}</v></c>`;
        r1 += `<c r="F${s1RowNum}" s="0"/>`; // Zero-border clean spacer

        r1 += `<c r="G${s1RowNum}" s="27" t="s"><v>${gradeLblIdx}</v></c>`;
        r1 += `<c r="H${s1RowNum}" s="28" t="s"><v>${currGradeValIdx}</v></c>`;
        r1 += `<c r="I${s1RowNum}" s="29"/>`;
        s1MergeCells.push(`H${s1RowNum}:I${s1RowNum}`);

        s1RowXml += `<row r="${s1RowNum}" ht="22" customHeight="1">${r1}</row>`;
        s1RowNum++;

        // Row 3: Job Title (With Floating Centered Arrow ➔ in Col F)
        const titleLblIdx = addString('Job Title:');
        const prevTitleValIdx = addString(prev.jobTitle || '—');
        const currTitleValIdx = addString(curr.jobTitle || '—');
        const arrowIdx = addString('➔');

        let r2 = `<c r="A${s1RowNum}" s="13"/><c r="B${s1RowNum}" s="14"/><c r="C${s1RowNum}" s="15"/>`;

        r2 += `<c r="D${s1RowNum}" s="21" t="s"><v>${titleLblIdx}</v></c>`;
        r2 += `<c r="E${s1RowNum}" s="22" t="s"><v>${prevTitleValIdx}</v></c>`;
        r2 += `<c r="F${s1RowNum}" s="9" t="s"><v>${arrowIdx}</v></c>`; // Centered floating arrow, NO borders!

        r2 += `<c r="G${s1RowNum}" s="27" t="s"><v>${titleLblIdx}</v></c>`;
        r2 += `<c r="H${s1RowNum}" s="28" t="s"><v>${currTitleValIdx}</v></c>`;
        r2 += `<c r="I${s1RowNum}" s="29"/>`;
        s1MergeCells.push(`H${s1RowNum}:I${s1RowNum}`);

        s1RowXml += `<row r="${s1RowNum}" ht="22" customHeight="1">${r2}</row>`;
        s1RowNum++;

        // Row 4: Period
        const periodLblIdx = addString('Period:');
        const prevPeriodValIdx = addString(`${prev.periodStart} → ${prev.periodEnd}`);
        const currPeriodValIdx = addString(`${curr.periodStart} → ${curr.periodEnd}`);

        let r3 = `<c r="A${s1RowNum}" s="13"/><c r="B${s1RowNum}" s="14"/><c r="C${s1RowNum}" s="15"/>`;

        r3 += `<c r="D${s1RowNum}" s="21" t="s"><v>${periodLblIdx}</v></c>`;
        r3 += `<c r="E${s1RowNum}" s="22" t="s"><v>${prevPeriodValIdx}</v></c>`;
        r3 += `<c r="F${s1RowNum}" s="0"/>`; // Zero-border clean spacer

        r3 += `<c r="G${s1RowNum}" s="27" t="s"><v>${periodLblIdx}</v></c>`;
        r3 += `<c r="H${s1RowNum}" s="28" t="s"><v>${currPeriodValIdx}</v></c>`;
        r3 += `<c r="I${s1RowNum}" s="29"/>`;
        s1MergeCells.push(`H${s1RowNum}:I${s1RowNum}`);

        s1RowXml += `<row r="${s1RowNum}" ht="22" customHeight="1">${r3}</row>`;
        s1RowNum++;

        // Row 5: Period Length (Bottom Closed Border Row)
        const lenLblIdx = addString('Period Length:');
        const prevLenValIdx = addString(cleanDuration(prev.duration));
        const currLenValIdx = addString(cleanDuration(curr.duration));

        let r4 = `<c r="A${s1RowNum}" s="16"/><c r="B${s1RowNum}" s="17"/><c r="C${s1RowNum}" s="18"/>`;

        r4 += `<c r="D${s1RowNum}" s="23" t="s"><v>${lenLblIdx}</v></c>`;
        r4 += `<c r="E${s1RowNum}" s="23" t="s"><v>${prevLenValIdx}</v></c>`;
        r4 += `<c r="F${s1RowNum}" s="0"/>`; // Zero-border clean spacer

        r4 += `<c r="G${s1RowNum}" s="30" t="s"><v>${lenLblIdx}</v></c>`;
        r4 += `<c r="H${s1RowNum}" s="31" t="s"><v>${currLenValIdx}</v></c>`;
        r4 += `<c r="I${s1RowNum}" s="32"/>`;
        s1MergeCells.push(`H${s1RowNum}:I${s1RowNum}`);

        s1RowXml += `<row r="${s1RowNum}" ht="23" customHeight="1">${r4}</row>`;
        s1RowNum++;

        // Vertically merge Col A, Col B, Col C
        s1MergeCells.push(`A${startRow}:A${endRow}`);
        s1MergeCells.push(`B${startRow}:B${endRow}`);
        s1MergeCells.push(`C${startRow}:C${endRow}`);
      }

      // Clean Blank Spacing Row Between Employee Cards (NO BORDERS)
      s1RowXml += `<row r="${s1RowNum}" ht="12" customHeight="1"><c r="A${s1RowNum}" s="0"/></row>`;
      s1RowNum++;
    });
  }

  s1FilterEndRow = s1RowNum - 1;

  // Sheet 1 Column Widths
  let s1ColsXml = '<cols>';
  const s1ColWidths = [24, 14, 20, 16, 28, 8, 16, 28, 20];
  for (let c = 0; c < numCols; c++) {
    const w = s1ColWidths[c] || 18;
    s1ColsXml += `<col min="${c + 1}" max="${c + 1}" width="${w}" customWidth="1"/>`;
  }
  s1ColsXml += '</cols>';

  const s1MergeXml = s1MergeCells.length
    ? `<mergeCells count="${s1MergeCells.length}">${s1MergeCells.map(m => `<mergeCell ref="${m}"/>`).join('')}</mergeCells>`
    : '';

  // AutoFilter on Columns A-C (Employee Name, Employee ID, Department)
  const s1AutoFilterXml = s1FilterStartRow > 0
    ? `<autoFilter ref="A${s1FilterStartRow}:C${s1FilterEndRow}"/>`
    : '';

  const sheet1Xml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheetViews>
    <sheetView tabSelected="1" workbookViewId="0" showGridLines="1"/>
  </sheetViews>
  ${s1ColsXml}
  <sheetData>
    ${s1RowXml}
  </sheetData>
  ${s1AutoFilterXml}
  ${s1MergeXml}
</worksheet>`;

  // ==========================================
  // SHEET 2: COMPLETE SEARCH & FILTER DATA TABLE
  // ==========================================
  let s2RowNum = 1;
  let s2RowXml = '';
  const s2Cols = (columns && columns.length > 0) ? columns : [
    { key: 'emp_code', label: 'Employee ID', width: 14 },
    { key: 'emp_name', label: 'Employee Name', width: 26 },
    { key: 'department', label: 'Department', width: 20 },
    { key: 'transition_type', label: 'Career Transition', width: 22 },
    { key: 'job_title', label: 'Job Title / Role', width: 26 },
    { key: 'grade', label: 'Grade', width: 14 },
    { key: 'period_start', label: 'Period Start', width: 15 },
    { key: 'period_end', label: 'Period End', width: 15 },
    { key: 'duration', label: 'Duration / Tenure', width: 18 },
    { key: 'status', label: 'Status', width: 14 }
  ];

  // Header Row
  let s2HeaderCells = '';
  s2Cols.forEach((col, idx) => {
    const colLetter = colToLetter(idx);
    const labelIdx = addString(col.label || col.key);
    s2HeaderCells += `<c r="${colLetter}${s2RowNum}" s="8" t="s"><v>${labelIdx}</v></c>`;
  });
  s2RowXml += `<row r="${s2RowNum}" ht="28" customHeight="1">${s2HeaderCells}</row>`;
  s2RowNum++;

  // Data Rows
  if (data && data.length > 0) {
    data.forEach((row, rIdx) => {
      let cells = '';
      const isEven = (rIdx % 2 === 0);
      const cellStyleId = isEven ? 33 : 34; // 33: White Zebra, 34: Light Slate Zebra

      s2Cols.forEach((col, cIdx) => {
        const colLetter = colToLetter(cIdx);
        const rawVal = (row[col.key] !== undefined && row[col.key] !== null) ? row[col.key] : '—';
        const isNum = col.isNumeric;

        if (typeof rawVal === 'number') {
          cells += `<c r="${colLetter}${s2RowNum}" s="${cellStyleId}"><v>${rawVal}</v></c>`;
        } else if (isNum && !isNaN(Number(rawVal))) {
          cells += `<c r="${colLetter}${s2RowNum}" s="${cellStyleId}"><v>${Number(rawVal)}</v></c>`;
        } else {
          const sIdx = addString(rawVal);
          cells += `<c r="${colLetter}${s2RowNum}" s="${cellStyleId}" t="s"><v>${sIdx}</v></c>`;
        }
      });

      s2RowXml += `<row r="${s2RowNum}" ht="22" customHeight="1">${cells}</row>`;
      s2RowNum++;
    });
  }

  const s2EndColLetter = colToLetter(s2Cols.length - 1);
  const s2EndRowNum = Math.max(s2RowNum - 1, 1);

  let s2ColsXml = '<cols>';
  s2Cols.forEach((col, idx) => {
    const w = col.width || 18;
    s2ColsXml += `<col min="${idx + 1}" max="${idx + 1}" width="${w}" customWidth="1"/>`;
  });
  s2ColsXml += '</cols>';

  const sheet2Xml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheetViews>
    <sheetView tabSelected="0" workbookViewId="0" showGridLines="1">
      <pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/>
    </sheetView>
  </sheetViews>
  ${s2ColsXml}
  <sheetData>
    ${s2RowXml}
  </sheetData>
  <autoFilter ref="A1:${s2EndColLetter}${s2EndRowNum}"/>
</worksheet>`;

  // ==========================================
  // STYLESHEET DEFINITION WITH SOFT CLEAN BORDERS
  // ==========================================
  const stylesXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="14">
    <!-- 0: Normal Base Font -->
    <font><sz val="10"/><color rgb="FF1E293B"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 1: Banner Title -->
    <font><b/><sz val="14"/><color rgb="FFFFFFFF"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 2: Stat Card Label -->
    <font><b/><sz val="9.5"/><color rgb="FF475569"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 3: Stat Card Value -->
    <font><b/><sz val="10"/><color rgb="FF0F172A"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 4: Filter Bar Header Text -->
    <font><b/><sz val="10.5"/><color rgb="FFFFFFFF"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 5: Employee Card Name -->
    <font><b/><sz val="11"/><color rgb="FF0F766E"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 6: Employee Card Code -->
    <font><b/><sz val="10"/><color rgb="FF0D9488"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 7: Prev Box Header -->
    <font><b/><sz val="10"/><color rgb="FF1E40AF"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 8: Curr Box Header -->
    <font><b/><sz val="10"/><color rgb="FF166534"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 9: Transition Arrow -->
    <font><b/><sz val="14"/><color rgb="FF2563EB"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 10: Card Field Label -->
    <font><b/><sz val="9.5"/><color rgb="FF64748B"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 11: Card Field Value -->
    <font><sz val="10"/><color rgb="FF0F172A"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 12: Employee Card Dept -->
    <font><b/><sz val="10"/><color rgb="FF334155"/><name val="Segoe UI"/><family val="2"/></font>
    <!-- 13: Table Header -->
    <font><b/><sz val="10.5"/><color rgb="FFFFFFFF"/><name val="Segoe UI"/><family val="2"/></font>
  </fonts>
  <fills count="12">
    <!-- 0: none -->
    <fill><patternFill patternType="none"/></fill>
    <!-- 1: gray125 -->
    <fill><patternFill patternType="gray125"/></fill>
    <!-- 2: Title & Filter Deep Teal -->
    <fill><patternFill patternType="solid"><fgColor rgb="FF0F766E"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 3: Slate 100 -->
    <fill><patternFill patternType="solid"><fgColor rgb="FFF1F5F9"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 4: Slate 50 -->
    <fill><patternFill patternType="solid"><fgColor rgb="FFF8FAFC"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 5: Left Box Soft Mint -->
    <fill><patternFill patternType="solid"><fgColor rgb="FFF0FDFA"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 6: Prev Box Header Light Blue -->
    <fill><patternFill patternType="solid"><fgColor rgb="FFEBF5FF"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 7: Curr Box Header Light Green -->
    <fill><patternFill patternType="solid"><fgColor rgb="FFF0FDF4"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 8: Pure White -->
    <fill><patternFill patternType="solid"><fgColor rgb="FFFFFFFF"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 9: Filter Header Blue -->
    <fill><patternFill patternType="solid"><fgColor rgb="FF1E40AF"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 10: Filter Header Green -->
    <fill><patternFill patternType="solid"><fgColor rgb="FF15803D"/><bgColor indexed="64"/></patternFill></fill>
    <!-- 11: Gray Spacer -->
    <fill><patternFill patternType="solid"><fgColor rgb="FF0F766E"/><bgColor indexed="64"/></patternFill></fill>
  </fills>
  <borders count="28">
    <!-- 0: None -->
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <!-- 1: Standard Thin Grid (Slate 200) -->
    <border><left style="thin"><color rgb="FFE2E8F0"/></left><right style="thin"><color rgb="FFE2E8F0"/></right><top style="thin"><color rgb="FFE2E8F0"/></top><bottom style="thin"><color rgb="FFE2E8F0"/></bottom><diagonal/></border>
    <!-- 2: Title Banner Border -->
    <border><left/><right/><top style="thin"><color rgb="FF0F766E"/></top><bottom style="medium"><color rgb="FF042F2E"/></bottom><diagonal/></border>
    <!-- 3: Filter Bar Header Border -->
    <border><left style="thin"><color rgb="FF115E59"/></left><right style="thin"><color rgb="FF115E59"/></right><top style="thin"><color rgb="FF115E59"/></top><bottom style="medium"><color rgb="FF0F766E"/></bottom><diagonal/></border>
    <!-- 4: Col A - Top (Soft Slate Border) -->
    <border><left style="thin"><color rgb="FFCBD5E1"/></left><right style="thin"><color rgb="FFE2E8F0"/></right><top style="thin"><color rgb="FFCBD5E1"/></top><bottom/><diagonal/></border>
    <!-- 5: Col B - Top -->
    <border><left style="thin"><color rgb="FFE2E8F0"/></left><right style="thin"><color rgb="FFE2E8F0"/></right><top style="thin"><color rgb="FFCBD5E1"/></top><bottom/><diagonal/></border>
    <!-- 6: Col C - Top -->
    <border><left style="thin"><color rgb="FFE2E8F0"/></left><right style="thin"><color rgb="FFCBD5E1"/></right><top style="thin"><color rgb="FFCBD5E1"/></top><bottom/><diagonal/></border>
    <!-- 7: Col A - Mid -->
    <border><left style="thin"><color rgb="FFCBD5E1"/></left><right style="thin"><color rgb="FFE2E8F0"/></right><top/><bottom/><diagonal/></border>
    <!-- 8: Col B - Mid -->
    <border><left style="thin"><color rgb="FFE2E8F0"/></left><right style="thin"><color rgb="FFE2E8F0"/></right><top/><bottom/><diagonal/></border>
    <!-- 9: Col C - Mid -->
    <border><left style="thin"><color rgb="FFE2E8F0"/></left><right style="thin"><color rgb="FFCBD5E1"/></right><top/><bottom/><diagonal/></border>
    <!-- 10: Col A - Bot -->
    <border><left style="thin"><color rgb="FFCBD5E1"/></left><right style="thin"><color rgb="FFE2E8F0"/></right><top/><bottom style="thin"><color rgb="FFCBD5E1"/></bottom><diagonal/></border>
    <!-- 11: Col B - Bot -->
    <border><left style="thin"><color rgb="FFE2E8F0"/></left><right style="thin"><color rgb="FFE2E8F0"/></right><top/><bottom style="thin"><color rgb="FFCBD5E1"/></bottom><diagonal/></border>
    <!-- 12: Col C - Bot -->
    <border><left style="thin"><color rgb="FFE2E8F0"/></left><right style="thin"><color rgb="FFCBD5E1"/></right><top/><bottom style="thin"><color rgb="FFCBD5E1"/></bottom><diagonal/></border>
    <!-- 13: Prev Box - Header Left (Soft Blue) -->
    <border><left style="thin"><color rgb="FFBFDBFE"/></left><right/><top style="thin"><color rgb="FFBFDBFE"/></top><bottom style="thin"><color rgb="FFBFDBFE"/></bottom><diagonal/></border>
    <!-- 14: Prev Box - Header Right -->
    <border><left/><right style="thin"><color rgb="FFBFDBFE"/></right><top style="thin"><color rgb="FFBFDBFE"/></top><bottom style="thin"><color rgb="FFBFDBFE"/></bottom><diagonal/></border>
    <!-- 15: Prev Box - Row Left -->
    <border><left style="thin"><color rgb="FFBFDBFE"/></left><right/><top/><bottom style="thin"><color rgb="FFF1F5F9"/></bottom><diagonal/></border>
    <!-- 16: Prev Box - Row Right -->
    <border><left/><right style="thin"><color rgb="FFBFDBFE"/></right><top/><bottom style="thin"><color rgb="FFF1F5F9"/></bottom><diagonal/></border>
    <!-- 17: Prev Box - Bot Left -->
    <border><left style="thin"><color rgb="FFBFDBFE"/></left><right/><top/><bottom style="thin"><color rgb="FFBFDBFE"/></bottom><diagonal/></border>
    <!-- 18: Prev Box - Bot Right -->
    <border><left/><right style="thin"><color rgb="FFBFDBFE"/></right><top/><bottom style="thin"><color rgb="FFBFDBFE"/></bottom><diagonal/></border>
    <!-- 19: Curr Box - Header Left (Soft Green) -->
    <border><left style="thin"><color rgb="FFBBF7D0"/></left><right/><top style="thin"><color rgb="FFBBF7D0"/></top><bottom style="thin"><color rgb="FFBBF7D0"/></bottom><diagonal/></border>
    <!-- 20: Curr Box - Header Mid -->
    <border><left/><right/><top style="thin"><color rgb="FFBBF7D0"/></top><bottom style="thin"><color rgb="FFBBF7D0"/></bottom><diagonal/></border>
    <!-- 21: Curr Box - Header Right -->
    <border><left/><right style="thin"><color rgb="FFBBF7D0"/></right><top style="thin"><color rgb="FFBBF7D0"/></top><bottom style="thin"><color rgb="FFBBF7D0"/></bottom><diagonal/></border>
    <!-- 22: Curr Box - Row Left -->
    <border><left style="thin"><color rgb="FFBBF7D0"/></left><right/><top/><bottom style="thin"><color rgb="FFF1F5F9"/></bottom><diagonal/></border>
    <!-- 23: Curr Box - Row Mid -->
    <border><left/><right/><top/><bottom style="thin"><color rgb="FFF1F5F9"/></bottom><diagonal/></border>
    <!-- 24: Curr Box - Row Right -->
    <border><left/><right style="thin"><color rgb="FFBBF7D0"/></right><top/><bottom style="thin"><color rgb="FFF1F5F9"/></bottom><diagonal/></border>
    <!-- 25: Curr Box - Bot Left -->
    <border><left style="thin"><color rgb="FFBBF7D0"/></left><right/><top/><bottom style="thin"><color rgb="FFBBF7D0"/></bottom><diagonal/></border>
    <!-- 26: Curr Box - Bot Mid -->
    <border><left/><right/><top/><bottom style="thin"><color rgb="FFBBF7D0"/></bottom><diagonal/></border>
    <!-- 27: Curr Box - Bot Right -->
    <border><left/><right style="thin"><color rgb="FFBBF7D0"/></right><top/><bottom style="thin"><color rgb="FFBBF7D0"/></bottom><diagonal/></border>
  </borders>
  <cellStyleXfs count="1">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
  </cellStyleXfs>
  <cellXfs count="35">
    <!-- 0: Default Blank -->
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
    <!-- 1: Banner Title (Deep Teal) -->
    <xf numFmtId="0" fontId="1" fillId="2" borderId="2" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 2: Stat Label -->
    <xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center" indent="1"/></xf>
    <!-- 3: Stat Value -->
    <xf numFmtId="0" fontId="3" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 4: Filter Bar - Header Teal (Name, ID, Dept, Spacer) -->
    <xf numFmtId="0" fontId="4" fillId="2" borderId="3" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 5: Filter Bar - Previous (Blue) -->
    <xf numFmtId="0" fontId="4" fillId="9" borderId="3" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 6: Filter Bar - Current (Green) -->
    <xf numFmtId="0" fontId="4" fillId="10" borderId="3" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 7: Reserved -->
    <xf numFmtId="0" fontId="4" fillId="2" borderId="3" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 8: Sheet 2 Data Table Header -->
    <xf numFmtId="0" fontId="13" fillId="2" borderId="3" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
    <!-- 9: Transition Arrow Center (Zero border, clean floating) -->
    <xf numFmtId="0" fontId="9" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 10: Col A - Top (Name) -->
    <xf numFmtId="0" fontId="5" fillId="5" borderId="4" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 11: Col B - Top (ID) -->
    <xf numFmtId="0" fontId="6" fillId="5" borderId="5" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 12: Col C - Top (Dept) -->
    <xf numFmtId="0" fontId="12" fillId="5" borderId="6" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 13: Col A - Mid -->
    <xf numFmtId="0" fontId="5" fillId="5" borderId="7" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 14: Col B - Mid -->
    <xf numFmtId="0" fontId="6" fillId="5" borderId="8" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 15: Col C - Mid -->
    <xf numFmtId="0" fontId="12" fillId="5" borderId="9" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 16: Col A - Bot -->
    <xf numFmtId="0" fontId="5" fillId="5" borderId="10" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 17: Col B - Bot -->
    <xf numFmtId="0" fontId="6" fillId="5" borderId="11" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 18: Col C - Bot -->
    <xf numFmtId="0" fontId="12" fillId="5" borderId="12" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <!-- 19: Prev Box - Header Left -->
    <xf numFmtId="0" fontId="7" fillId="6" borderId="13" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 20: Prev Box - Header Right -->
    <xf numFmtId="0" fontId="7" fillId="6" borderId="14" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center"/></xf>
    <!-- 21: Prev Box - Row Left (Label) -->
    <xf numFmtId="0" fontId="10" fillId="4" borderId="15" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 22: Prev Box - Row Right (Value) -->
    <xf numFmtId="0" fontId="11" fillId="8" borderId="16" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 23: Prev Box - Bot Both -->
    <xf numFmtId="0" fontId="10" fillId="4" borderId="17" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 24: Curr Box - Header Left -->
    <xf numFmtId="0" fontId="8" fillId="7" borderId="19" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 25: Curr Box - Header Mid -->
    <xf numFmtId="0" fontId="8" fillId="7" borderId="20" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center"/></xf>
    <!-- 26: Curr Box - Header Right -->
    <xf numFmtId="0" fontId="8" fillId="7" borderId="21" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center"/></xf>
    <!-- 27: Curr Box - Row Left (Label) -->
    <xf numFmtId="0" fontId="10" fillId="4" borderId="22" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 28: Curr Box - Row Mid -->
    <xf numFmtId="0" fontId="11" fillId="8" borderId="23" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 29: Curr Box - Row Right -->
    <xf numFmtId="0" fontId="11" fillId="8" borderId="24" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 30: Curr Box - Bot Left -->
    <xf numFmtId="0" fontId="10" fillId="4" borderId="25" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 31: Curr Box - Bot Mid -->
    <xf numFmtId="0" fontId="11" fillId="8" borderId="26" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 32: Curr Box - Bot Right -->
    <xf numFmtId="0" fontId="11" fillId="8" borderId="27" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 33: Data Table Row Even -->
    <xf numFmtId="0" fontId="0" fillId="8" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <!-- 34: Data Table Row Odd -->
    <xf numFmtId="0" fontId="0" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
  </cellXfs>
</styleSheet>`;

  const sharedStringsXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="${sharedStrings.length}" uniqueCount="${sharedStrings.length}">
  ${sharedStrings.map(s => `<si><t xml:space="preserve">${escapeXml(s)}</t></si>`).join('')}
</sst>`;

  const contentTypesXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
  <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
</Types>`;

  const relsXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>`;

  const wbRelsXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
  <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
</Relationships>`;

  const cleanSheetName = (sheetName || 'Career Transitions').replace(/[\\/?*[\]:]/g, '').substring(0, 31);
  const workbookXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="${escapeXml(cleanSheetName)}" sheetId="1" r:id="rId1"/>
    <sheet name="Filter &amp; Search Records" sheetId="2" r:id="rId2"/>
  </sheets>
</workbook>`;

  const zipData = createZipArchive([
    { name: '[Content_Types].xml', data: contentTypesXml },
    { name: '_rels/.rels', data: relsXml },
    { name: 'xl/_rels/workbook.xml.rels', data: wbRelsXml },
    { name: 'xl/workbook.xml', data: workbookXml },
    { name: 'xl/styles.xml', data: stylesXml },
    { name: 'xl/sharedStrings.xml', data: sharedStringsXml },
    { name: 'xl/worksheets/sheet1.xml', data: sheet1Xml },
    { name: 'xl/worksheets/sheet2.xml', data: sheet2Xml }
  ]);

  if (typeof window !== 'undefined' && typeof document !== 'undefined') {
    const blob = new Blob([zipData], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    const cleanExt = filename.replace(/\.(xls|xlsx)$/i, '');
    const validFilename = `${cleanExt}.xlsx`;
    link.setAttribute('download', validFilename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  return zipData;
}
