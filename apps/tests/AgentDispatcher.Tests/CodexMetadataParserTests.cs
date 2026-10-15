using AgentDispatcher.Domain.Codex;

namespace AgentDispatcher.Tests;

public sealed class CodexMetadataParserTests
{
    [Fact]
    public void ReturnsNotPresentWhenMetadataHeadingDoesNotExist()
    {
        var result = CodexMetadataParser.Parse("## 目的\n\n通常のIssue本文");

        Assert.Equal(CodexMetadataState.NotPresent, result.State);
        Assert.Null(result.Route);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ParsesMetadataAtBeginningOfIssueBody()
    {
        var result = CodexMetadataParser.Parse(
            """
            ## Codex Metadata

            Model: `gpt-5.6-codex`
            Reasoning level: `high`

            ## 目的

            実装する。
            """);

        Assert.Equal(CodexMetadataState.Valid, result.State);
        Assert.NotNull(result.Route);
        Assert.Equal("gpt-5.6-codex", result.Route.ModelIdentifier);
        Assert.Equal("high", result.Route.ReasoningEffort);
    }

    [Fact]
    public void RejectsMetadataThatIsNotAtBeginningOfIssueBody()
    {
        var result = CodexMetadataParser.Parse(
            """
            ## 目的

            実装する。

            ## Codex Metadata

            Model: gpt-5.6-codex
            Reasoning level: high
            """);

        Assert.Equal(CodexMetadataState.Invalid, result.State);
        Assert.Contains("先頭", result.Error);
    }

    [Theory]
    [InlineData("Model", "## Codex Metadata\n\nReasoning level: high")]
    [InlineData("Reasoning level", "## Codex Metadata\n\nModel: gpt-5.6-codex")]
    public void RejectsIncompleteMetadata(string expected, string body)
    {
        var result = CodexMetadataParser.Parse(body);

        Assert.Equal(CodexMetadataState.Invalid, result.State);
        Assert.Contains(expected, result.Error);
    }

    [Fact]
    public void RejectsUnsupportedReasoningLevel()
    {
        var result = CodexMetadataParser.Parse(
            "## Codex Metadata\n\nModel: gpt-5.6-codex\nReasoning level: impossible");

        Assert.Equal(CodexMetadataState.Invalid, result.State);
        Assert.Contains("minimal", result.Error);
    }
}
