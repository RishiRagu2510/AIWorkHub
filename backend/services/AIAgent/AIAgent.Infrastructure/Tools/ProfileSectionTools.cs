using System.Globalization;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AIAgent.Infrastructure.Tools;


public static class ProfileSections
{
    public static readonly string[] All = ["personal", "experience", "education", "certifications", "languages"];

    public static readonly string[] LanguageLevels = ["Elementary", "Limited Working", "Professional Working", "Full Professional", "Native / Bilingual"];

    
    public static string? LanguageLevel(string? said)
    {
        if (string.IsNullOrWhiteSpace(said)) return null;
        var word = said.Trim().ToLowerInvariant();
        if (int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return n is >= 1 and <= 5 ? LanguageLevels[n - 1] : null;
        var exact = LanguageLevels.FirstOrDefault(l => l.Equals(said.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        if (word.Contains("native") || word.Contains("bilingual") || word.Contains("mother")) return LanguageLevels[4];
        if (word.Contains("fluent") || word.Contains("full")) return LanguageLevels[3];
        if (word.Contains("professional") || word.Contains("working") || word.Contains("intermediate") || word.Contains("good")) return LanguageLevels[2];
        if (word.Contains("limited") || word.Contains("basic") || word.Contains("conversational")) return LanguageLevels[1];
        if (word.Contains("elementary") || word.Contains("beginner") || word.Contains("little")) return LanguageLevels[0];
        return null;
    }
}




public class PrepareProfileSectionTool(IHttpClientFactory factory, IHttpContextAccessor accessor) : ServiceToolBase(factory, accessor)
{
    public override string Name => "prepare_profile_section";
    public override string Description =>
        "Show the user a form in the chat to add or change an entry on one of their own profile tabs: 'personal' (preferred name, phone, emergency contact), 'experience' (past jobs), 'education', 'certifications' or 'languages'. " +
        "Call this when the user wants to add, update or edit that kind of detail and has not given every field. The form lists what is already on the profile, to change one, and has a button to add a new one. " +
        "If the user already named some details, pass every one so the form opens with them filled in; they still press Save themselves. " +
        "Examples: 'I know Hindi' -> section=languages, name=Hindi; 'I know Hindi, fluent' -> also proficiency; 'add my AWS Solutions Architect certification' -> section=certifications, name=AWS Solutions Architect. " +
        "For skills use prepare_skills; for removing an entry use prepare_delete. If the user gave every field of a personal change, use update_my_profile instead. " +
        "The form is the answer: reply with one short sentence and do not ask them to type the details.";
    public override string Category => "Persona";
    public override string? RequiredPortal => "employee";
    public override string RequiredPermission => "EMPLOYEE_UPDATE_SELF";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["section"] = new { type = "STRING", description = "personal, experience, education, certifications or languages." },
            ["name"] = new { type = "STRING", description = "A certification's name (for example AWS Solutions Architect) or a language's name (for example Hindi). Always pass it when the user said it." },
            ["organization"] = new { type = "STRING", description = "The company (experience), institution (education) or issuer (certification)." },
            ["title"] = new { type = "STRING", description = "The job title (experience) or the degree or qualification (education)." },
            ["fieldOfStudy"] = new { type = "STRING", description = "The field of study (education)." },
            ["description"] = new { type = "STRING", description = "What they did in the job (experience)." },
            ["startDate"] = new { type = "STRING", description = "YYYY-MM-DD. When the job or the studies began." },
            ["endDate"] = new { type = "STRING", description = "YYYY-MM-DD. When the job or the studies ended; leave out if ongoing." },
            ["issuedDate"] = new { type = "STRING", description = "YYYY-MM-DD. When the certification was issued." },
            ["expiryDate"] = new { type = "STRING", description = "YYYY-MM-DD. When the certification expires; leave out if it does not." },
            ["credentialId"] = new { type = "STRING", description = "The certification's credential id." },
            ["credentialUrl"] = new { type = "STRING", description = "An https link to the credential." },
            ["proficiency"] = new { type = "STRING", description = "A language level: Elementary, Limited Working, Professional Working, Full Professional or Native / Bilingual (or 1 to 5)." }
        }
    };

    private static ToolResult Fail(string message) => new(false, null, message);

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var section = (Str(arguments, "section") ?? "").Trim().ToLowerInvariant();
            if (section is "certification" or "certificate" or "certificates") section = "certifications";
            if (section is "language") section = "languages";
            if (section is "job" or "jobs" or "work experience" or "work_experience") section = "experience";
            if (!ProfileSections.All.Contains(section))
                return Fail("section must be one of: personal, experience, education, certifications, languages. For skills use prepare_skills.");

            
            var prefill = new Dictionary<string, string?>();
            var dates = new Dictionary<string, DateOnly>();
            foreach (var key in new[] { "startDate", "endDate", "issuedDate", "expiryDate" })
            {
                if (Str(arguments, key) is not { } text) continue;
                if (!TryDate(text, out var parsed)) return Fail($"{key} must be written as YYYY-MM-DD.");
                dates[key] = parsed;
            }
            if (dates.TryGetValue("startDate", out var s) && dates.TryGetValue("endDate", out var e) && e < s) return Fail("endDate is before startDate.");
            if (dates.TryGetValue("issuedDate", out var i) && dates.TryGetValue("expiryDate", out var x) && x < i) return Fail("expiryDate is before issuedDate.");
            if (Str(arguments, "credentialUrl") is { } url && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
                return Fail("credentialUrl must be an https link.");

            string? D(string key) => dates.TryGetValue(key, out var d) ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            switch (section)
            {
                case "experience":
                    prefill = new() { ["company"] = Str(arguments, "organization"), ["title"] = Str(arguments, "title"), ["description"] = Str(arguments, "description"), ["startDate"] = D("startDate"), ["endDate"] = D("endDate") };
                    break;
                case "education":
                    prefill = new() { ["institution"] = Str(arguments, "organization"), ["qualification"] = Str(arguments, "title"), ["fieldOfStudy"] = Str(arguments, "fieldOfStudy"), ["startDate"] = D("startDate"), ["endDate"] = D("endDate") };
                    break;
                case "certifications":
                    prefill = new() { ["name"] = Str(arguments, "name"), ["issuer"] = Str(arguments, "organization"), ["issuedDate"] = D("issuedDate"), ["expiryDate"] = D("expiryDate"), ["credentialId"] = Str(arguments, "credentialId"), ["credentialUrl"] = Str(arguments, "credentialUrl") };
                    break;
                case "languages":
                    var said = Str(arguments, "proficiency");
                    prefill = new() { ["name"] = Str(arguments, "name"), ["proficiency"] = ProfileSections.LanguageLevel(said) };
                    break;
            }
            var given = prefill.Where(p => !string.IsNullOrWhiteSpace(p.Value)).ToDictionary(p => p.Key, p => p.Value);

            var client = Identity();
            using var me = await TryGetAsync(client, IdentityRoutes.Me, cancellationToken);
            if (me == null) return Fail("I could not load your profile just now, so I could not open the form. Please try again in a moment.");
            var version = LongProp(me.RootElement, "version");
            if (version == null) return Fail("Your profile did not report a version, so I could not open the form.");

            if (section == "personal")
            {
                var values = new
                {
                    preferredName = StrOf(me.RootElement, "preferredName"),
                    phone = StrOf(me.RootElement, "phone"),
                    emergencyContactName = StrOf(me.RootElement, "emergencyContactName"),
                    emergencyContactPhone = StrOf(me.RootElement, "emergencyContactPhone"),
                    emergencyContactRelation = StrOf(me.RootElement, "emergencyContactRelation")
                };
                return new ToolResult(true, JsonSerializer.Serialize(new { form = "profileSection", section, version, values, items = Array.Empty<object>(), prefill = new Dictionary<string, string?>() }));
            }

            using var mine = await TryGetAsync(client, IdentityRoutes.MySection(section), cancellationToken);
            if (mine == null || mine.RootElement.ValueKind != JsonValueKind.Array)
                return Fail("I could not load what is on your profile just now, so I could not open the form. Please try again in a moment.");

            var items = mine.RootElement.EnumerateArray().Select(row => Shape(section, row)).ToList();
            return new ToolResult(true, JsonSerializer.Serialize(new { form = "profileSection", section, version, items, prefill = given, languageLevels = section == "languages" ? ProfileSections.LanguageLevels : null }));
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    
    private static Dictionary<string, object?> Shape(string section, JsonElement row)
    {
        var item = new Dictionary<string, object?> { ["id"] = LongProp(row, "id") };
        switch (section)
        {
            case "experience":
                foreach (var key in new[] { "company", "title", "description", "startDate", "endDate" }) item[key] = StrOf(row, key);
                break;
            case "education":
                foreach (var key in new[] { "institution", "qualification", "fieldOfStudy", "startDate", "endDate" }) item[key] = StrOf(row, key);
                break;
            case "certifications":
                foreach (var key in new[] { "name", "issuer", "issuedDate", "expiryDate", "credentialId", "credentialUrl", "fileUrl", "fileName" }) item[key] = StrOf(row, key);
                break;
            default:
                foreach (var key in new[] { "name", "proficiency" }) item[key] = StrOf(row, key);
                break;
        }
        return item;
    }
}
