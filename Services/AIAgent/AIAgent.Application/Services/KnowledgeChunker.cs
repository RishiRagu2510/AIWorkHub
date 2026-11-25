using System.Text;
using System.Text.RegularExpressions;
using AIAgent.Application.Interfaces;

namespace AIAgent.Application.Services;

public static partial class KnowledgeChunker
{
    public const int Target = 900;      
    public const int Hard = 1400;       

    public static IReadOnlyList<KnowledgeChunk> Split(string? text)
    {
        var chunks = new List<KnowledgeChunk>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        string? heading = null;
        var headingHasText = true;
        var body = new StringBuilder();

        void Flush()
        {
            var content = body.ToString().Trim();
            body.Clear();
            if (content.Length == 0) return;
            chunks.Add(new KnowledgeChunk(heading, content));
            headingHasText = true;
        }

        void NewHeading(string found)
        {
            Flush();
            
            if (!headingHasText && heading is not null) chunks.Add(new KnowledgeChunk(null, heading));
            heading = found;
            headingHasText = false;
        }

        foreach (var segment in Segments(text))
        {
            if (segment.Heading is { } title)
            {
                NewHeading(title);
                continue;
            }

            foreach (var piece in Pieces(segment.Text))
            {
                if (body.Length > 0 && body.Length + piece.Length + 2 > Target) Flush();
                if (body.Length > 0) body.Append("\n\n");
                body.Append(piece);
            }
        }
        Flush();
        if (!headingHasText && heading is not null) chunks.Add(new KnowledgeChunk(null, heading));
        return chunks;
    }

    
    public static string Stored(KnowledgeChunk chunk) =>
        string.IsNullOrEmpty(chunk.Heading) ? chunk.Text : chunk.Heading + "\n" + chunk.Text;

    private readonly record struct Segment(string? Heading, string Text);

    private static IEnumerable<Segment> Segments(string text)
    {
        var normal = text.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var raw in BlankLines().Split(normal))
        {
            var pending = new List<string>();
            foreach (var line in raw.Split('\n'))
            {
                if (HeadingOf(line) is { } title)
                {
                    if (pending.Count > 0) yield return new Segment(null, string.Join("\n", pending).Trim());
                    pending.Clear();
                    yield return new Segment(title, "");
                }
                else if (line.Trim().Length > 0)
                {
                    pending.Add(line.Trim());
                }
            }
            if (pending.Count > 0) yield return new Segment(null, string.Join("\n", pending).Trim());
        }
    }

    
    private static string? HeadingOf(string line)
    {
        line = line.Trim();
        if (line.Length == 0 || line.Contains('\n')) return null;
        var markdown = MarkdownHeading().Match(line);
        if (markdown.Success) return markdown.Groups[1].Value.Trim();
        if (line.Length > 80 || EndsLikeSentence(line)) return null;
        if (Numbered().IsMatch(line) && line.Split(' ').Length <= 10) return line;
        if (line.Split(' ').Length <= 10 && IsMostlyUpper(line)) return line;
        return null;
    }

    private static bool EndsLikeSentence(string s) => s.TrimEnd().EndsWith('.') || s.TrimEnd().EndsWith(',') || s.TrimEnd().EndsWith(';') || s.TrimEnd().EndsWith(':');

    private static bool IsMostlyUpper(string s)
    {
        var letters = s.Where(char.IsLetter).ToList();
        return letters.Count >= 3 && letters.Count(char.IsUpper) * 10 >= letters.Count * 8;
    }

    
    private static IEnumerable<string> Pieces(string block)
    {
        if (block.Length <= Hard)
        {
            yield return block;
            yield break;
        }

        var current = new StringBuilder();
        foreach (var sentence in SentenceEnd().Split(block))
        {
            var s = sentence.Trim();
            if (s.Length == 0) continue;
            while (s.Length > Hard)
            {
                if (current.Length > 0) { yield return current.ToString(); current.Clear(); }
                var cut = s.LastIndexOf(' ', Hard - 1);
                if (cut < Hard / 2) cut = Hard;
                yield return s[..cut].Trim();
                s = s[cut..].Trim();
            }
            if (current.Length > 0 && current.Length + s.Length + 1 > Target)
            {
                yield return current.ToString();
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(s);
        }
        if (current.Length > 0) yield return current.ToString();
    }

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex BlankLines();

    [GeneratedRegex(@"^#{1,6}\s+(.+?)\s*#*$")]
    private static partial Regex MarkdownHeading();

    [GeneratedRegex(@"^(\d{1,2}(\.\d{1,2}){0,3}[.)]?|[A-Z][.)])\s+[A-Z]")]
    private static partial Regex Numbered();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceEnd();
}
