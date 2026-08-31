using ModelContextProtocol.Server;
using System.Text;
using System.Text.Json;

namespace Rikarin.Skala.Mcp.Tests;

/// <summary>
///     The MCP surface, and the part of it that is a policy rather than a feature.
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
    ///     ⚠ docs/plan/10: "The MCP server exposes no tool that can disable a rule, edit
    ///     <c>.editorconfig</c>, or update a baseline. Those are human operations and their absence from
    ///     the tool list is the enforcement."
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
    ///     ⚠ doc 10 calls this "the single highest-leverage integration in this document": an agent can
    ///     format a file it has not written yet, which turns formatting from a correction into a step.
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
    ///     ⚠ Documentation comments too, because an agent's draft is mostly documentation comments.
    /// </summary>
    /// <remarks>
    ///     The MCP surface takes no flags, so whatever the formatter's default is, this is what an agent
    ///     gets — and the default changed (SK-DIV-0006). An agent that formats its draft and still has
    ///     to fix the doc comment by hand has been told the file is formatted when it is not.
    /// </remarks>
    [Fact]
    public void Format_FormatsDocumentationCommentsToo() {
        // ⚠ The subject has to be a comment the sub-formatter *changes*. This test used to pass
        // `///<summary>Docs.</summary>` and assert the marker gained its space — but the oracle was
        // measured on 2026-08-29 leaving a comment that needs no other change byte-identical, marker
        // included, and Skala now matches it. The old subject therefore asserted a behaviour neither
        // engine has, and would have gone on passing only while Skala was wrong.
        var formatted = McpServerInspection.FormatContent(
            Directory.GetCurrentDirectory(),
            "public sealed class Draft{///<summary>This is a fairly long documentation sentence that "
            + "will certainly need to be wrapped by the sub formatter because it exceeds the margin."
            + "</summary>\npublic int Value;}"
        );

        Assert.Contains("/// <summary>", formatted, StringComparison.Ordinal);
        Assert.Contains("///     This is a fairly long", formatted, StringComparison.Ordinal);
        Assert.Contains("///     because it exceeds the margin.", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ ADR-003 reaches the MCP surface too: an agent mid-refactor writes text that does not parse,
    ///     and the answer is to say so and change nothing, never to guess.
    /// </summary>
    [Fact]
    public void Format_LeavesTextThatDoesNotParseExactlyAsItIs() {
        var answer = McpServerInspection.FormatContent(
            Directory.GetCurrentDirectory(),
            "public sealed class Broken { void M( }"
        );

        Assert.Contains("does not parse", answer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The whole server, over a real transport, answering a real <c>tools/list</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ This exists because of a bug the tool-list unit tests could not see:
    ///     <c>McpServerOptions.ToolCollection</c> is <b>null</b> until something assigns one, so
    ///     <c>options.ToolCollection?.Add(tool)</c> compiled, ran, added nothing, and produced a server
    ///     that completed the handshake and answered <c>tools/list</c> with an empty array. Every unit
    ///     test passed, because they asked <c>SkalaTools</c> for the list rather than the server. A
    ///     transport-level test is the only kind that could have caught it.
    /// </remarks>
    [Fact]
    public async Task TheServer_AnswersToolsListOverATransport() {
        var input = new MemoryStream(
            Encoding.UTF8.GetBytes(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}
                {"jsonrpc":"2.0","method":"notifications/initialized"}
                {"jsonrpc":"2.0","id":2,"method":"tools/list"}

                """.ReplaceLineEndings("\n")
            )
        );

        var output = new MemoryStream();

        // ⚠ The reader must not see EOF before the server has answered. A plain MemoryStream is at
        // EOF the moment its bytes are consumed, and the SDK's loop then stops — which is also why
        // piping three lines into `skala mcp` and closing stdin prints nothing. Holding the input
        // open until the answer is on the way is what makes this test about the server rather than
        // about the race.
        await using (var transport = new StreamServerTransport(new HoldOpen(input, output), output, "skala")) {
            await using var server = McpServer.Create(transport, Directory.GetCurrentDirectory());
            await server.RunAsync(TestContext.Current.CancellationToken);
        }

        var names = new List<string>();
        foreach (var line in Encoding.UTF8.GetString(output.ToArray()).Split('\n')) {
            if (line.Length == 0) {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("result", out var result)
                || !result.TryGetProperty("tools", out var tools)) {
                continue;
            }

            names.AddRange(tools.EnumerateArray().Select(static tool => tool.GetProperty("name").GetString()!));
        }

        Assert.Equal(
            [
                "skala_check", "skala_config_explain", "skala_explain", "skala_fix", "skala_format", "skala_verify"
            ],
            names.Order(StringComparer.Ordinal)
        );
    }

    /// <summary>
    ///     A read-only stream that stays open until the server has written two JSON-RPC results.
    /// </summary>
    sealed class HoldOpen(Stream source, MemoryStream written) : Stream {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            var read = source.Read(buffer, offset, count);
            if (read > 0) {
                return read;
            }

            // Exhausted: wait for the two answers, then report EOF so the loop ends.
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline && Answers() < 2) {
                Thread.Sleep(10);
            }

            return 0;
        }

        int Answers() {
            lock (written) {
                var text = System.Text.Encoding.UTF8.GetString(written.GetBuffer(), 0, (int)written.Length);
                return text.Split('\n').Count(static line => line.Contains("\"result\"", StringComparison.Ordinal));
            }
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
