using System;
using System.Collections.Generic;
using Jira.Domain.Entities;

namespace Jira.Application.Abstractions.Persistence;

public sealed class IssueSearchResult
{
    public IReadOnlyList<Issue> Items { get; init; }
        = Array.Empty<Issue>();

    public int TotalCount { get; init; }
}