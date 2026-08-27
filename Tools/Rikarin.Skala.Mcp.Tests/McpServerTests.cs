using ModelContextProtocol.Server;
using Rikarin.Skala.Mcp;

namespace Rikarin.Skala.Mcp.Tests;

/// <summary>
/// The MCP surface, and the part of it that is a policy rather than a feature.
/// </summary>
public sealed class McpServerTests {
    static IReadOnlyList<McpServerTool> Tools() => McpServerInspection.Tools(Directory.GetCurrentDirectory());

    [Fact]
    public void TheToolList_IsTheSixOfDocumentTen() {
        var names = Tools().Select(static tool => tool.ProtocolTool.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(
            [
                "skala_check", "skala_config_explain", "skala_explain", "skala_fix", "skala_format", "skala_verify"
            ],
            names
        );
    }

    /// <summary>
    /// ⚠ docs/plan/10: "The MCP server exposes no tool that can disable a rule, edit
    /// <c>.editorconfig</c>, or update a baseline. Those are human operations and their absence from
    /// the tool list is the enforcement."
    /// </summary>
    [Fact]
    public void NoTool_CanDisableARuleOrEditTheConfiguration() {
        foreach (var tool in Tools()) {
            var name = tool.ProtocolTool.Name;
            Assert.DoesNotContain("disable", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("suppress", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("baseline", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("severity", name, StringComparison.OrdinalIgnoreCase);

            // `skala_config_explain` reads; nothing writes configuration.
            Assert.False(
                name.Contains("config", StringComparison.OrdinalIgnoreCase)
                && !name.EndsWith("_explain", StringComparison.Ordinal),
                $"'{name}' names configuration and is not a read."
            );
        }
    }

    /// <summary>
    /// ⚠ doc 10 calls this "the single highest-leverage integration in this document": an agent can
    /// format a file it has not written yet, which turns formatting from a correction into a step.
    /// </summary>
    [Fact]
    public void Format_AcceptsContentAndReturnsFormattedText() {
        var formatted = McpServerInspection.FormatContent(
            Directory.GetCurrentDirectory(),
            "public sealed class Draft{public int    Value;}"
        );

        Assert.Contains("public sealed class Draft {", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("int    Value", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ ADR-003 reaches the MCP surface too: an agent mid-refactor writes text that does not parse,
    /// and the answer is to say so and change nothing, never to guess.
    /// </summary>
    [Fact]
    public void Format_LeavesTextThatDoesNotParseExactlyAsItIs() {
        var answer = McpServerInspection.FormatContent(
            Directory.GetCurrentDirectory(),
            "public sealed class Broken { void M( }"
        );

        Assert.Contains("does not parse", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryTool_CarriesADescriptionTheModelCanAct() {
        foreach (var tool in Tools()) {
            Assert.False(
                string.IsNullOrWhiteSpace(tool.ProtocolTool.Description),
                $"{tool.ProtocolTool.Name} has no description; a tool a model cannot tell apart is a tool it calls wrongly."
            );
        }
    }
}
