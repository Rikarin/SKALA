using System.Collections.Immutable;
using System.Text.Json;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>
/// What a repository wants done about canonical drift. Drift is an error by default: a repository
/// whose managed block has been edited is a repository whose IDE and gate have started to disagree,
/// which is the failure the canonical exists to prevent.
/// </summary>
public sealed record CanonicalPolicy(SkalaSeverity Drift) {
    public static CanonicalPolicy Default { get; } = new(SkalaSeverity.Error);
}

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

    ToolConfiguration(string path, CanonicalPolicy canonical, ImmutableArray<SkalaDiagnostic> diagnostics) {
        Path = path;
        Canonical = canonical;
        Diagnostics = diagnostics;
    }

    public string Path { get; }

    /// <summary>What this repository wants done about canonical drift.</summary>
    public CanonicalPolicy Canonical { get; }

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
        var canonical = CanonicalPolicy.Default;

        try {
            using var document = JsonDocument.Parse(text, options);
            Walk(document.RootElement, path, diagnostics);
            canonical = ReadCanonical(document.RootElement, path, diagnostics);
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

        return new ToolConfiguration(path, canonical, diagnostics.ToImmutable());
    }

    /// <summary>
    /// <c>"canonical": { "drift": "error" }</c> — and nothing else.
    /// </summary>
    /// <remarks>
    /// ⚠ There is deliberately no <c>version</c> key. The canonical version a repository is on is
    /// recorded in the <c>.editorconfig</c> marker, beside the bytes it describes, so that the
    /// question "is this file what it claims to be" is answerable from the file alone. A version
    /// recorded in a second file is a version that drifts, which is this whole feature's disease.
    /// </remarks>
    static CanonicalPolicy ReadCanonical(
        JsonElement root,
        string path,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("canonical", out var canonical)
            || canonical.ValueKind != JsonValueKind.Object) {
            return CanonicalPolicy.Default;
        }

        if (canonical.TryGetProperty("version", out _)) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.CanonicalVersionInToolConfig,
                    SkalaSeverity.Error,
                    $"'canonical.version' cannot be set in {FileName}",
                    path,
                    0,
                    "The version lives in the `# skala:canonical begin` marker in .editorconfig, beside the bytes it names. Recording it twice is how it comes to disagree with itself."
                )
            );
        }

        if (!canonical.TryGetProperty("drift", out var drift) || drift.ValueKind != JsonValueKind.String) {
            return CanonicalPolicy.Default;
        }

        return drift.GetString() switch {
            "error" => new CanonicalPolicy(SkalaSeverity.Error),
            "warning" => new CanonicalPolicy(SkalaSeverity.Warning),
            "off" => new CanonicalPolicy(SkalaSeverity.Info),
            var other => Unknown(other, path, diagnostics)
        };
    }

    static CanonicalPolicy Unknown(string? value, string path, ImmutableArray<SkalaDiagnostic>.Builder diagnostics) {
        diagnostics.Add(
            new SkalaDiagnostic(
                ConfigDiagnosticIds.CanonicalDrift,
                SkalaSeverity.Warning,
                $"'canonical.drift' is '{value}'; expected 'error', 'warning' or 'off'. Using 'error'.",
                path
            )
        );

        return CanonicalPolicy.Default;
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
