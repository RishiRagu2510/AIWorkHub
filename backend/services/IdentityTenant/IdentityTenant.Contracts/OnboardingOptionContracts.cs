namespace IdentityTenant.Contracts;

public sealed record OnboardingReviewerDto(long UserId, string[] Roles);


public sealed record OnboardingValidationDto(object Validated, IReadOnlyList<OnboardingReviewerDto> Reviewers);

public sealed record OnboardingReferenceDto(long Id, string Kind, string Name);
public sealed record OnboardingProfileDto(long Id, string Name);
public sealed record OnboardingLevelDto(long Id, long CareerProfileId, long GradeId, string Grade, string CareerTitle);
public sealed record OnboardingManagerDto(long Id, string FirstName, string LastName, string EmployeeCode, string WorkEmail);
public sealed record OnboardingTeamDto(long Id, string Name);


public sealed record OnboardingOptionsDto(
    IReadOnlyList<OnboardingReferenceDto> References, IReadOnlyList<OnboardingProfileDto> Profiles, IReadOnlyList<OnboardingLevelDto> Levels,
    IReadOnlyList<OnboardingManagerDto> Managers, IReadOnlyList<OnboardingTeamDto> Teams,
    string[] EmploymentTypes, string[] EmploymentStatuses, string[] Designations);
