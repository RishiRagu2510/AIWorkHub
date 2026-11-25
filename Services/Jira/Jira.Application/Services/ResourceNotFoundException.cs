namespace Jira.Application.Services;

public sealed class ResourceNotFoundException() : Exception("The requested resource was not found.");
