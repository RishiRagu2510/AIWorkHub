using AIAgent.Application.Services;
using Xunit;

namespace AIAgent.Application.Tests;

public class SkillsIntentTests
{
    [Theory]
    [InlineData("update my skills")]
    [InlineData("Update my skills.")]
    [InlineData("edit my skills")]
    [InlineData("change my skills please")]
    [InlineData("manage my skills")]
    [InlineData("add a skill")]
    [InlineData("add new skill")]
    [InlineData("add skills")]
    [InlineData("I want to update my skills")]
    [InlineData("can you update my technical skills")]
    [InlineData("  UPDATE MY SKILLS  ")]
    public void OpensTheFormWhenNoSkillIsNamed(string message) => Assert.True(SkillsIntent.WantsForm(message), message);

    [Theory]
    [InlineData("add Kubernetes skill level 4")]
    [InlineData("add skill Kubernetes")]
    [InlineData("update my React skill to 5")]
    [InlineData("show my skills")]
    [InlineData("what skills does Vetri have")]
    [InlineData("list my skills")]
    [InlineData("remove my skills")]
    [InlineData("update my profile")]
    [InlineData("")]
    [InlineData(null)]
    public void LeavesEverythingElseToTheModel(string? message) => Assert.False(SkillsIntent.WantsForm(message), message);
}
