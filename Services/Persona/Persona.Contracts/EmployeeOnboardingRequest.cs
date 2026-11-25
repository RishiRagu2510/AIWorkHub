using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Persona.Contracts;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeOnboardingRequest(
 [Required,EmailAddress,StringLength(320)] string WorkEmail,
 JsonElement Employee,
 [StringLength(4000)] string? Reason=null,
 [StringLength(4000)] string? Notes=null,
 [StringLength(2000)] string? RequestedAccess=null,
 long? TeamId=null);
