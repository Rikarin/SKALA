using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     How one fixture is compiled: the three settings a fixture may ask for, and the defaults.
/// </summary>
/// <remarks>
///     ⚠ Before this existed, every fixture compiled at <c>LangVersion=Preview</c> with no preprocessor
///     symbols, and two classes of rule could not be fixtured at all
///     ([#317](https://github.com/Rikarin/SKALA/issues/317)):
///     <list type="bullet">
///         <item>
///             A rule whose territory is <em>below</em> the current language version has no positive
///             fixture that can fire, because the compiler's own diagnostic hosts the shape at
///             <c>Preview</c> and is silent at the version where a Skala rule would earn its place.
///         </item>
///         <item>
///             A rule that reasons about <c>#if</c> is measured in exactly one configuration. With no
///             symbols defined, <c>#if DEBUG</c> is disabled text in every fixture — and <c>SK2220</c>'s
///             corpus zero over a Debug binlog was the same non-event in production.
///         </item>
///     </list>
///     ⚠ And <c>allowUnsafe</c> was not passed at all, so a fixture containing <c>unsafe</c> was
///     <c>CS0227</c> and an entire class of C# — pointers, <c>fixed</c>, pointer arithmetic — was
///     unreachable from any fixture, positive or negative
///     ([#310](https://github.com/Rikarin/SKALA/issues/310)). It now defaults to <c>true</c>, which is
///     what <see cref="M:Rikarin.Skala.Analysis.Loading.LooseLoader.Load" /> already does in production;
///     a fixture that wants the safe context back asks for <c>AllowUnsafe = false</c>.
///     <para>
///         The defaults are today's behaviour for every setting that can change a fixture's meaning, so
///         nothing existing moves: a fixture is affected only by directives it carries itself.
///     </para>
/// </remarks>
/// <param name="LanguageVersion">The version the fixture is parsed at.</param>
/// <param name="PreprocessorSymbols">
///     The symbols defined while parsing — production's <c>--define</c>, per fixture.
/// </param>
/// <param name="AllowUnsafe">Whether an <c>unsafe</c> context is legal.</param>
public sealed record FixtureCompilation(
    LanguageVersion LanguageVersion,
    ImmutableArray<string> PreprocessorSymbols,
    bool AllowUnsafe) {
    /// <summary>What a fixture that says nothing is compiled as.</summary>
    public static FixtureCompilation Default { get; } = new(LanguageVersion.Preview, [], true);

    const string Prefix = "// fixture-option:";

    /// <summary>Reads the <c>// fixture-option:</c> directives out of a fixture's leading comment block.</summary>
    /// <remarks>
    ///     ⚠ An unrecognised key or an unparseable value <b>throws</b> rather than being ignored. A
    ///     directive that is silently dropped is the worst kind of instrument: the fixture reads as
    ///     though it is measuring C# 13 and measures <c>Preview</c>, and nothing anywhere says so.
    /// </remarks>
    public static FixtureCompilation From(string source) {
        var result = Default;
        foreach (var (key, value) in Directives(source, Prefix)) {
            result = key switch {
                "LangVersion" => result with { LanguageVersion = ParseVersion(value) },
                "DefineConstants" => result with { PreprocessorSymbols = ParseSymbols(value) },
                "AllowUnsafe" => result with { AllowUnsafe = ParseBoolean(value) },
                _ => throw new InvalidOperationException(
                    $"'{Prefix} {key}' is not a fixture option; the keys are LangVersion, DefineConstants and AllowUnsafe."
                )
            };
        }

        return result;
    }

    static LanguageVersion ParseVersion(string value) =>
        LanguageVersionFacts.TryParse(value, out var version)
            ? version
            : throw new InvalidOperationException($"'{value}' is not a C# language version.");

    static ImmutableArray<string> ParseSymbols(string value) =>
        [
            .. value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
                .Select(static symbol => symbol.Trim())
                .Where(static symbol => symbol.Length > 0)
        ];

    static bool ParseBoolean(string value) =>
        bool.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"'{value}' is not true or false."
            );

    /// <summary>
    ///     The <c>key = value</c> pairs a fixture's leading comment block carries under one prefix.
    /// </summary>
    /// <remarks>
    ///     ⚠ The scan stops at the first line that is not a comment, so a directive is a property of the
    ///     file's header and cannot be smuggled in halfway down — which keeps a fixture's compilation
    ///     readable from its first few lines and keeps a directive-looking comment inside a fixture's
    ///     <em>subject matter</em> from changing how it is compiled.
    /// </remarks>
    internal static IEnumerable<(string Key, string Value)> Directives(string source, string prefix) {
        foreach (var line in SourceText.From(source).Lines) {
            var trimmed = line.ToString().Trim();
            if (!trimmed.StartsWith("//", StringComparison.Ordinal)) {
                yield break;
            }

            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal)) {
                continue;
            }

            var assignment = trimmed[prefix.Length..];
            var separator = assignment.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0) {
                yield return (assignment[..separator].Trim(), assignment[(separator + 1)..].Trim());
            }
        }
    }

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"C# {LanguageVersionFacts.ToDisplayString(LanguageVersion)}, define [{string.Join(";", PreprocessorSymbols)}], unsafe {AllowUnsafe}"
        );
}
