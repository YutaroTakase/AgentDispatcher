using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;

namespace AgentDispatcher.Api.Projects;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects");

        group.MapGet("/", async (IProjectRepository repository, CancellationToken cancellationToken) =>
        {
            var projects = await repository.ListAsync(cancellationToken);
            return Results.Ok(projects.Select(ToResponse));
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            IProjectRepository repository,
            CancellationToken cancellationToken) =>
        {
            var project = await repository.GetAsync(id, cancellationToken);
            return project is null ? Results.NotFound() : Results.Ok(ToResponse(project));
        });

        group.MapPost("/", async (
            ProjectRequest request,
            IProjectRepository projectRepository,
            IIssueSelectorRepository selectorRepository,
            IRoutingConfigurationRepository routingRepository,
            IGitHubIssueSource gitHub,
            CancellationToken cancellationToken) =>
        {
            var validation = TryBuildConfiguration(
                null,
                request,
                out var project,
                out var selector,
                out var defaultRoute);

            if (validation is not null)
            {
                return validation;
            }

            var repositoryCheck = await gitHub.CheckRepositoryAsync(
                project!,
                cancellationToken);
            if (!repositoryCheck.Success)
            {
                return Results.UnprocessableEntity(new
                {
                    message = repositoryCheck.Error
                });
            }

            await projectRepository.AddAsync(project!, cancellationToken);

            try
            {
                await selectorRepository.UpsertAsync(selector!, cancellationToken);
                await routingRepository.UpsertDefaultRouteAsync(
                    project!.Id,
                    defaultRoute!,
                    cancellationToken);
            }
            catch
            {
                await projectRepository.DeleteAsync(project!.Id, CancellationToken.None);
                throw;
            }

            return Results.Created($"/api/projects/{project!.Id}", ToResponse(project));
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            ProjectRequest request,
            IProjectRepository projectRepository,
            IIssueSelectorRepository selectorRepository,
            IRoutingConfigurationRepository routingRepository,
            IGitHubIssueSource gitHub,
            CancellationToken cancellationToken) =>
        {
            var current = await projectRepository.GetAsync(id, cancellationToken);
            if (current is null)
            {
                return Results.NotFound();
            }

            var validation = TryBuildConfiguration(
                current,
                request,
                out var project,
                out var selector,
                out var defaultRoute);

            if (validation is not null)
            {
                return validation;
            }

            var repositoryCheck = await gitHub.CheckRepositoryAsync(
                project!,
                cancellationToken);
            if (!repositoryCheck.Success)
            {
                return Results.UnprocessableEntity(new
                {
                    message = repositoryCheck.Error
                });
            }

            await projectRepository.UpdateAsync(project!, cancellationToken);
            await selectorRepository.UpsertAsync(selector!, cancellationToken);
            await routingRepository.UpsertDefaultRouteAsync(
                project!.Id,
                defaultRoute!,
                cancellationToken);

            return Results.Ok(ToResponse(project));
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IProjectRepository repository,
            CancellationToken cancellationToken) =>
        {
            return await repository.DeleteAsync(id, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        });

        return endpoints;
    }

    private static IResult? TryBuildConfiguration(
        Project? current,
        ProjectRequest request,
        out Project? project,
        out IssueSelectorSettings? selector,
        out ExecutionRoute? defaultRoute)
    {
        try
        {
            project = current is null
                ? Project.Create(
                    request.DisplayName,
                    request.Repository,
                    request.Enabled,
                    request.DefaultBranch,
                    request.IssueScanIntervalMinutes,
                    request.MaxConcurrentExecutions,
                    request.ExecutionRetentionDays,
                    request.FailureWorktreeRetentionDays)
                : current.Update(
                    request.DisplayName,
                    request.Repository,
                    request.Enabled,
                    request.DefaultBranch,
                    request.IssueScanIntervalMinutes,
                    request.MaxConcurrentExecutions,
                    request.ExecutionRetentionDays,
                    request.FailureWorktreeRetentionDays);

            selector = IssueSelectorSettings.Create(
                project.Id,
                request.IssueQueryFragment,
                request.IssueSortField,
                request.IssueSortOrder,
                request.MaxCandidatesPerScan);

            defaultRoute = ExecutionRoute.Create(
                request.DefaultModelIdentifier,
                request.DefaultReasoningEffort);

            return null;
        }
        catch (ArgumentException exception)
        {
            project = null;
            selector = null;
            defaultRoute = null;
            return ValidationProblem(exception);
        }
    }

    private static IResult ValidationProblem(ArgumentException exception)
    {
        var key = string.IsNullOrWhiteSpace(exception.ParamName)
            ? "project"
            : exception.ParamName;

        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [exception.Message]
        });
    }

    private static ProjectResponse ToResponse(Project project)
    {
        return new ProjectResponse(
            project.Id,
            project.DisplayName,
            project.Repository,
            project.Enabled,
            project.DefaultBranch,
            project.IssueScanIntervalMinutes,
            project.MaxConcurrentExecutions,
            project.ExecutionRetentionDays,
            project.FailureWorktreeRetentionDays);
    }
}

public sealed record ProjectRequest(
    string DisplayName,
    string Repository,
    bool Enabled,
    string DefaultBranch,
    int IssueScanIntervalMinutes,
    int MaxConcurrentExecutions,
    int ExecutionRetentionDays,
    int FailureWorktreeRetentionDays,
    string? IssueQueryFragment,
    string IssueSortField,
    string IssueSortOrder,
    int MaxCandidatesPerScan,
    string DefaultModelIdentifier,
    string DefaultReasoningEffort);

public sealed record ProjectResponse(
    Guid Id,
    string DisplayName,
    string Repository,
    bool Enabled,
    string DefaultBranch,
    int IssueScanIntervalMinutes,
    int MaxConcurrentExecutions,
    int ExecutionRetentionDays,
    int FailureWorktreeRetentionDays);
