using AgentDispatcher.Domain.Projects;

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
            IProjectRepository repository,
            CancellationToken cancellationToken) =>
        {
            var validation = TryCreateProject(request, out var project);
            if (validation is not null)
            {
                return validation;
            }

            await repository.AddAsync(project!, cancellationToken);
            return Results.Created($"/api/projects/{project!.Id}", ToResponse(project));
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            ProjectRequest request,
            IProjectRepository repository,
            CancellationToken cancellationToken) =>
        {
            var current = await repository.GetAsync(id, cancellationToken);
            if (current is null)
            {
                return Results.NotFound();
            }

            var validation = TryUpdateProject(current, request, out var updated);
            if (validation is not null)
            {
                return validation;
            }

            await repository.UpdateAsync(updated!, cancellationToken);
            return Results.Ok(ToResponse(updated!));
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

    private static IResult? TryCreateProject(ProjectRequest request, out Project? project)
    {
        try
        {
            project = Project.Create(
                request.DisplayName,
                request.Repository,
                request.Enabled,
                request.DefaultBranch,
                request.IssueScanIntervalMinutes,
                request.MaxConcurrentExecutions,
                request.ExecutionRetentionDays,
                request.FailureWorktreeRetentionDays);
            return null;
        }
        catch (ArgumentException exception)
        {
            project = null;
            return ValidationProblem(exception);
        }
    }

    private static IResult? TryUpdateProject(Project current, ProjectRequest request, out Project? project)
    {
        try
        {
            project = current.Update(
                request.DisplayName,
                request.Repository,
                request.Enabled,
                request.DefaultBranch,
                request.IssueScanIntervalMinutes,
                request.MaxConcurrentExecutions,
                request.ExecutionRetentionDays,
                request.FailureWorktreeRetentionDays);
            return null;
        }
        catch (ArgumentException exception)
        {
            project = null;
            return ValidationProblem(exception);
        }
    }

    private static IResult ValidationProblem(ArgumentException exception)
    {
        var key = string.IsNullOrWhiteSpace(exception.ParamName) ? "project" : exception.ParamName;
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
    int FailureWorktreeRetentionDays);

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
