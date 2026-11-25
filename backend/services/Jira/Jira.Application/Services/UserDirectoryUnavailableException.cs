namespace Jira.Application.Services;
public sealed class UserDirectoryUnavailableException(Exception inner)
    : Exception("User directory is unavailable. Please try again.", inner);
