using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace AIAgent.Application.Services;

public static class DocumentText
{
    private const string Truncated = "\n[... the rest of the file was left out because it is long ...]";
    private const long MaxEntryBytes = 25L * 1024 * 1024;
    private const int MaxSheetRows = 300;
    private const int MaxSheetColumns = 40;

    private static readonly XmlReaderSettings Safe = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,   
        XmlResolver = null,                       
        MaxCharactersInDocument = 60_000_000,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = false
    };

    public enum Kind { Text, Word, Excel, PowerPoint, Unsupported }

    public static Kind KindOf(string extension) => extension.ToLowerInvariant() switch
    {
        ".txt" or ".csv" or ".md" or ".json" => Kind.Text,
        ".docx" => Kind.Word,
        ".xlsx" => Kind.Excel,
        ".pptx" => Kind.PowerPoint,
        _ => Kind.Unsupported
    };

    
    public static string? Extract(byte[] bytes, string extension, int maxChars)
    {
        try
        {
            return KindOf(extension) switch
            {
                Kind.Text => Clip(DecodeText(bytes), maxChars),
                Kind.Word => Tidy(Zipped(bytes, archive => WordText(archive, maxChars)), maxChars),
                Kind.Excel => Tidy(Zipped(bytes, archive => ExcelText(archive, maxChars)), maxChars),
                Kind.PowerPoint => Tidy(Zipped(bytes, archive => SlidesText(archive, maxChars)), maxChars),
                _ => null
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string DecodeText(byte[] bytes)
    {
        
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string Clip(string text, int maxChars) => text.Length <= maxChars ? text : text[..maxChars] + Truncated;

    
    private static string Tidy(string? text, int maxChars)
    {
        if (text is null) return "";
        var cleaned = Regex.Replace(text.Replace("\r", ""), @"[ \t]+\n", "\n", RegexOptions.None, TimeSpan.FromSeconds(2));
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n", RegexOptions.None, TimeSpan.FromSeconds(2)).Trim();
        return Clip(cleaned, maxChars);
    }

    private static T Zipped<T>(byte[] bytes, Func<ZipArchive, T> read)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return read(archive);
    }

    private static XmlReader? Open(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry is null || entry.Length > MaxEntryBytes) return null;
        return XmlReader.Create(entry.Open(), Safe);
    }

    

    private static string WordText(ZipArchive archive, int maxChars)
    {
        using var reader = Open(archive, "word/document.xml");
        if (reader is null) return "";

        var text = new StringBuilder();
        while (!reader.EOF && text.Length < maxChars)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.LocalName)
                {
                    case "t" when !reader.IsEmptyElement:
                        text.Append(reader.ReadElementContentAsString());
                        continue;
                    case "tab": text.Append('\t'); break;
                    case "br" or "cr": text.Append('\n'); break;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                switch (reader.LocalName)
                {
                    case "p": text.Append('\n'); break;
                    case "tc":                              
                        while (text.Length > 0 && text[^1] == '\n') text.Length--;
                        text.Append(" | ");
                        break;
                    case "tr":                              
                        if (text.Length >= 3 && text.ToString(text.Length - 3, 3) == " | ") text.Length -= 3;
                        text.Append('\n');
                        break;
                }
            }
            reader.Read();
        }
        return text.ToString();
    }

    

    private static string SlidesText(ZipArchive archive, int maxChars)
    {
        var slides = archive.Entries
            .Select(e => (Entry: e, Number: SlideNumber(e.FullName)))
            .Where(x => x.Number > 0)
            .OrderBy(x => x.Number)
            .ToList();

        var text = new StringBuilder();
        foreach (var (entry, number) in slides)
        {
            if (text.Length >= maxChars || entry.Length > MaxEntryBytes) break;
            text.Append("--- Slide ").Append(number).Append(" ---\n");

            using var reader = XmlReader.Create(entry.Open(), Safe);
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && !reader.IsEmptyElement)
                {
                    text.Append(reader.ReadElementContentAsString());
                    continue;
                }
                if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "p") text.Append('\n');
                reader.Read();
            }
            text.Append('\n');
        }
        return text.ToString();
    }

    private static int SlideNumber(string path)
    {
        var match = Regex.Match(path, @"^ppt/slides/slide(\d+)\.xml$", RegexOptions.None, TimeSpan.FromSeconds(1));
        return match.Success && int.TryParse(match.Groups[1].Value, out var n) ? n : 0;
    }

    

    private static string ExcelText(ZipArchive archive, int maxChars)
    {
        var shared = SharedStrings(archive);
        var sheets = SheetFiles(archive);
        var text = new StringBuilder();

        foreach (var (name, path) in sheets)
        {
            if (text.Length >= maxChars) break;
            using var reader = Open(archive, path);
            if (reader is null) continue;

            text.Append("--- Sheet: ").Append(name).Append(" ---\n");
            var rows = 0;
            while (!reader.EOF && rows < MaxSheetRows && text.Length < maxChars)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
                {
                    string line;
                    using (var row = reader.ReadSubtree()) line = RowText(row, shared);   
                    if (line.Length > 0) { text.Append(line).Append('\n'); rows++; }
                }
                reader.Read();
            }
            if (rows >= MaxSheetRows) text.Append("[... more rows were left out ...]\n");
            text.Append('\n');
        }
        return text.ToString();
    }

    private static string RowText(XmlReader row, List<string> shared)
    {
        var cells = new List<string>();
        while (row.Read())
        {
            if (row.NodeType != XmlNodeType.Element || row.LocalName != "c") continue;

            var column = ColumnIndex(row.GetAttribute("r"));
            var type = row.GetAttribute("t");
            string value = "";

            if (!row.IsEmptyElement)
            {
                var inline = new StringBuilder();
                string? raw = null;
                using (var cell = row.ReadSubtree())
                {
                    while (!cell.EOF)
                    {
                        if (cell.NodeType == XmlNodeType.Element && !cell.IsEmptyElement && cell.LocalName == "v") { raw = cell.ReadElementContentAsString(); continue; }
                        if (cell.NodeType == XmlNodeType.Element && !cell.IsEmptyElement && cell.LocalName == "t") { inline.Append(cell.ReadElementContentAsString()); continue; }
                        cell.Read();
                    }
                }
                value = type switch
                {
                    "s" when int.TryParse(raw, out var index) && index >= 0 && index < shared.Count => shared[index],
                    "inlineStr" => inline.ToString(),
                    "b" => raw == "1" ? "TRUE" : "FALSE",
                    _ => raw ?? inline.ToString()
                };
            }

            if (column >= MaxSheetColumns) continue;
            while (cells.Count < column) cells.Add("");
            if (cells.Count == column) cells.Add(value.Replace('\n', ' '));
            else cells[column] = value.Replace('\n', ' ');
        }

        while (cells.Count > 0 && cells[^1].Length == 0) cells.RemoveAt(cells.Count - 1);
        return cells.Count == 0 ? "" : string.Join(" | ", cells);
    }

    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return 0;
        var index = 0;
        foreach (var c in reference)
        {
            if (!char.IsLetter(c)) break;
            index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }
        return Math.Max(0, index - 1);
    }

    private static List<string> SharedStrings(ZipArchive archive)
    {
        var strings = new List<string>();
        using var reader = Open(archive, "xl/sharedStrings.xml");
        if (reader is null) return strings;

        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si" && !reader.IsEmptyElement)
            {
                var item = new StringBuilder();
                using (var si = reader.ReadSubtree())
                {
                    var skipPhonetic = 0;
                    while (!si.EOF)
                    {
                        if (si.NodeType == XmlNodeType.Element && si.LocalName == "rPh" && !si.IsEmptyElement) skipPhonetic++;
                        else if (si.NodeType == XmlNodeType.EndElement && si.LocalName == "rPh") skipPhonetic--;
                        else if (si.NodeType == XmlNodeType.Element && si.LocalName == "t" && skipPhonetic == 0 && !si.IsEmptyElement)
                        {
                            item.Append(si.ReadElementContentAsString());
                            continue;
                        }
                        si.Read();
                    }
                }
                strings.Add(item.ToString());
            }
            reader.Read();
            if (strings.Count > 200_000) break;
        }
        return strings;
    }

    private static List<(string Name, string Path)> SheetFiles(ZipArchive archive)
    {
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var rels = Open(archive, "xl/_rels/workbook.xml.rels"))
        {
            while (rels is not null && rels.Read())
                if (rels.NodeType == XmlNodeType.Element && rels.LocalName == "Relationship"
                    && rels.GetAttribute("Id") is { } id && rels.GetAttribute("Target") is { } target)
                    targets[id] = target.TrimStart('/').StartsWith("xl/", StringComparison.Ordinal) ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
        }

        var sheets = new List<(string, string)>();
        using (var book = Open(archive, "xl/workbook.xml"))
        {
            while (book is not null && book.Read())
            {
                if (book.NodeType != XmlNodeType.Element || book.LocalName != "sheet") continue;
                var name = book.GetAttribute("name") ?? $"Sheet{sheets.Count + 1}";
                var relId = book.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                if (relId is not null && targets.TryGetValue(relId, out var path)) sheets.Add((name, path));
            }
        }

        if (sheets.Count > 0) return sheets;

        
        return archive.Entries
            .Where(e => Regex.IsMatch(e.FullName, @"^xl/worksheets/sheet\d+\.xml$", RegexOptions.None, TimeSpan.FromSeconds(1)))
            .OrderBy(e => e.FullName.Length).ThenBy(e => e.FullName, StringComparer.Ordinal)
            .Select((e, i) => ($"Sheet{i + 1}", e.FullName))
            .ToList();
    }
}
