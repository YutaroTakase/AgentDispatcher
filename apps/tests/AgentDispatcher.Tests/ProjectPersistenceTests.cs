using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Infrastructure.Persistence;

namespace AgentDispatcher.Tests;

public sealed class ProjectPersistenceTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "AgentDispatcher.Tests",
        Guid.NewGuid().ToString("N"));

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    [Fact]
    public void InvalidProjectIsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => Project.Create(
            "test",
            "invalid",
            true,
            "main",
            5,
            1,
            30,
            7));

        Assert.Equal("repository", exception.ParamName);
    }

    [Fact]
    public async Task ProjectCrudPersistsAcrossRepositoryInstances()
    {
        var databasePath = Path.Combine(_directory, "agent-dispatcher.db");
        var database = SqliteDatabase.FromFile(databasePath);

        await database.InitializeAsync();
        await database.InitializeAsync();

        var repository = new SqliteProjectRepository(database);
        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            2,
            30,
            7);

        await repository.AddAsync(project);

        var restored = await new SqliteProjectRepository(SqliteDatabase.FromFile(databasePath))
            .GetAsync(project.Id);

        Assert.NotNull(restored);
        Assert.Equal(project.Repository, restored.Repository);

        var updated = restored.Update(
            "更新後",
            "owner/repository",
            false,
            "develop",
            10,
            3,
            60,
            14);

        Assert.True(await repository.UpdateAsync(updated));

        var list = await repository.ListAsync();
        var saved = Assert.Single(list);
        Assert.Equal("更新後", saved.DisplayName);
        Assert.False(saved.Enabled);
        Assert.Equal("develop", saved.DefaultBranch);

        Assert.True(await repository.DeleteAsync(project.Id));
        Assert.Null(await repository.GetAsync(project.Id));
    }
}
