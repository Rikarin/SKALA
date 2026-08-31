using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rikarin.Skala.Analysis;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Mcp;

/// <summary>
///     <c>skala mcp</c> — stdio, one process per repository, started by the agent host (ADR-014).
/// </summary>
/// <remarks>
///     ⚠ <b>The tool list is the enforcement.</b> docs/plan/10: "The MCP server exposes no tool that
///     can disable a rule, edit <c>.editorconfig</c>, or update a baseline. Those are human operations
///     and their absence from the tool list is the enforcement." An agent given a warning and the
///     ability to edit will reach for <c>#pragma warning disable</c>, not out of malice but because it
///     is a valid move toward the check passing; the answer is to make the honest path the easy one and
///     not to offer the other one at all.
///     <para>
///         ⚠ <c>skala_format</c> accepts <em>content</em> as well as paths, which doc 10 calls the single
///         highest-leverage integration in the plan: it lets an agent format a file it has not written yet
///         — draft, format, then write the formatted text — which turns formatting from a correction into a
///         step.
///     </para>
/// </remarks>
public static class McpServer {
    public static async Task RunAsync(string repositoryRoot, CancellationToken cancellation) {
        await using var transport = new StdioServerTransport("skala");
        await using var server = Create(transport, repositoryRoot);
        await server.RunAsync(cancellation).ConfigureAwait(false);
    }

    /// <summary>Builds the server over any transport, so a test can drive it without a process.</summary>
    public static ModelContextProtocol.Server.McpServer Create(ITransport transport, string repositoryRoot) {
        var options = new McpServerOptions {
            ServerInfo = new Implementation { Name = "skala", Version = SkalaVersion.Value },
            ServerInstructions = Instructions,
            Capabilities = new ServerCapabilities { Tools = new ToolsCapability() },

            // ⚠ Constructed, not left to the default. `McpServerOptions.ToolCollection` is **null**
            // until something assigns one, so `options.ToolCollection?.Add(tool)` compiles, runs,
            // adds nothing, and produces a server that answers `tools/list` with an empty array —
            // a working handshake and no tools, which looks like a client problem.
            ToolCollection = new McpServerPrimitiveCollection<McpServerTool>()
        };

        foreach (var tool in new SkalaTools(Path.GetFullPath(repositoryRoot)).Create()) {
            options.ToolCollection.Add(tool);
        }

        return ModelContextProtocol.Server.McpServer.Create(
            transport,
            options,
            NullLoggerFactory.Instance,
            serviceProvider: null!
        );
    }

    /// <summary>
    ///     What the agent is told about the server, once, at connect time.
    /// </summary>
    /// <remarks>
    ///     It is the <c>CLAUDE.md</c> contract from docs/plan/10, said by the tool rather than by the
    ///     repository — including the last line, which matters most: the escape hatch is
    ///     <em>
    ///         saying
    ///         so
    ///     </em>, not <em>doing something</em>. An agent with a sanctioned way to disagree does not
    ///     need an unsanctioned one.
    /// </remarks>
    const string Instructions = """
                                Skala formats and analyses C# against this repository's .editorconfig.

                                Before claiming work is finished, call skala_verify. Exit 0 or it is not finished.
                                  - Formatting: call skala_format. Never format by hand.
                                  - Fixable findings: call skala_fix with safeOnly, then verify again.
                                  - Findings needing a decision: fix the code. Do not add `#pragma warning disable`,
                                    do not lower a severity in .editorconfig, do not add to a baseline — all three are
                                    visible in review and all three are reverted.
                                  - If you believe a rule is wrong, call skala_explain and say so in your message.
                                    Do not act on that belief unilaterally.

                                skala_format accepts `content` as well as `paths`: draft the code, format it, then write
                                the formatted text. That is cheaper than writing and correcting.
                                """;
}

/// <summary>The six tools of docs/plan/10 § "The MCP server".</summary>
sealed class SkalaTools(string repositoryRoot) {
    /// <summary>
    ///     ⚠ Bounded, like the agent renderer. An unbounded dump eats the context window the agent needs to fix things
    ///     with.
    /// </summary>
    const int MaxCharacters = 16000;

    public IEnumerable<McpServerTool> Create() {
        yield return McpServerTool.Create(Verify, new McpServerToolCreateOptions { Name = "skala_verify" });
        yield return McpServerTool.Create(Format, new McpServerToolCreateOptions { Name = "skala_format" });
        yield return McpServerTool.Create(Check, new McpServerToolCreateOptions { Name = "skala_check" });
        yield return McpServerTool.Create(Fix, new McpServerToolCreateOptions { Name = "skala_fix" });
        yield return McpServerTool.Create(Explain, new McpServerToolCreateOptions { Name = "skala_explain" });
        yield return McpServerTool.Create(
            ConfigExplain,
            new McpServerToolCreateOptions { Name = "skala_config_explain" }
        );
    }

    [Description(
        "Is this acceptable? Runs format --check, arrange --check, and the analyzers and returns the "
        + "three-bucket report: formatting first, safe fixes second, decisions last. Exit 0 means "
        + "nothing to do. Uses an unambiguous workspace automatically, or loose mode with no project."
    )]
    string Verify(
        [Description("Files or directories. Empty means the whole repository.")] string[]? paths = null,
        [Description("Apply the safe fixes first, then report what is left.")] bool fix = false
    ) {
        var result = VerifyCommand.Run(
            new VerifyRequest { Paths = paths ?? [], RepositoryRoot = repositoryRoot, Fix = fix }
        );

        return Bound(result.Output.Length == 0 ? "OK  nothing to do.\n" : result.Output);
    }

    [Description(
        "Format C# to this repository's .editorconfig. Pass `content` to format text that is not on "
        + "disk yet — draft the code, format it, then write the formatted text. Pass `paths` to "
        + "format files in place."
    )]
    string Format(
        [Description("Files or directories to format in place.")] string[]? paths = null,
        [Description("C# source text to format and return. Not written anywhere.")] string? content = null,
        [Description("The path `content` will be saved to, so the right .editorconfig section applies.")]
        string? contentPath = null,
        [Description("Report what would change and write nothing.")] bool check = false
    ) {
        if (content is not null) {
            // ⚠ The content path matters and is not cosmetic: an .editorconfig may carry
            // [*.Designer.cs] or [Testing/**], so formatting a buffer as if it were at the
            // repository root can apply the wrong section. Told the intended path, the answer is
            // the one the file will get once it is written.
            var path = contentPath is { Length: > 0 }
                ? Path.GetFullPath(Path.Combine(repositoryRoot, contentPath))
                : Path.Combine(repositoryRoot, "_unsaved.cs");

            var options = ConfigurationCache.Options(EditorConfigChain.For(path), null);
            var result = CSharpFormatter.Format(path, SourceText.From(content, Encoding.UTF8), options);

            return result.Outcome switch {
                FormatOutcome.NotParseable =>
                    "The text does not parse and was left exactly as it is (SK9010). "
                    + string.Join(" ", result.Diagnostics.Select(static d => d.Message)),
                FormatOutcome.VerificationFailed =>
                    "SK9099: the formatter's output was not token-equivalent. Nothing was changed; this is a Skala bug.",
                _ => result.Formatted
            };
        }

        var command = FormatCommand.Run(
            new FormatRequest { Paths = paths ?? [], RepositoryRoot = repositoryRoot, Check = check, Quiet = false }
        );

        return Bound(command.Output);
    }

    [Description("Run the analyzers and return the findings, bounded and ordered by file.")]
    string Check(
        [Description("Files or directories. Empty means the whole repository.")] string[]? paths = null,
        [Description("The named gate from skala.jsonc. Default `local`.")] string gate = "local",
        [Description("Only these rule ids.")] string[]? rules = null,
        [Description("binlog | workspace | loose. Default loose, which needs no build.")] string load = "loose"
    ) {
        // An unrecognised mode falls back to the ladder's default rather than being an error: the
        // caller is a model, and a typo in an optional argument should not cost it a whole turn.
        var mode = LoadModes.TryParse(load, out var parsed) ? parsed : LoadMode.Loose;
        var (result, _) = CheckCommand.Run(
            new CheckRequest {
                Paths = paths ?? [],
                RepositoryRoot = repositoryRoot,
                Mode = mode,
                Gate = gate,
                Format = ReportFormat.Plain,
                Rules = rules ?? [],
                Output = string.Empty
            }
        );

        return Bound(result.Output.Length == 0 ? "No findings.\n" : result.Output);
    }

    [Description(
        "Apply the fixes the findings carry. With safeOnly the catalogue's safe fixes are applied; "
        + "without it you must name the rules, and that choice is visible in your transcript."
    )]
    string Fix(
        [Description("Files or directories. Empty means the whole repository.")] string[]? paths = null,
        [Description("Only apply fixes the catalogue marks safe.")] bool safeOnly = true,
        [Description("Rule ids whose unsafe fixes to apply. Required when safeOnly is false.")]
        string[]? rules = null,
        [Description("Say what would be applied and write nothing.")] bool dryRun = false
    ) {
        var result = FixCommand.Run(
            new FixRequest {
                Paths = paths ?? [],
                RepositoryRoot = repositoryRoot,
                SafeOnly = safeOnly,
                Include = rules ?? [],
                DryRun = dryRun
            }
        );

        return Bound(result.Output);
    }

    [Description(
        "Why a rule exists: its rationale, a bad and a good example, and the cases where it "
        + "deliberately does not fire. Read this before disagreeing with a finding."
    )]
    string Explain([Description("A rule id, e.g. SK1010.")] string ruleId) => Bound(ExplainCommand.Run(ruleId).Output);

    [Description(
        "The effective formatting options for a file, each with the .editorconfig file and line it "
        + "came from and its implementation tier."
    )]
    string ConfigExplain(
        [Description("The file whose effective options to print.")] string path,
        [Description("Only the options the configuration actually sets.")] bool configuredOnly = true
    ) =>
        Bound(
            ConfigCommands.Explain(
                Path.GetFullPath(Path.Combine(repositoryRoot, path)),
                repositoryRoot,
                configuredOnly,
                null
            ).Output
        );

    /// <summary>The content path of <c>skala_format</c>, without the tool plumbing.</summary>
    internal string FormatForTest(string content) => Format(content: content);

    static string Bound(string text) =>
        text.Length <= MaxCharacters
            ? text
            : text[..MaxCharacters]
            + "\n… truncated at "
            + MaxCharacters.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " characters. Narrow the paths, or call skala_check with `rules` set.\n";
}

/// <summary>The rule ids, exposed so an agent's `rules` argument can be checked before a call.</summary>
public static class McpRuleList {
    public static IReadOnlyList<string> Ids { get; } = [.. RuleCatalog.All.Select(static rule => rule.Id)];
}

/// <summary>
///     The tool list and the content path, reachable without a transport.
/// </summary>
/// <remarks>
///     ⚠ It exists so that the policy in <see cref="McpServer" />'s remarks — no tool that can disable a
///     rule, edit <c>.editorconfig</c> or update a baseline — is asserted by a test rather than
///     maintained by discipline. A stdio round trip would test the SDK; this tests the decision.
/// </remarks>
public static class McpServerInspection {
    public static IReadOnlyList<McpServerTool> Tools(string repositoryRoot) =>
        [.. new SkalaTools(Path.GetFullPath(repositoryRoot)).Create()];

    public static string FormatContent(string repositoryRoot, string content) =>
        new SkalaTools(Path.GetFullPath(repositoryRoot)).FormatForTest(content);
}
