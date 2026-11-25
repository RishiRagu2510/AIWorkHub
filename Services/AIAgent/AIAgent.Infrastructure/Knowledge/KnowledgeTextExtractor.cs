using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AIAgent.Infrastructure.Knowledge;


public static class KnowledgeTextExtractor
{
    private const int MaxPages = 600;

    
    public static string? Extract(byte[] bytes, string fileName)
    {
        var extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        var text = extension == ".pdf" ? Pdf(bytes) : DocumentText.Extract(bytes, extension, KnowledgeRules.MaxTextChars);
        text = text?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? Pdf(byte[] bytes)
    {
        try
        {
            using var pdf = PdfDocument.Open(bytes);
            var text = new System.Text.StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                if (page.Number > MaxPages || text.Length >= KnowledgeRules.MaxTextChars) break;
                text.AppendLine(ContentOrderTextExtractor.GetText(page));
                text.AppendLine();
            }
            return text.Length > KnowledgeRules.MaxTextChars ? text.ToString(0, KnowledgeRules.MaxTextChars) : text.ToString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            
            return null;
        }
    }
}
