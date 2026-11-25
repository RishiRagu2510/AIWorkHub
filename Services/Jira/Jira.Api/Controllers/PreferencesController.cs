using Jira.Contracts.Collaboration;
using Jira.Infrastructure.Persistence.Preferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jira.Api.Controllers;


[ApiController, Authorize, Route("api/preferences")]
public sealed class PreferencesController(PreferenceService preferences) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken ct) => Ok(await preferences.Read(ct));

    [HttpPut("{key}")]
    public async Task<IActionResult> Save(string key, UpdatePreferenceRequest request, CancellationToken ct)
    {
        var result = await preferences.Save(key, request, ct);
        return result.Outcome switch
        {
            PreferenceOutcome.Saved => Ok(result.Preference),
            PreferenceOutcome.Invalid => BadRequest(),
            _ => Conflict()
        };
    }
}
