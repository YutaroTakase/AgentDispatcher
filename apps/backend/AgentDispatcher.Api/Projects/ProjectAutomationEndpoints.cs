using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;

namespace AgentDispatcher.Api.Projects;

public static class ProjectAutomationEndpoints
{
    public static IEndpointRouteBuilder MapProjectAutomationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects/{id:guid}");

        group.MapGet("/repository-check", CheckRepositoryAsync);
        group.MapGet("/issues/preview", PreviewIssuesAsync);
        group.MapGet("/issue-selector", GetIssueSelectorAsync);
        group.MapPut("/issue-selector", PutIssueSelectorAsync);
        group.MapGet("/routing/default", GetDefaultRouteAsync);
        group.MapPut("/routing/default", PutDefaultRouteAsync);
        group.MapGet("/routing/rules", GetRoutingRulesAsync);
        group.MapPut("/routing/rules", PutRoutingRulesAsync);

        return endpoints;
    }

    private static async Task<IResult> CheckRepositoryAsync(
        Guid id,
        IProjectRepository projects,
        IGitHubIssueSource source,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(id, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var result = await source.CheckRepositoryAsync(project, cancellationToken);
        return result.Success
            ? Results.Ok(result)
            : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> PreviewIssuesAsync(
        Guid id,
        IProjectRepository projects,
        IIssueSelectorRepository selectors,
        IGitHubIssueSource source,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(id, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var selector = await selectors.GetAsync(id, cancellationToken);
        if (selector is null)
        {
            return Results.Conflict(new
            {
                message = "Issue取得条件が設定されていません。"
            });
        }

        var result = await source.SearchIssuesAsync(project, selector, cancellationToken);
        return result.Success
            ? Results.Ok(result.Issues)
            : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> GetIssueSelectorAsync(
        Guid id,
        IProjectRepository projects,
        IIssueSelectorRepository selectors,
        CancellationToken cancellationToken)
    {
        if (await projects.GetAsync(id, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var selector = await selectors.GetAsync(id, cancellationToken);
        return selector is null ? Results.NotFound() : Results.Ok(selector);
    }

    private static async Task<IResult> PutIssueSelectorAsync(
        Guid id,
        IssueSelectorRequest request,
        IProjectRepository projects,
        IIssueSelectorRepository selectors,
        CancellationToken cancellationToken)
    {
        if (await projects.GetAsync(id, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        try
        {
            var selector = IssueSelectorSettings.Create(
                id,
                request.QueryFragment,
                request.SortField,
                request.SortOrder,
                request.MaxCandidatesPerScan);

            await selectors.UpsertAsync(selector, cancellationToken);
            return Results.Ok(selector);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception);
        }
    }

    private static async Task<IResult> GetDefaultRouteAsync(
        Guid id,
        IProjectRepository projects,
        IRoutingConfigurationRepository routing,
        CancellationToken cancellationToken)
    {
        if (await projects.GetAsync(id, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var route = await routing.GetDefaultRouteAsync(id, cancellationToken);
        return route is null ? Results.NotFound() : Results.Ok(route);
    }

    private static async Task<IResult> PutDefaultRouteAsync(
        Guid id,
        RouteRequest request,
        IProjectRepository projects,
        IRoutingConfigurationRepository routing,
        CancellationToken cancellationToken)
    {
        if (await projects.GetAsync(id, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        try
        {
            var route = ExecutionRoute.Create(
                request.ModelIdentifier,
                request.ReasoningEffort);

            await routing.UpsertDefaultRouteAsync(id, route, cancellationToken);
            return Results.Ok(route);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception);
        }
    }

    private static async Task<IResult> GetRoutingRulesAsync(
        Guid id,
        IProjectRepository projects,
        IRoutingConfigurationRepository routing,
        CancellationToken cancellationToken)
    {
        if (await projects.GetAsync(id, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var rules = await routing.ListRulesAsync(id, cancellationToken);
        return Results.Ok(rules.Select(ToRuleResponse));
    }

    private static async Task<IResult> PutRoutingRulesAsync(
        Guid id,
        IReadOnlyList<RoutingRuleRequest> requests,
        IProjectRepository projects,
        IRoutingConfigurationRepository routing,
        CancellationToken cancellationToken)
    {
        if (await projects.GetAsync(id, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        try
        {
            var rules = requests
                .Select(request =>
                {
                    var route = ExecutionRoute.Create(
                        request.ModelIdentifier,
                        request.ReasoningEffort);

                    return request.Id is { } ruleId && ruleId != Guid.Empty
                        ? RoutingRule.Restore(
                            ruleId,
                            id,
                            request.Order,
                            request.Enabled,
                            request.RequiredLabels,
                            request.ExcludedLabels,
                            route)
                        : RoutingRule.Create(
                            id,
                            request.Order,
                            request.Enabled,
                            request.RequiredLabels,
                            request.ExcludedLabels,
                            route);
                })
                .ToArray();

            await routing.ReplaceRulesAsync(id, rules, cancellationToken);
            return Results.Ok(rules.Select(ToRuleResponse));
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception);
        }
    }

    private static object ToRuleResponse(RoutingRule rule)
    {
        return new
        {
            rule.Id,
            rule.Order,
            rule.Enabled,
            rule.RequiredLabels,
            rule.ExcludedLabels,
            rule.Route.ModelIdentifier,
            rule.Route.ReasoningEffort
        };
    }

    private static IResult ValidationProblem(ArgumentException exception)
    {
        var key = string.IsNullOrWhiteSpace(exception.ParamName)
            ? "configuration"
            : exception.ParamName;

        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [exception.Message]
        });
    }
}

public sealed record IssueSelectorRequest(
    string? QueryFragment,
    string SortField,
    string SortOrder,
    int MaxCandidatesPerScan);

public sealed record RouteRequest(
    string ModelIdentifier,
    string ReasoningEffort);

public sealed record RoutingRuleRequest(
    Guid? Id,
    int Order,
    bool Enabled,
    IReadOnlyList<string>? RequiredLabels,
    IReadOnlyList<string>? ExcludedLabels,
    string ModelIdentifier,
    string ReasoningEffort);
