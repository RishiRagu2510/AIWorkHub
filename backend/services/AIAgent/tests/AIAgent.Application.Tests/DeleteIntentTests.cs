using AIAgent.Application.Services;
using Xunit;

namespace AIAgent.Application.Tests;

public class DeleteIntentTests
{
    [Theory]
    [InlineData("delete my work log", "worklog")]
    [InlineData("Delete a time log", "worklog")]
    [InlineData("remove my time entry", "worklog")]
    [InlineData("delete the hours I logged", "")]   
    [InlineData("cancel my leave", "leave")]
    [InlineData("cancel my leave request", "leave")]
    [InlineData("delete my WFH request", "leave")]
    [InlineData("remove a skill", "skills")]
    [InlineData("delete my React skill", "skills")]
    [InlineData("remove my certificate", "certifications")]
    [InlineData("delete a certification", "certifications")]
    [InlineData("delete my degree", "education")]
    [InlineData("remove my education", "education")]
    [InlineData("delete my previous job", "experience")]
    [InlineData("delete work experience", "experience")]
    [InlineData("remove a language", "languages")]
    [InlineData("delete my comment", "comments")]
    [InlineData("delete my comment on FLP360-17", "comments")]
    public void OpensTheFormForTheKindNamed(string message, string kind)
    {
        var opens = DeleteIntent.WantsForm(message, out var found, out _);
        if (kind.Length == 0) { Assert.False(opens, message); return; }
        Assert.True(opens, message);
        Assert.Equal(kind, found);
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("Delete something")]
    [InlineData("I want to delete something")]
    [InlineData("can you delete anything please")]
    [InlineData("remove an entry")]
    [InlineData("delete stuff from my profile")]
    public void OpensTheFormWithNoKindWhenNothingIsNamed(string message)
    {
        Assert.True(DeleteIntent.WantsForm(message, out var kind, out _), message);
        Assert.Equal("", kind);
    }

    [Fact]
    public void PassesTheIssueKeyForComments()
    {
        Assert.True(DeleteIntent.WantsForm("remove my comment on flp360-17", out var kind, out var key));
        Assert.Equal("comments", kind);
        Assert.Equal("FLP360-17", key);
    }

    [Theory]
    [InlineData("delete issue FLP360-17")]
    [InlineData("delete the project")]
    [InlineData("delete my account")]
    [InlineData("delete this chat")]
    [InlineData("remove the attachment")]
    [InlineData("delete Vetri's skills")]
    [InlineData("remove all employees")]
    [InlineData("delete their work log")]
    [InlineData("delete the sprint")]
    [InlineData("cancel the meeting")]
    [InlineData("cancel")]
    [InlineData("show my skills")]
    [InlineData("add a skill")]
    [InlineData("what is the leave policy")]
    [InlineData("remove my skill and also delete the project")]
    [InlineData("")]
    [InlineData(null)]
    public void LeavesEverythingElseToTheModel(string? message) => Assert.False(DeleteIntent.WantsForm(message), message);

    [Fact]
    public void IgnoresLongMessages() => Assert.False(DeleteIntent.WantsForm("delete my skill " + new string('x', 200)));
}
