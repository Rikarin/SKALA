using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rikarin.Skala.Server;

/// <summary>
///     <c>skala lsp</c> — stdio, and deliberately four capabilities wide.
/// </summary>
/// <remarks>
///     docs/plan/11 § "LSP":
///     <list type="table">
///         <item>
///             <term>textDocument/formatting</term><description>full-file edits</description>
///         </item>
///         <item>
///             <term>textDocument/rangeFormatting</term><description>full-file fit, edits filtered to the range</description>
///         </item>
///         <item>
///             <term>textDocument/diagnostic</term><description>the findings for the file (pull model)</description>
///         </item>
///         <item>
///             <term>textDocument/codeAction</term><description>the fixes, as quickfix actions</description>
///         </item>
///     </list>
///     <para>
///         ⚠ Range formatting is a <em>filter over a whole-file fit</em>, not a fit of the range. It is the
///         only way range formatting can be consistent with whole-file formatting (docs/plan/04 § "Emitting
///         minimal edits"): the column a construct is measured against depends on the indentation stack
///         above it, and a fit that starts half way down a file has to guess at that stack. Guessing makes
///         "format selection" and "format document" disagree, which is the bug every editor integration
///         eventually reports.
///     </para>
///     <para>
///         ⚠ Rider is not a consumer of this and does not need to be — it already implements this
///         `.editorconfig`, it is where the file came from, and building a plugin for it would be solving a
///         problem that does not exist (ADR-001). This is for VS Code, Neovim, Helix and Zed.
///     </para>
/// </remarks>
public sealed class LanguageServer {
    readonly FormatService _service = new();
    readonly ConcurrentDictionary<string, string> _open = new(StringComparer.Ordinal);
    readonly TextReader _input;
    readonly TextWriter _output;
    bool _shutdown;

    public LanguageServer(TextReader input, TextWriter output) {
        _input = input;
        _output = output;
    }

    public async Task RunAsync(CancellationToken cancellation) {
        while (!cancellation.IsCancellationRequested) {
            var message = await ReadMessageAsync().ConfigureAwait(false);
            if (message is null) {
                return;
            }

            var response = Dispatch(message);
            if (response is not null) {
                await WriteMessageAsync(response).ConfigureAwait(false);
            }

            if (_shutdown && message["method"]?.GetValue<string>() == "exit") {
                return;
            }
        }
    }

    JsonObject? Dispatch(JsonObject message) {
        var method = message["method"]?.GetValue<string>();
        var id = message["id"];

        switch (method) {
            case "initialize":
                return Result(id, Capabilities());

            case "initialized":
            case "$/setTrace":
                return null;

            case "shutdown":
                _shutdown = true;
                return Result(id, null);

            case "exit":
                _shutdown = true;
                return null;

            case "textDocument/didOpen":
                Track(message, "textDocument", "text");
                return null;

            case "textDocument/didChange":
                TrackChange(message);
                return null;

            case "textDocument/didClose":
                if (UriOf(message) is { } closing) {
                    _open.TryRemove(closing, out _);
                }

                return null;

            case "textDocument/formatting":
                return Result(id, Edits(message, null));

            case "textDocument/rangeFormatting":
                return Result(id, Edits(message, message["params"]?["range"] as JsonObject));

            case "textDocument/diagnostic":
                return Result(id, Diagnostics(message));

            case "textDocument/codeAction":
                return Result(id, CodeActions(message));

            default:
                // ⚠ An unknown request gets MethodNotFound and an unknown notification gets nothing.
                // A server that answers a notification wedges every client that counts responses.
                return id is null
                    ? null
                    : Error(id, -32601, $"skala lsp does not implement '{method}'");
        }
    }

    static JsonObject Capabilities() =>
        new() {
            ["capabilities"] = new JsonObject {
                // Full sync: the documents are small, the formatter reads whole files anyway, and
                // incremental sync is a second copy of the text model to keep correct.
                ["textDocumentSync"] = 1,
                ["documentFormattingProvider"] = true,
                ["documentRangeFormattingProvider"] = true,
                ["codeActionProvider"] = true,
                ["diagnosticProvider"] =
                    new JsonObject { ["interFileDependencies"] = false, ["workspaceDiagnostics"] = false }
            },
            ["serverInfo"] = new JsonObject {
                ["name"] = "skala",
                ["version"] = typeof(LanguageServer).Assembly.GetName().Version?.ToString() ?? "0.0.0"
            }
        };

    void Track(JsonObject message, string documentKey, string textKey) {
        var document = message["params"]?[documentKey] as JsonObject;
        if (document?["uri"]?.GetValue<string>() is { } uri && document[textKey]?.GetValue<string>() is { } text) {
            _open[uri] = text;
        }
    }

    void TrackChange(JsonObject message) {
        if (UriOf(message) is not { } uri) {
            return;
        }

        // Full sync, so the last change carries the whole document.
        if (message["params"]?["contentChanges"] is JsonArray changes
            && changes.Count > 0
            && changes[^1]?["text"]?.GetValue<string>() is { } text) {
            _open[uri] = text;
        }
    }

    static string? UriOf(JsonObject message) => message["params"]?["textDocument"]?["uri"]?.GetValue<string>();

    JsonArray Edits(JsonObject message, JsonObject? range) {
        if (UriOf(message) is not { } uri || PathOf(uri) is not { } path) {
            return new();
        }

        var text = _open.GetValueOrDefault(uri);
        var result = _service.Format(path, text, null, null);
        var source = SourceText.From(text ?? result.Original.ToString(), Encoding.UTF8);

        // ⚠ Filtered after a whole-file fit, never fitted from the range's first line.
        var edits = range is null
            ? result.Edits
            : EditEmitter.Restrict(result.Edits, SpanOf(source, range));

        var array = new JsonArray();
        foreach (var edit in edits) {
            array.Add(new JsonObject { ["range"] = RangeOf(source, edit.Span), ["newText"] = edit.NewText });
        }

        return array;
    }

    JsonObject Diagnostics(JsonObject message) {
        var items = new JsonArray();
        if (UriOf(message) is { } uri && PathOf(uri) is { } path) {
            var result = _service.Format(path, _open.GetValueOrDefault(uri), null, null);
            var source = result.Original;
            foreach (var diagnostic in result.Diagnostics) {
                items.Add(Render(source, diagnostic));
            }
        }

        return new() { ["kind"] = "full", ["items"] = items };
    }

    static JsonObject Render(SourceText source, SkalaDiagnostic diagnostic) {
        var line = Math.Clamp(diagnostic.Line - 1, 0, Math.Max(0, source.Lines.Count - 1));
        return new() {
            ["range"] = new JsonObject {
                ["start"] = new JsonObject { ["line"] = line, ["character"] = 0 },
                ["end"] = new JsonObject {
                    ["line"] = line, ["character"] = source.Lines.Count == 0 ? 0 : source.Lines[line].Span.Length
                }
            },
            ["severity"] = diagnostic.Severity switch {
                SkalaSeverity.Error => 1,
                SkalaSeverity.Warning => 2,
                SkalaSeverity.Info => 3,
                _ => 4
            },
            ["code"] = diagnostic.Id,
            ["source"] = "skala",
            ["message"] = diagnostic.Message
        };
    }

    JsonArray CodeActions(JsonObject message) {
        var actions = new JsonArray();
        if (UriOf(message) is not { } uri || PathOf(uri) is not { } path) {
            return actions;
        }

        var result = _service.Format(path, _open.GetValueOrDefault(uri), null, null);
        if (!result.Changed) {
            return actions;
        }

        var source = SourceText.From(_open.GetValueOrDefault(uri) ?? result.Original.ToString(), Encoding.UTF8);
        var edits = new JsonArray();
        foreach (var edit in result.Edits) {
            edits.Add(new JsonObject { ["range"] = RangeOf(source, edit.Span), ["newText"] = edit.NewText });
        }

        actions.Add(
            new JsonObject {
                ["title"] = "Format with Skala",
                ["kind"] = "source.formatDocument",
                ["edit"] = new JsonObject { ["changes"] = new JsonObject { [uri] = edits } }
            }
        );

        return actions;
    }

    static SourceSpan SpanOf(SourceText source, JsonObject range) {
        var start = OffsetOf(source, range["start"]);
        var end = OffsetOf(source, range["end"]);
        return SourceSpan.FromBounds(Math.Min(start, end), Math.Max(start, end));
    }

    static int OffsetOf(SourceText source, JsonNode? position) {
        if (position is null || source.Lines.Count == 0) {
            return 0;
        }

        var line = Math.Clamp(position["line"]?.GetValue<int>() ?? 0, 0, source.Lines.Count - 1);
        var character = Math.Max(0, position["character"]?.GetValue<int>() ?? 0);
        var span = source.Lines[line];
        return Math.Min(span.Start + character, span.End);
    }

    static JsonObject RangeOf(SourceText source, SourceSpan span) {
        var start = source.Lines.GetLinePosition(Math.Clamp(span.Start, 0, source.Length));
        var end = source.Lines.GetLinePosition(Math.Clamp(span.End, 0, source.Length));
        return new() {
            ["start"] = new JsonObject { ["line"] = start.Line, ["character"] = start.Character },
            ["end"] = new JsonObject { ["line"] = end.Line, ["character"] = end.Character }
        };
    }

    /// <summary>⚠ `file:` only. A URI Skala cannot read is answered with no edits, never with a guess.</summary>
    static string? PathOf(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile ? parsed.LocalPath : null;

    static JsonObject Result(JsonNode? id, JsonNode? value) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["result"] = value };

    static JsonObject Error(JsonNode? id, int code, string message) =>
        new() {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
        };

    async Task<JsonObject?> ReadMessageAsync() {
        var length = -1;
        while (true) {
            var header = await _input.ReadLineAsync().ConfigureAwait(false);
            if (header is null) {
                return null;
            }

            if (header.Length == 0) {
                break;
            }

            const string marker = "Content-Length:";
            if (header.StartsWith(marker, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(
                    header[marker.Length..].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed
                )) {
                length = parsed;
            }
        }

        if (length < 0) {
            return null;
        }

        var buffer = new char[length];
        var read = 0;
        while (read < length) {
            var got = await _input.ReadAsync(buffer.AsMemory(read)).ConfigureAwait(false);
            if (got == 0) {
                return null;
            }

            read += got;
        }

        return JsonNode.Parse(new string(buffer)) as JsonObject;
    }

    async Task WriteMessageAsync(JsonObject message) {
        var body = message.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var bytes = Encoding.UTF8.GetByteCount(body);
        await _output.WriteAsync(string.Create(CultureInfo.InvariantCulture, $"Content-Length: {bytes}\r\n\r\n{body}"))
            .ConfigureAwait(false);
        await _output.FlushAsync().ConfigureAwait(false);
    }
}
