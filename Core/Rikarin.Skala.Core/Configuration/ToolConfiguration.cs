using System.Collections.Immutable;
using System.Text.Json;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>
/// <c>skala.jsonc</c> — where to look and what to do about what is found.
/// </summary>
/// <remarks>
/// ⚠ Nothing about style may live here (ADR-001). The workflow this protects is: change a setting
/// in Rider, re-export, done. A second place to say what code should look like is a second place to
/// keep in sync, and the first divergence reintroduces the problem the tool exists to remove.
/// </remarks>
public sealed class ToolConfiguration {
    public const string FileName = "skala.jsonc";

    ToolConfiguration(string path, ImmutableArray<SkalaDiagnostic> diagnostics) {
        Path = path;
        Diagnostics = diagnostics;
    }

    public string Path { get; }

    public ImmutableArray<SkalaDiagnostic> Diagnostics { get; }

    public static ToolConfiguration? Find(string directory) {
        var path = System.IO.Path.Combine(directory, FileName);
        return File.Exists(path) ? FromText(path, File.ReadAllText(path)) : null;
    }

    public static ToolConfiguration FromText(string path, string text) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        var options = new JsonDocumentOptions {
            CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true
        };

        try {
            using var document = JsonDocument.Parse(text, options);
            Walk(document.RootElement, path, diagnostics);
        } catch (JsonException exception) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    "SK9007",
                    SkalaSeverity.Error,
                    $"{FileName} is not valid JSON: {exception.Message}",
                    path
                )
            );
        }

        return new ToolConfiguration(path, diagnostics.ToImmutable());
    }

    static void Walk(JsonElement element, string path, ImmutableArray<SkalaDiagnostic>.Builder diagnostics) {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) {
                    if (OptionRegistry.TryResolve(property.Name, out var id)) {
                        diagnostics.Add(
                            new SkalaDiagnostic(
                                ConfigDiagnosticIds.StyleKeyInToolConfig,
                                SkalaSeverity.Error,
                                $"'{property.Name}' is a style option and cannot be set in {FileName}; move it to .editorconfig",
                                path,
                                0,
                                $"It is {OptionRegistry.Get(id).Key} (Tier {OptionRegistry.Get(id).Tier}). ADR-001: .editorconfig is the only style configuration language."
                            )
                        );
                    }

                    Walk(property.Value, path, diagnostics);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) {
                    Walk(item, path, diagnostics);
                }

                break;

            default:
                break;
        }
    }
}
