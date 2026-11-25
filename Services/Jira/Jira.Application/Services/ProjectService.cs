using Jira.Application.Abstractions.Persistence;
using Jira.Application.DTOs;

namespace Jira.Application.Services;

public sealed class ProjectService
{
    private readonly IProjectRepository _projectRepository;

    public ProjectService(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<ProjectDto> UpdateAsync(long tenantId, long projectId, Jira.Contracts.Projects.UpdateProjectRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<Jira.Domain.Enums.ProjectType>(request.ProjectType, true, out var type) || !Enum.IsDefined(type))
            throw new ArgumentException("Invalid project type.");
        Jira.Domain.Enums.ProjectStatus? status = null;
        if (request.Status is not null)
        {
            if (!Enum.TryParse<Jira.Domain.Enums.ProjectStatus>(request.Status, true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ArgumentException("Invalid project status.");
            status = parsed;
        }
        return Map(await _projectRepository.UpdateAsync(tenantId, projectId, request.Name, request.Description, type, status, ct, request.AccountId, request.DisplayName));
    }

    public async Task<ProjectDto> MoveAsync(long tenantId, long projectId, long? parentProjectId, CancellationToken ct)
        => Map(await _projectRepository.MoveAsync(tenantId, projectId, parentProjectId, ct));

    public Task DeleteAsync(long tenantId, long projectId, CancellationToken ct)
        => _projectRepository.DeleteAsync(tenantId, projectId, ct);

    public async Task<ProjectDto> CreateAsync(long tenantId, Jira.Contracts.Projects.CreateProjectRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<Jira.Domain.Enums.ProjectType>(request.ProjectType, true, out var projectType) || !Enum.IsDefined(projectType))
            throw new ArgumentException("Invalid project type.");
        if (request.ParentProjectId.HasValue)
        {
            var parent = await _projectRepository.GetByIdAsync(tenantId, request.ParentProjectId.Value, ct);
            if (parent is null) throw new ResourceNotFoundException();
            if (parent.Status != Jira.Domain.Enums.ProjectStatus.Active)
                throw new ArgumentException("Subprojects require an active parent project.");
        }
        var project = new Jira.Domain.Entities.Project(tenantId, request.Key, request.Name,
            request.Description, projectType, parentProjectId: request.ParentProjectId);
        project.SelectAccount(request.AccountId);
        project.Rename(request.DisplayName);
        await _projectRepository.AddAsync(project, ct);
        return Map(project);
    }

    public async Task<IReadOnlyCollection<ProjectDto>> GetChildrenAsync(long tenantId, long parentProjectId, CancellationToken ct)
    {
        if (await _projectRepository.GetByIdAsync(tenantId, parentProjectId, ct) is null) throw new ResourceNotFoundException();
        return (await _projectRepository.GetChildrenAsync(tenantId, parentProjectId, ct)).Select(Map).ToArray();
    }

    public async Task<ProjectDto?> GetByIdAsync(
        long tenantId,
        long projectId,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(
            tenantId,
            projectId,
            cancellationToken);

        return project is null
            ? null
            : Map(project);
    }

    public async Task<IReadOnlyCollection<ProjectDto>> GetAllAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        var projects = await _projectRepository.GetAllAsync(
            tenantId,
            cancellationToken);

        return projects
            .Select(Map)
            .ToArray();
    }

    private static ProjectDto Map(
        Jira.Domain.Entities.Project project)
    {
        return new ProjectDto(
            project.Id,
            project.TenantId,
            project.Key,
            project.Name,
            project.Description,
            project.ProjectType.ToString().ToUpperInvariant(),
            project.Status.ToString().ToUpperInvariant(),
            project.ProjectLeadUserId,
            project.ParentProjectId,
            project.Version, project.AccountId, project.DisplayName);
    }
}