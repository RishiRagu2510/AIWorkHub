using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Backend.Authorization.Tests;






public sealed class EmployeeCareerContractTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null, uint? version = null, long user = 100, HttpContent? content = null, string[]? permissions = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(user, 1, user == 100 ? "ADMIN" : "DEVELOPER", permissions ?? SecurityFixture.Permissions));
        if (content is not null) request.Content = content;
        else if (body is not null) request.Content = JsonContent.Create(body);
        if (version.HasValue) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await fixture.Client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string[] Keys(JsonElement element) => element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
    private static string[] Expect(params string[] keys) => keys.Order(StringComparer.Ordinal).ToArray();

    private static readonly string[] SkillGetKeys = Expect("id", "tenantId", "employeeId", "skillId", "skillName", "category", "proficiency", "yearsOfExperience", "lastUsedDate", "version");
    private static readonly string[] SkillSavedKeys = Expect("id", "tenantId", "version", "employeeId", "skillId", "skill", "skillName", "category", "proficiency", "yearsOfExperience", "lastUsedDate");
    private static readonly string[] EducationKeys = Expect("fieldOfStudy", "id", "tenantId", "version", "employeeId", "institution", "qualification", "startDate", "endDate");
    private static readonly string[] CertificationKeys = Expect("credentialId", "id", "tenantId", "version", "employeeId", "name", "issuer", "issuedDate", "expiryDate", "credentialUrl", "fileUrl", "fileName");
    private static readonly string[] ExperienceKeys = Expect("id", "tenantId", "version", "employeeId", "company", "title", "description", "startDate", "endDate");
    private static readonly string[] LanguageKeys = Expect("id", "tenantId", "version", "employeeId", "name", "proficiency");
    private static readonly string[] CompensationKeys = Expect("id", "tenantId", "version", "employeeId", "ctc", "bonus", "currency", "effectiveFrom", "effectiveTo");

    [Fact]
    public async Task EverySectionKeepsItsExactShapeAndVersionEnvelope()
    {
        var employee = await Json(await Send("POST", "/api/employees", new { userId = 100, employeeCode = "CAREER100", firstName = "Career", lastName = "Admin", joiningDate = "2024-01-01" }));
        var id = employee.GetProperty("id").GetInt64();
        var version = employee.GetProperty("version").GetUInt32();

        
        async Task<JsonElement> Change(string method, string path, object? body)
        {
            var response = await Send(method, path, body, version);
            var json = await Json(response);
            Assert.Equal(Expect("item", "version"), Keys(json));
            var next = json.GetProperty("version").GetUInt32();
            Assert.NotEqual(version, next);
            Assert.Equal($"\"{next}\"", response.Headers.ETag!.Tag);
            version = next;
            return json.GetProperty("item");
        }

        
        var skill = await Change("POST", "/api/employees/me/skills", new { skillName = "CSharp", proficiency = 3, yearsOfExperience = 2.5, lastUsedDate = "2025-01-01" });
        Assert.Equal(SkillSavedKeys, Keys(skill));
        var skillId = skill.GetProperty("id").GetInt64();
        Assert.Equal(SkillGetKeys, Keys((await Json(await Send("GET", $"/api/employees/{id}/skills"))).EnumerateArray().First()));
        Assert.Equal("CSharp", (await Json(await Send("GET", "/api/employees/me/skills"))).EnumerateArray().First().GetProperty("skillName").GetString());
        Assert.Equal(SkillSavedKeys, Keys(await Change("PUT", $"/api/employees/me/skills/{skillId}", new { skillName = "CSharp", proficiency = 4, yearsOfExperience = 3 })));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/api/employees/me/skills", new { skillName = "CSharp", proficiency = 9, yearsOfExperience = 1 }, version)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", "/api/employees/me/skills", new { skillName = "CSharp", proficiency = 2, yearsOfExperience = 1 }, version)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("PUT", "/api/employees/me/skills/999999", new { skillName = "CSharp", proficiency = 2, yearsOfExperience = 1 }, version)).StatusCode);

        
        var education = await Change("POST", "/api/employees/me/education", new { institution = "Uni", qualification = "BSc", startDate = "2010-01-01", endDate = "2014-01-01", fieldOfStudy = " CS " });
        Assert.Equal(EducationKeys, Keys(education));
        Assert.Equal("CS", education.GetProperty("fieldOfStudy").GetString());
        var educationId = education.GetProperty("id").GetInt64();
        Assert.Equal(EducationKeys, Keys((await Json(await Send("GET", "/api/employees/me/education"))).EnumerateArray().First()));
        Assert.Equal(EducationKeys, Keys(await Change("PUT", $"/api/employees/me/education/{educationId}", new { institution = "Uni", qualification = "MSc", startDate = "2010-01-01", endDate = "2014-01-01" })));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/api/employees/me/education", new { institution = "Bad", qualification = "x", startDate = "2020-01-01", endDate = "2019-01-01" }, version)).StatusCode);

        
        var certification = await Change("POST", "/api/employees/me/certifications", new { name = "Cert", issuer = "Issuer", issuedDate = "2023-01-01", credentialId = " C-1 " });
        Assert.Equal(CertificationKeys, Keys(certification));
        Assert.Equal("C-1", certification.GetProperty("credentialId").GetString());
        var certificationId = certification.GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/api/employees/me/certifications", new { name = "Bad url", issuer = "I", issuedDate = "2023-01-01", credentialUrl = "javascript:alert(1)" }, version)).StatusCode);

        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=");
        MultipartFormDataContent Form(string fileName)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return new MultipartFormDataContent { { file, "file", fileName } };
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/api/employees/me/certifications/upload", version: version, content: Form("notes.txt"))).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await Send("POST", "/api/employees/me/certifications/upload", content: Form("proof.png"))).StatusCode);
        var uploaded = await Json(await Send("POST", "/api/employees/me/certifications/upload", version: version, content: Form("proof.png")));
        Assert.Equal(Expect("fileUrl", "fileName"), Keys(uploaded));
        Assert.Equal("proof.png", uploaded.GetProperty("fileName").GetString());
        var fileUrl = uploaded.GetProperty("fileUrl").GetString()!;
        Assert.Matches($"^/api/employees/{id}/certifications/file/[0-9a-f]{{32}}\\.png$", fileUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("GET", fileUrl)).StatusCode);   
        Assert.Equal(CertificationKeys, Keys(await Change("PUT", $"/api/employees/me/certifications/{certificationId}", new { name = "Cert", issuer = "Issuer", issuedDate = "2023-01-01", fileUrl, fileName = "proof.png" })));
        var download = await Send("GET", fileUrl);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("proof.png", download.Content.Headers.ContentDisposition!.ToString());
        Assert.Equal(CertificationKeys, Keys((await Json(await Send("GET", "/api/employees/me/certifications"))).EnumerateArray().First()));

        
        var experience = await Change("POST", "/api/employees/me/experience", new { company = "Co", title = "Dev", description = "Did things", startDate = "2018-01-01", endDate = "2020-01-01" });
        Assert.Equal(ExperienceKeys, Keys(experience));
        var experienceId = experience.GetProperty("id").GetInt64();
        Assert.Equal(ExperienceKeys, Keys((await Json(await Send("GET", "/api/employees/me/experience"))).EnumerateArray().First()));
        Assert.Equal(ExperienceKeys, Keys(await Change("PUT", $"/api/employees/me/experience/{experienceId}", new { company = "Co", title = "Senior Dev", startDate = "2018-01-01", endDate = "2020-01-01" })));

        
        var language = await Change("POST", "/api/employees/me/languages", new { name = "English", proficiency = "Native" });
        Assert.Equal(LanguageKeys, Keys(language));
        var languageId = language.GetProperty("id").GetInt64();
        Assert.Equal(LanguageKeys, Keys((await Json(await Send("GET", "/api/employees/me/languages"))).EnumerateArray().First()));
        Assert.Equal(LanguageKeys, Keys(await Change("PUT", $"/api/employees/me/languages/{languageId}", new { name = "English", proficiency = "Fluent" })));

        
        var compensation = await Change("POST", $"/api/employees/{id}/compensation", new { ctc = 100000, bonus = 5000, currency = "INR", effectiveFrom = "2024-01-01", effectiveTo = "2024-12-31" });
        Assert.Equal(CompensationKeys, Keys(compensation));
        var compensationId = compensation.GetProperty("id").GetInt64();
        Assert.Equal(CompensationKeys, Keys((await Json(await Send("GET", $"/api/employees/{id}/compensation"))).EnumerateArray().First()));
        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", $"/api/employees/{id}/compensation", new { ctc = 1, bonus = 1, currency = "INR", effectiveFrom = "2024-06-01", effectiveTo = (string?)null }, version)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", $"/api/employees/{id}/compensation", new { ctc = 1, bonus = 1, currency = "inr", effectiveFrom = "2025-06-01", effectiveTo = (string?)null }, version)).StatusCode);
        Assert.Equal(CompensationKeys, Keys(await Change("PUT", $"/api/employees/{id}/compensation/{compensationId}", new { ctc = 120000, bonus = 5000, currency = "INR", effectiveFrom = "2024-01-01", effectiveTo = "2024-12-31" })));

        
        foreach (var (path, entry) in new[] { ("skills", skillId), ("education", educationId), ("certifications", certificationId), ("experience", experienceId), ("languages", languageId) })
        {
            var deleted = await Change("DELETE", $"/api/employees/me/{path}/{entry}", null);
            Assert.Equal(Expect("deletedId"), Keys(deleted));
            Assert.Equal(entry, deleted.GetProperty("deletedId").GetInt64());
            Assert.Equal(HttpStatusCode.NotFound, (await Send("DELETE", $"/api/employees/me/{path}/{entry}", null, version)).StatusCode);
        }
        Assert.Equal(compensationId, (await Change("DELETE", $"/api/employees/{id}/compensation/{compensationId}", null)).GetProperty("deletedId").GetInt64());
        Assert.Empty((await Json(await Send("GET", "/api/employees/me/skills"))).EnumerateArray());
    }

    [Fact]
    public async Task SectionsNeedAVersionAndOnlyTheOwnerOrAnUpdaterMayChangeThem()
    {
        var mine = await Json(await Send("POST", "/api/employees", new { userId = 101, employeeCode = "OWN101", firstName = "Own", lastName = "Dev", joiningDate = "2024-01-01" }));
        var theirs = await Json(await Send("POST", "/api/employees", new { userId = 102, employeeCode = "OWN102", firstName = "Other", lastName = "Dev", joiningDate = "2024-01-01" }));
        var theirId = theirs.GetProperty("id").GetInt64();
        var body = new { name = "English", proficiency = "Native" };

        Assert.Equal((HttpStatusCode)428, (await Send("POST", "/api/employees/me/languages", body, user: 101)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", "/api/employees/me/languages", body, 0, user: 101)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send("POST", "/api/employees/me/languages", body, mine.GetProperty("version").GetUInt32(), user: 101)).StatusCode);
        
        string[] developer = ["EMPLOYEE_VIEW", "EMPLOYEE_UPDATE_SELF"];
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("POST", $"/api/employees/{theirId}/languages", body, theirs.GetProperty("version").GetUInt32(), user: 101, permissions: developer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("GET", $"/api/employees/{theirId}/languages", user: 101, permissions: developer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("GET", $"/api/employees/{theirId}/compensation", user: 101)).StatusCode);
    }
}
