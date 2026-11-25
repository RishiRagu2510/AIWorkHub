namespace Jira.Application.Services;

public sealed class ProjectConflictException(string message = "A project with this key already exists in your company.") : Exception(message);
