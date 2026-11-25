using System.Net;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;

public class ProfileSectionToolTests
{
    private static Dictionary<string, object?> Args(string json)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        args[IssueVisibility.CallerRolesArgument] = "DEVELOPER";
        args[IssueVisibility.CallerUserIdArgument] = 15L;
        return args;
    }

    private static JsonDocument Data(ToolResult result)
    {
        Assert.True(result.Success, result.Error);
        return JsonDocument.Parse((string)result.Data!);
    }

    private static Routes Services() => new Routes()
        .Get("/api/employees/me/experience", """[{"id":3,"company":"Acme","title":"Developer","description":"Built APIs","startDate":"2019-01-01","endDate":"2021-06-30"}]""")
        .Get("/api/employees/me/education", """[{"id":4,"institution":"Anna University","qualification":"B.E.","fieldOfStudy":"Computer Science","startDate":"2015-06-01","endDate":"2019-05-01"}]""")
        .Get("/api/employees/me/certifications", """[{"id":5,"name":"AZ-900","issuer":"Microsoft","issuedDate":"2023-02-01","expiryDate":null,"credentialId":"C1","credentialUrl":"https://example.com/c1","fileUrl":"/files/c1.pdf","fileName":"c1.pdf"}]""")
        .Get("/api/employees/me/languages", """[{"id":6,"name":"English","proficiency":"Native / Bilingual"}]""")
        .Get("/api/employees/me", """{"id":9,"userId":15,"version":42,"preferredName":"Asha","phone":"98765","aadharNumber":"1234-5678","permanentAddress":"1 Main Road","emergencyContactName":"Ravi","emergencyContactPhone":"11111","emergencyContactRelation":"Brother"}""");

    private static Task<ToolResult> Run(Routes routes, string json, out RecordingHandler handler)
    {
        var (h, factory) = routes.Build();
        handler = h;
        return new PrepareProfileSectionTool(factory, new HttpContextAccessor()).ExecuteAsync(Args(json));
    }

    [Theory]
    [InlineData("experience", "company", "Acme")]
    [InlineData("education", "institution", "Anna University")]
    [InlineData("certifications", "name", "AZ-900")]
    [InlineData("languages", "name", "English")]
    public async Task EachTab_ListsWhatIsOnTheProfile_AndTheVersionToSaveAgainst(string section, string field, string expected)
    {
        var result = await Run(Services(), $$"""{"section":"{{section}}"}""", out _);

        using var data = Data(result);
        Assert.Equal("profileSection", data.RootElement.GetProperty("form").GetString());
        Assert.Equal(section, data.RootElement.GetProperty("section").GetString());
        Assert.Equal(42, data.RootElement.GetProperty("version").GetInt64());
        var items = data.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(expected, items[0].GetProperty(field).GetString());
        Assert.True(items[0].GetProperty("id").GetInt64() > 0);
    }

    [Fact]
    public async Task Certifications_KeepTheUploadedFile_SoEditingDoesNotLoseIt()
    {
        var result = await Run(Services(), """{"section":"certifications"}""", out _);

        using var data = Data(result);
        var item = data.RootElement.GetProperty("items")[0];
        Assert.Equal("/files/c1.pdf", item.GetProperty("fileUrl").GetString());
        Assert.Equal("c1.pdf", item.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Personal_ShowsContactDetails_ButNeverTheGovernmentIdOrAddress()
    {
        var result = await Run(Services(), """{"section":"personal"}""", out _);

        var text = (string)result.Data!;
        using var data = JsonDocument.Parse(text);
        var values = data.RootElement.GetProperty("values");
        Assert.Equal("Asha", values.GetProperty("preferredName").GetString());
        Assert.Equal("98765", values.GetProperty("phone").GetString());
        Assert.Equal("Ravi", values.GetProperty("emergencyContactName").GetString());
        Assert.DoesNotContain("1234-5678", text);
        Assert.DoesNotContain("Main Road", text);
    }

    [Fact]
    public async Task WhatTheUserSaid_OpensTheFormFilledIn()
    {
        var result = await Run(Services(),
            """{"section":"certifications","name":"AWS Solutions Architect","organization":"Amazon","issuedDate":"2024-03-10","credentialUrl":"https://example.com/aws"}""", out _);

        using var data = Data(result);
        var prefill = data.RootElement.GetProperty("prefill");
        Assert.Equal("AWS Solutions Architect", prefill.GetProperty("name").GetString());
        Assert.Equal("Amazon", prefill.GetProperty("issuer").GetString());
        Assert.Equal("2024-03-10", prefill.GetProperty("issuedDate").GetString());
        Assert.Equal("https://example.com/aws", prefill.GetProperty("credentialUrl").GetString());
        Assert.False(prefill.TryGetProperty("expiryDate", out _));   
    }

    [Fact]
    public async Task Experience_MapsOrganizationAndTitleToCompanyAndJobTitle()
    {
        var result = await Run(Services(), """{"section":"experience","organization":"Globex","title":"Lead","startDate":"2022-01-01"}""", out _);

        using var data = Data(result);
        var prefill = data.RootElement.GetProperty("prefill");
        Assert.Equal("Globex", prefill.GetProperty("company").GetString());
        Assert.Equal("Lead", prefill.GetProperty("title").GetString());
        Assert.Equal("2022-01-01", prefill.GetProperty("startDate").GetString());
    }

    [Fact]
    public async Task Education_MapsOrganizationAndTitleToInstitutionAndDegree()
    {
        var result = await Run(Services(), """{"section":"education","organization":"IIT Madras","title":"M.Tech","fieldOfStudy":"AI"}""", out _);

        using var data = Data(result);
        var prefill = data.RootElement.GetProperty("prefill");
        Assert.Equal("IIT Madras", prefill.GetProperty("institution").GetString());
        Assert.Equal("M.Tech", prefill.GetProperty("qualification").GetString());
        Assert.Equal("AI", prefill.GetProperty("fieldOfStudy").GetString());
    }

    [Theory]
    [InlineData("fluent", "Full Professional")]
    [InlineData("native", "Native / Bilingual")]
    [InlineData("conversational", "Limited Working")]
    [InlineData("beginner", "Elementary")]
    [InlineData("3", "Professional Working")]
    [InlineData("Professional Working", "Professional Working")]
    public async Task Language_LevelIsMatchedToOneOfTheFiveLevels(string said, string level)
    {
        var result = await Run(Services(), $$"""{"section":"languages","name":"Hindi","proficiency":"{{said}}"}""", out _);

        using var data = Data(result);
        Assert.Equal(level, data.RootElement.GetProperty("prefill").GetProperty("proficiency").GetString());
        Assert.Equal(5, data.RootElement.GetProperty("languageLevels").GetArrayLength());
    }

    [Fact]
    public async Task Language_ALevelItDoesNotKnow_IsLeftForThePersonToChoose()
    {
        var result = await Run(Services(), """{"section":"languages","name":"Hindi","proficiency":"so-so"}""", out _);

        using var data = Data(result);
        var prefill = data.RootElement.GetProperty("prefill");
        Assert.Equal("Hindi", prefill.GetProperty("name").GetString());
        Assert.False(prefill.TryGetProperty("proficiency", out _));
    }

    [Theory]
    [InlineData("""{"section":"skills"}""")]
    [InlineData("""{"section":"compensation"}""")]
    [InlineData("""{}""")]
    public async Task AnUnknownTab_IsRefused_BeforeAnythingIsRead(string json)
    {
        var result = await Run(Services(), json, out var handler);

        Assert.False(result.Success);
        Assert.Contains("section must be one of", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData("""{"section":"experience","startDate":"01/02/2020"}""", "startDate must be written as YYYY-MM-DD")]
    [InlineData("""{"section":"experience","startDate":"2022-01-01","endDate":"2021-01-01"}""", "endDate is before startDate")]
    [InlineData("""{"section":"certifications","issuedDate":"2024-01-01","expiryDate":"2023-01-01"}""", "expiryDate is before issuedDate")]
    [InlineData("""{"section":"certifications","credentialUrl":"http://example.com"}""", "credentialUrl must be an https link")]
    public async Task BadDetails_AreRefused_SoTheModelCanCorrectThem(string json, string message)
    {
        var result = await Run(Services(), json, out var handler);

        Assert.False(result.Success);
        Assert.Contains(message, result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task WhenTheProfileCannotBeLoaded_SaysSo()
    {
        var routes = new Routes().Get("/api/employees/me", "{}", HttpStatusCode.ServiceUnavailable);

        var result = await Run(routes, """{"section":"education"}""", out _);

        Assert.False(result.Success);
        Assert.Contains("could not load your profile", result.Error);
    }

    [Fact]
    public async Task WhenTheTabCannotBeLoaded_SaysSo()
    {
        var routes = new Routes()
            .Get("/api/employees/me/languages", "{}", HttpStatusCode.ServiceUnavailable)
            .Get("/api/employees/me", """{"id":9,"version":42}""");

        var result = await Run(routes, """{"section":"languages"}""", out _);

        Assert.False(result.Success);
        Assert.Contains("could not load what is on your profile", result.Error);
    }

    [Fact]
    public void ItIsAFormTool_ThatNeedsOnlyTheSelfServicePermission_AndWritesNothingItself()
    {
        var tool = new PrepareProfileSectionTool(new FakeHttpClientFactory(new RecordingHandler(_ => Http.Json("{}", HttpStatusCode.OK))), new HttpContextAccessor());

        Assert.Equal("EMPLOYEE_UPDATE_SELF", tool.RequiredPermission);
        Assert.Equal(ToolActivityKind.Form, ToolActivity.Classify(tool.Name));
    }
}
