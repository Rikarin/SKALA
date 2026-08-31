using Rikarin.Skala.Formatting.CSharp;
using System.Text;
using System.Text.Json.Nodes;

namespace Rikarin.Skala.Server.Tests;

/// <summary>A scratch repository: a `.git` directory, an `.editorconfig`, and files.</summary>
public sealed class Scratch : IDisposable {
    public Scratch(
        string editorConfig =
        "root = true\n[*.cs]\nindent_size = 4\nresharper_csharp_new_line_before_open_brace = none\ncsharp_new_line_before_open_brace = none\n"
    ) {
        Root = Directory.CreateTempSubdirectory("skala-server-").FullName;
        Directory.CreateDirectory(Path.Combine(Root, ".git"));
        File.WriteAllText(Path.Combine(Root, ".editorconfig"), editorConfig);
    }

    public string Root { get; }

    public string Write(string name, string content) {
        var path = Path.Combine(Root, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() {
        try {
            Directory.Delete(Root, true);
        } catch (IOException) {
            // A handle the runtime still holds is not worth failing a test over.
        }
    }
}

public sealed class LanguageServerTests {
    static async Task<List<JsonObject>> Converse(params JsonObject[] requests) {
        var input = new StringBuilder();
        foreach (var request in requests) {
            var body = request.ToJsonString();
            input.Append("Content-Length: ")
                .Append(Encoding.UTF8.GetByteCount(body).ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append("\r\n\r\n")
                .Append(body);
        }

        var output = new StringWriter();
        var server = new LanguageServer(new StringReader(input.ToString()), output);
        await server.RunAsync(CancellationToken.None);

        var responses = new List<JsonObject>();
        var text = output.ToString();
        var cursor = 0;
        while (true) {
            var header = text.IndexOf("Content-Length: ", cursor, StringComparison.Ordinal);
            if (header < 0) {
                break;
            }

            var end = text.IndexOf("\r\n\r\n", header, StringComparison.Ordinal);
            var length = int.Parse(
                text[(header + "Content-Length: ".Length)..end],
                System.Globalization.CultureInfo.InvariantCulture
            );

            // ⚠ The header counts bytes and the buffer holds chars; the fixtures are ASCII, so this
            // is exact here and would not be for a document with an emoji in it.
            var start = end + 4;
            responses.Add((JsonObject)JsonNode.Parse(text[start..(start + length)])!);
            cursor = start + length;
        }

        return responses;
    }

    static JsonObject Request(int id, string method, JsonObject? parameters = null) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters ?? new JsonObject() };

    [Fact]
    public async Task Initialize_AdvertisesExactlyTheFourCapabilities() {
        var responses = await Converse(Request(1, "initialize"));
        var capabilities = responses[0]["result"]!["capabilities"]!;

        Assert.True(capabilities["documentFormattingProvider"]!.GetValue<bool>());
        Assert.True(capabilities["documentRangeFormattingProvider"]!.GetValue<bool>());
        Assert.True(capabilities["codeActionProvider"]!.GetValue<bool>());
        Assert.NotNull(capabilities["diagnosticProvider"]);
    }

    [Fact]
    public async Task Formatting_ReturnsEditsForAnOpenDocument() {
        using var scratch = new Scratch();
        var path = scratch.Write("D.cs", "class C{void M(){M();}}\n");
        var uri = new Uri(path).AbsoluteUri;

        var responses = await Converse(
            Request(1, "initialize"),
            new JsonObject {
                ["jsonrpc"] = "2.0",
                ["method"] = "textDocument/didOpen",
                ["params"] = new JsonObject {
                    ["textDocument"] = new JsonObject { ["uri"] = uri, ["text"] = "class C{void M(){M();}}\n" }
                }
            },
            Request(
                2,
                "textDocument/formatting",
                new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri } }
            )
        );

        var edits = (JsonArray)responses[1]["result"]!;
        Assert.NotEmpty(edits);
    }

    [Fact]
    public async Task RangeFormatting_IsAFilterOverTheWholeFileFit() {
        // ⚠ Not a fit of the range. docs/plan/04 § "Emitting minimal edits": the edits are filtered
        // *after* a whole-file fit, which is the only way "format selection" and "format document"
        // can agree. The observable consequence is that a range's edits are a subset of the file's.
        using var scratch = new Scratch();
        var source = "class C{\nvoid A(){A();}\nvoid B(){B();}\n}\n";
        var path = scratch.Write("E.cs", source);
        var uri = new Uri(path).AbsoluteUri;

        var open = new JsonObject {
            ["jsonrpc"] = "2.0",
            ["method"] = "textDocument/didOpen",
            ["params"] = new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri, ["text"] = source } }
        };

        var whole = await Converse(
            open,
            Request(
                1,
                "textDocument/formatting",
                new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri } }
            )
        );

        var ranged = await Converse(
            open,
            Request(
                1,
                "textDocument/rangeFormatting",
                new JsonObject {
                    ["textDocument"] = new JsonObject { ["uri"] = uri },
                    ["range"] = new JsonObject {
                        ["start"] = new JsonObject { ["line"] = 1, ["character"] = 0 },
                        ["end"] = new JsonObject { ["line"] = 1, ["character"] = 14 }
                    }
                }
            )
        );

        var all = (JsonArray)whole[0]["result"]!;
        var some = (JsonArray)ranged[0]["result"]!;
        Assert.True(some.Count < all.Count, "the range should be a strict subset of the file's edits");
        Assert.NotEmpty(some);
    }

    [Fact]
    public async Task Diagnostic_ReportsAFileThatDoesNotParse() {
        using var scratch = new Scratch();
        var source = "class C { void M( }\n";
        var path = scratch.Write("F.cs", source);
        var uri = new Uri(path).AbsoluteUri;

        var responses = await Converse(
            new JsonObject {
                ["jsonrpc"] = "2.0",
                ["method"] = "textDocument/didOpen",
                ["params"] = new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri, ["text"] = source } }
            },
            Request(
                1,
                "textDocument/diagnostic",
                new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri } }
            )
        );

        var items = (JsonArray)responses[0]["result"]!["items"]!;
        Assert.Contains(items, item => item!["code"]!.GetValue<string>() == FormatDiagnosticIds.NotParseable);
    }

    [Fact]
    public async Task CodeAction_OffersFormattingWhenThereIsSomethingToDo() {
        using var scratch = new Scratch();
        var source = "class C{void M(){M();}}\n";
        var path = scratch.Write("G.cs", source);
        var uri = new Uri(path).AbsoluteUri;

        var responses = await Converse(
            new JsonObject {
                ["jsonrpc"] = "2.0",
                ["method"] = "textDocument/didOpen",
                ["params"] = new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri, ["text"] = source } }
            },
            Request(
                1,
                "textDocument/codeAction",
                new JsonObject {
                    ["textDocument"] = new JsonObject { ["uri"] = uri },
                    ["range"] = new JsonObject {
                        ["start"] = new JsonObject { ["line"] = 0, ["character"] = 0 },
                        ["end"] = new JsonObject { ["line"] = 0, ["character"] = 0 }
                    }
                }
            )
        );

        var actions = (JsonArray)responses[0]["result"]!;
        Assert.Single(actions);
        Assert.Equal("Format with Skala", actions[0]!["title"]!.GetValue<string>());
    }

    [Fact]
    public async Task AnUnknownRequest_IsMethodNotFound_AndAnUnknownNotificationIsSilent() {
        // ⚠ A server that answers a notification wedges every client that counts responses.
        var responses = await Converse(
            new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "textDocument/didSave" },
            Request(7, "textDocument/inlayHint")
        );

        Assert.Single(responses);
        Assert.Equal(7, responses[0]["id"]!.GetValue<int>());
        Assert.Equal(-32601, responses[0]["error"]!["code"]!.GetValue<int>());
    }
}

public sealed class GitHookTests {
    [Fact]
    public void Install_WritesTheHook_AndSaysSoFirst() {
        using var scratch = new Scratch();

        var dry = GitHooks.Install(scratch.Root, false);
        Assert.False(dry.Written);
        Assert.False(File.Exists(dry.Path));

        var written = GitHooks.Install(scratch.Root, true);
        Assert.True(written.Written);
        Assert.Contains("skala format --staged", File.ReadAllText(written.Path), StringComparison.Ordinal);
    }

    [Fact]
    public void Install_RefusesToClobberSomebodyElsesHook() {
        using var scratch = new Scratch();
        var hooks = Path.Combine(scratch.Root, ".git", "hooks");
        Directory.CreateDirectory(hooks);
        File.WriteAllText(Path.Combine(hooks, "pre-commit"), "#!/bin/sh\necho mine\n");

        var result = GitHooks.Install(scratch.Root, true);
        Assert.False(result.Written);
        Assert.Equal("#!/bin/sh\necho mine\n", File.ReadAllText(result.Path));
    }

    [Fact]
    public void Install_DefersToAHookManager() {
        // ⚠ Detecting rather than clobbering. A tool that overwrites somebody's husky configuration
        // to install itself has broken every other check that repository ran.
        using var scratch = new Scratch();
        File.WriteAllText(Path.Combine(scratch.Root, ".pre-commit-config.yaml"), "repos: []\n");

        var result = GitHooks.Install(scratch.Root, true);
        Assert.False(result.Written);
        Assert.Contains("pre-commit", result.Outcome, StringComparison.Ordinal);
    }

    [Fact]
    public void Install_DefersToCoreHooksPath() {
        using var scratch = new Scratch();
        File.WriteAllText(
            Path.Combine(scratch.Root, ".git", "config"),
            "[core]\n\thooksPath = .githooks\n"
        );

        var result = GitHooks.Install(scratch.Root, true);
        Assert.False(result.Written);
        Assert.Contains("core.hooksPath", result.Outcome, StringComparison.Ordinal);
    }
}
