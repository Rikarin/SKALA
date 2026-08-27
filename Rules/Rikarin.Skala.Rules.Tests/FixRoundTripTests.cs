using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
/// Applies every fix and then asks the compiler and the rule what they think of the result.
/// </summary>
/// <remarks>
/// ⚠ <see cref="RuleFixtureTests.EveryFix_ProducesTextThatStillParses"/> checks that the edited text
/// <em>parses</em>, which is a real check and is not the one that matters. A fix that turns
/// <c>x != null</c> into a pattern inside an expression tree parses perfectly and is CS8122; a fix
/// that lifts a declaration into a scope where the name is taken parses perfectly and is CS0136.
/// Those are binding errors, and finding them needs the semantic model rather than the parser.
/// <para>
/// ⚠ The second assertion is the one that catches a fix which is <em>correct but not a fix</em>: if
/// the rule still fires on its own output, the edit did not address the finding, and
/// <c>skala fix</c> would either loop or leave the report unchanged after doing work. docs/plan/10:
/// "A fixing tool that can break the build is a tool an agent will use to break the build" — and one
/// that changes a file without changing the report is the quieter version of the same problem.
/// </para>
/// <para>
/// ⚠ The analyzers are discovered by reflection rather than listed. A hand-kept list is a list that
/// silently omits the rule someone forgot to add, and the omission looks exactly like a passing
/// test.
/// </para>
/// </remarks>
public sealed class FixRoundTripTests {
    static ImmutableArray<DiagnosticAnalyzer> Analyzers { get; } = Discover();

    static ImmutableArray<DiagnosticAnalyzer> Discover() {
        var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        foreach (var type in typeof(SkalaRule).Assembly.GetTypes()) {
            if (type is { IsAbstract: false, IsPublic: true }
                && typeof(DiagnosticAnalyzer).IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) is not null) {
                builder.Add((DiagnosticAnalyzer)Activator.CreateInstance(type)!);
            }
        }

        return builder.ToImmutable();
    }

    public static TheoryData<RuleFixture> Fixable {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()) {
                if (fixture.ShouldFire && RuleCatalog.Find(fixture.RuleId) is { HasFix: true }) {
                    data.Add(fixture);
                }
            }

            return data;
        }
    }

    [Fact]
    public void EveryShippedAnalyzer_IsDiscovered() {
        // Anti-vacuity: a reflection sweep that finds nothing passes every theory below.
        Assert.True(Analyzers.Length > 15, $"Only {Analyzers.Length} analyzer(s) were discovered.");
    }

    [Theory]
    [MemberData(nameof(Fixable))]
    public void ApplyingAFix_LeavesTheCodeCompilingAndTheRuleSilent(RuleFixture fixture) {
        var cancellation = TestContext.Current.CancellationToken;
        var source = File.ReadAllText(fixture.Path);
        var before = RuleFixtures.Compile(source, fixture.Path);

        var findings = RuleFixtures.Analyze(before, Analyzers, cancellation)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        Assert.True(findings.Length > 0, $"{fixture}: nothing to round-trip; the rule did not fire.");

        var edits = findings.SelectMany(ReadEdits).OrderByDescending(static edit => edit.Start).ToList();
        Assert.True(edits.Count > 0, $"{fixture}: {fixture.RuleId} carries no fix, but the catalogue says it has one.");

        var text = source;
        var applied = 0;
        var consumed = int.MaxValue;
        foreach (var (start, length, replacement) in edits) {
            // ⚠ Two findings in one fixture can overlap. `skala fix` resolves that by applying a
            // pass and re-running; here the later edit is skipped, which is the same outcome for
            // the question being asked.
            if (start + length > consumed) {
                continue;
            }

            text = text[..start] + replacement + text[(start + length)..];
            consumed = start;
            applied++;
        }

        Assert.True(applied > 0, $"{fixture}: every edit overlapped another and none was applied.");

        var after = RuleFixtures.Compile(text, fixture.Path);

        // ⚠ Counted per diagnostic id rather than per (line, id): a fix that deletes a brace moves
        // every error below it, and a line-keyed comparison reports the move as a regression.
        var was = ErrorsById(before, cancellation);
        var now = ErrorsById(after, cancellation);
        var regressions = now
            .Where(entry => entry.Value > (was.TryGetValue(entry.Key, out var count) ? count : 0))
            .Select(
                entry => entry.Key
                    + " ×"
                    + (entry.Value - (was.TryGetValue(entry.Key, out var count) ? count : 0))
                        .ToString(CultureInfo.InvariantCulture)
            )
            .ToArray();

        Assert.True(
            regressions.Length == 0,
            $"{fixture}: applying {fixture.RuleId}'s fix introduced binding errors the parser would not "
            + $"have seen: {string.Join(", ", regressions)}\n---\n{text}"
        );

        var remaining = RuleFixtures.Analyze(after, Analyzers, cancellation)
            .Count(diagnostic => diagnostic.Id == fixture.RuleId);

        Assert.True(
            remaining < findings.Length,
            $"{fixture}: {fixture.RuleId} still fires {remaining} time(s) on its own fix's output, having "
            + $"fired {findings.Length} time(s) before it. The edit did not address the finding.\n---\n{text}"
        );
    }

    static Dictionary<string, int> ErrorsById(Compilation compilation, CancellationToken cancellation) {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var diagnostic in compilation.GetDiagnostics(cancellation)) {
            if (diagnostic.Severity != DiagnosticSeverity.Error) {
                continue;
            }

            result[diagnostic.Id] = result.TryGetValue(diagnostic.Id, out var count) ? count + 1 : 1;
        }

        return result;
    }

    static IEnumerable<(int Start, int Length, string Text)> ReadEdits(Diagnostic diagnostic) {
        if (!diagnostic.Properties.TryGetValue(FixEdits.CountKey, out var countText)
            || !int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)) {
            yield break;
        }

        for (var i = 0; i < count; i++) {
            yield return (
                int.Parse(diagnostic.Properties[FixEdits.StartKey(i)]!, CultureInfo.InvariantCulture),
                int.Parse(diagnostic.Properties[FixEdits.LengthKey(i)]!, CultureInfo.InvariantCulture),
                diagnostic.Properties[FixEdits.TextKey(i)] ?? string.Empty
            );
        }
    }
}
