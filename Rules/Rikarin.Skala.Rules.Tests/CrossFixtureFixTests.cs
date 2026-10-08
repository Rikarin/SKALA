using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Every safe fix of every rule, applied to every fixture in the repository, and recompiled.
/// </summary>
/// <remarks>
///     ⚠ <see cref="FixRoundTripTests" /> round-trips a rule's fix on the rule's <b>own</b> positive
///     fixtures and nowhere else, so a finding a rule makes on <em>another</em> rule's fixture is
///     reported, carries an edit marked safe, and is never applied by anything. Both of
///     [#402](https://github.com/Rikarin/SKALA/issues/402) and [#403](https://github.com/Rikarin/SKALA/issues/403)
///     lived there: <c>SK1053</c> turned <c>Local local = new();</c> into <c>_ = new();</c> (CS8754) on
///     an <c>SK7081</c> fixture, and <c>SK0241</c> deleted the <c>abstract</c> that re-abstracts a base
///     interface's default on an <c>SK0280</c> fixture (CS0501). Each rule's own fixtures held neither
///     shape, because each author wrote the shapes they had thought of.
///     <para>
///         The fixture corpus is the largest body of deliberately varied C# in the repository, and
///         nearly every file in it is somebody else's edge case. Applying one rule's edits at a time —
///         grouped per rule, as <c>skala fix</c> applies a pass — keeps a regression attributable to
///         the rule that caused it; two rules' edits that are each sound and conflict together are
///         <c>skala fix</c>'s re-analysis problem, not a fix defect.
///     </para>
///     <para>
///         ⚠ It is one <see cref="FactAttribute" /> over a parallel loop rather than a theory with a row
///         per fixture: xunit runs a theory's rows serially, and this is one compilation per
///         (fixture, rule) pair on top of an analysis per fixture.
///     </para>
/// </remarks>
public sealed class CrossFixtureFixTests {
    const string PlantedPath = "planted.cs";

    [Fact]
    public void EverySafeFix_OnEveryFixture_IntroducesNoCompilerError() {
        var cancellation = TestContext.Current.CancellationToken;
        var fixtures = RuleFixtures.All();
        var failures = new ConcurrentBag<string>();
        var applied = 0;

        Parallel.ForEach(
            fixtures,
            new() { CancellationToken = cancellation },
            fixture => {
                foreach (var failure in Regressions(
                             File.ReadAllText(fixture.Path),
                             fixture.Path,
                             ref applied,
                             cancellation
                         )) {
                    failures.Add(CrossRuleBaseline.Key(fixture.Path) + ": " + failure);
                }
            }
        );

        // Anti-vacuity: a sweep that applied nothing passes for the reason a disabled check passes.
        // ⚠ 864 once #412's audit flipped 59 fixes to unsafe; it was over 1000 before.
        Assert.True(
            applied > 800,
            $"Only {applied} (fixture, rule) fix(es) were applied across {fixtures.Count} fixtures."
        );
        Assert.True(
            failures.IsEmpty,
            $"{failures.Count} safe fix(es) out of {applied} applied break a fixture that compiled before:\n  "
            + string.Join("\n  ", failures.Order(StringComparer.Ordinal))
        );
    }

    /// <summary>
    ///     ⚠ The instrument check: an edit that breaks the build must come back as a regression, and the
    ///     one that is sound must not.
    /// </summary>
    [Fact]
    public void TheComparison_SeesABreakingEdit_AndNotASoundOne() {
        const string source = "class C { void M() { var x = 1; } }";
        var cancellation = TestContext.Current.CancellationToken;
        var before = RuleFixtures.Compile(source, PlantedPath);

        Assert.Equal(
            ["CS8754 ×1"],
            NewErrors(
                before,
                source.Replace("var x = 1", "_ = new()", StringComparison.Ordinal),
                PlantedPath,
                cancellation
            )
        );
        Assert.Empty(
            NewErrors(
                before,
                source.Replace("var x = 1", "_ = 1", StringComparison.Ordinal),
                PlantedPath,
                cancellation
            )
        );
    }

    /// <summary>
    ///     ⚠ #412: <b>compiling is not meaning the same thing.</b> Every safe fix applied to every
    ///     fixture that can be executed — one declaring a <c>Probe</c> type with a public static
    ///     parameterless <c>Run</c> — and both versions run.
    /// </summary>
    /// <remarks>
    ///     <c>SK4022</c>'s <c>readonly</c> compiled on a struct that called a mutating method on its
    ///     captured struct, and redirected the call to a defensive copy: the sweep above passed it, and
    ///     only running the program shows the difference. Execution is opt-in, because a fixture is a
    ///     shape and not a program, and calling arbitrary members of every fixture would run their
    ///     side effects; a fixture that wants its fix's behaviour pinned adds a <c>Probe</c>.
    /// </remarks>
    [Fact]
    public void EverySafeFix_OnEveryExecutableFixture_PreservesTheResult() {
        var cancellation = TestContext.Current.CancellationToken;
        var failures = new ConcurrentBag<string>();
        var compared = 0;

        Parallel.ForEach(
            RuleFixtures.All()
                .Where(static fixture => File.ReadAllText(fixture.Path)
                        .Contains("class Probe", StringComparison.Ordinal)
                ),
            new() { CancellationToken = cancellation },
            fixture => {
                var source = File.ReadAllText(fixture.Path);
                var before = RuleFixtures.Compile(source, fixture.Path);
                if (Probe(before, cancellation) is not { } expected) {
                    return;
                }

                foreach (var (id, text) in FixedTexts(source, before, cancellation)) {
                    var after = RuleFixtures.Compile(text, fixture.Path);
                    if (after.GetDiagnostics(cancellation).Any(static d => d.Severity == DiagnosticSeverity.Error)) {
                        continue; // The compile sweep's finding, not this one's.
                    }

                    Interlocked.Increment(ref compared);
                    var actual = Probe(after, cancellation);
                    if (actual != expected) {
                        failures.Add(
                            $"{CrossRuleBaseline.Key(fixture.Path)}: {id} changes Probe.Run() "
                            + $"from {expected} to {actual ?? "<no result>"}"
                        );
                    }
                }
            }
        );

        // Anti-vacuity: SK4022's own executable positives alone are five fixed versions.
        Assert.True(compared >= 5, $"Only {compared} fixed version(s) were executed.");
        Assert.True(
            failures.IsEmpty,
            $"{failures.Count} safe fix(es) out of {compared} executed change what the program does:\n  "
            + string.Join("\n  ", failures.Order(StringComparer.Ordinal))
        );
    }

    /// <summary>
    ///     ⚠ #423's eight rules, which change how many times or in what order an expression is
    ///     evaluated, each against its own executable fixtures — whether the catalogue calls its fix
    ///     safe or not.
    /// </summary>
    /// <remarks>
    ///     The sweep above executes safe fixes only, and two of these rules stay unsafe for a reason
    ///     that is not evaluation count — <c>SK4030</c> for the exception a <c>null</c> receiver throws
    ///     (#437), and <c>SK2064</c> by construction. Their shapes are pinned here all the same, so the
    ///     guard is already proved on the day the other defect is fixed and the rule flips; #430's
    ///     mutating predicates and #431's reads before the overwrite are executable negatives here. An
    ///     edit to <c>[CallerArgumentExpression]</c> text is still left out, as the host leaves it (#422).
    /// </remarks>
    public static TheoryData<string> EvaluationCountRules =>
        new("SK1041", "SK1044", "SK1052", "SK2051", "SK2064", "SK2200", "SK4030", "SK4032");

    [Theory]
    [MemberData(nameof(EvaluationCountRules))]
    public void TheEvaluationCountFix_OnItsOwnExecutableFixtures_PreservesTheResult(string id) {
        var cancellation = TestContext.Current.CancellationToken;
        var failures = new List<string>();
        var compared = 0;

        foreach (var fixture in RuleFixtures.All().Where(fixture => fixture.RuleId == id)) {
            var source = File.ReadAllText(fixture.Path);
            if (!source.Contains("class Probe", StringComparison.Ordinal)) {
                continue;
            }

            var before = RuleFixtures.Compile(source, fixture.Path);
            var expected = Probe(before, cancellation);
            Assert.True(expected is not null, $"{fixture}: declares a Probe that does not run.");
            foreach (var (_, text) in FixedTexts(source, before, rule => rule == id, cancellation)) {
                compared++;
                var actual = Probe(RuleFixtures.Compile(text, fixture.Path), cancellation);
                if (actual != expected) {
                    failures.Add($"{fixture}: Probe.Run() went from {expected} to {actual ?? "<no result>"}");
                }
            }
        }

        // Anti-vacuity: each of the eight has at least one executable positive.
        Assert.True(compared > 0, $"{id}: no fixed version of any of its own fixtures was executed.");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    ///     ⚠ #425's modernization rules, each fix run on its own executable fixtures whether the
    ///     catalogue calls it safe or not.
    /// </summary>
    /// <remarks>
    ///     Each rule's negatives carry the shape #412's audit measured changing the program, with a
    ///     <c>Probe</c>: while the guard holds they do not fire and are not compared here, and the
    ///     rule's negative fixture test is what fails. Remove the guard and the same fixture fires,
    ///     its fixed version runs, and this theory names the result that moved — so a sabotage shows
    ///     the audit's own numbers. The positives pin what the guard still admits.
    /// </remarks>
    public static TheoryData<string> ModernizationRules =>
        new(
            "SK1001",
            "SK1006",
            "SK1010",
            "SK1035",
            "SK1042",
            "SK1054",
            "SK1060",
            "SK1061",
            "SK1063",
            "SK1064",
            "SK1071",
            "SK1072",
            "SK1073",
            "SK1081",
            "SK1091",
            "SK1133"
        );

    [Theory]
    [MemberData(nameof(ModernizationRules))]
    public void TheModernizationFix_OnItsOwnExecutableFixtures_PreservesTheResult(string id) =>
        TheEvaluationCountFix_OnItsOwnExecutableFixtures_PreservesTheResult(id);

    /// <summary>
    ///     ⚠ #425: why <c>SK1073</c> stays unsafe with its expression-tree guard fixed. Replacing a fresh
    ///     <c>EventArgs</c> with the cached one is the rule, and the cached one is one object: a set
    ///     that held three fresh instances holds one.
    /// </summary>
    [Fact]
    public void TheCachedInstanceRewrite_OnFreshEventArgs_ChangesTheResult() {
        var cancellation = TestContext.Current.CancellationToken;
        const string source = """
                              using System;
                              using System.Collections.Generic;

                              public static class Probe {
                                  public static int Run() {
                                      var seen = new HashSet<object>();
                                      for (var i = 0; i < 3; i++) {
                                          seen.Add(new EventArgs());
                                      }

                                      return seen.Count;
                                  }
                              }
                              """;
        var before = RuleFixtures.Compile(source, PlantedPath);
        var (id, text) = Assert.Single(FixedTexts(source, before, static rule => rule == "SK1073", cancellation));

        Assert.Equal("SK1073", id);
        Assert.Contains("seen.Add(EventArgs.Empty)", text, StringComparison.Ordinal);
        Assert.Equal("3", Probe(before, cancellation));
        Assert.Equal("1", Probe(RuleFixtures.Compile(text, PlantedPath), cancellation));
    }

    /// <summary>
    ///     ⚠ #425: why <c>SK1082</c> stays unsafe. On a null receiver <c>ElementAt</c> throws
    ///     <c>ArgumentNullException</c> and the indexer <c>NullReferenceException</c>, and the flow state
    ///     cannot rule that out: <c>!</c> makes it say not-null.
    /// </summary>
    [Fact]
    public void TheIndexerRewrite_OnANullReceiver_ChangesTheException() {
        var cancellation = TestContext.Current.CancellationToken;
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public static class Probe {
                                  static List<int>? Find(bool found) => found ? new List<int> { 1 } : null;

                                  public static int Run() {
                                      var list = Find(false)!;
                                      return list.ElementAt(0);
                                  }
                              }
                              """;
        var before = RuleFixtures.Compile(source, PlantedPath);
        var (id, text) = Assert.Single(FixedTexts(source, before, static rule => rule == "SK1082", cancellation));

        Assert.Equal("SK1082", id);
        Assert.Contains("return list[0];", text, StringComparison.Ordinal);
        Assert.Equal("throws ArgumentNullException", Probe(before, cancellation));
        Assert.Equal("throws NullReferenceException", Probe(RuleFixtures.Compile(text, PlantedPath), cancellation));
    }

    /// <summary>
    ///     ⚠ #437: why <c>SK4030</c> stays unsafe, decided with <c>SK1082</c>. Each <c>Enumerable</c> call it
    ///     rewrites checks its source and the list's own method is an instance call, so a null receiver
    ///     throws <c>ArgumentNullException</c> before and <c>NullReferenceException</c> after.
    /// </summary>
    [Fact]
    public void TheListMethodRewrite_OnANullReceiver_ChangesTheException() {
        var cancellation = TestContext.Current.CancellationToken;
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public static class Probe {
                                  static List<int>? Find(bool found) => found ? new List<int> { 1 } : null;

                                  public static bool Run() {
                                      var list = Find(false)!;
                                      return list.Any(x => x > 0);
                                  }
                              }
                              """;
        var before = RuleFixtures.Compile(source, PlantedPath);
        var (id, text) = Assert.Single(FixedTexts(source, before, static rule => rule == "SK4030", cancellation));

        Assert.Equal("SK4030", id);
        Assert.Contains("list.Exists(", text, StringComparison.Ordinal);
        Assert.Equal("throws ArgumentNullException", Probe(before, cancellation));
        Assert.Equal("throws NullReferenceException", Probe(RuleFixtures.Compile(text, PlantedPath), cancellation));
    }

    /// <summary>
    ///     ⚠ #423: why <c>SK2064</c> stays unsafe with its guard fixed. On the null guard the rule
    ///     exists for, <c>&amp;</c> throws and <c>&amp;&amp;</c> does not: the finding is the behaviour
    ///     change, as it is for <c>SK2181</c>, which ships unsafe for the same reason.
    /// </summary>
    [Fact]
    public void TheShortCircuitRewrite_OnTheGuardItReports_ChangesTheResult() {
        var cancellation = TestContext.Current.CancellationToken;
        const string source = """
                              public sealed class Node { public bool Ready; }

                              public static class Probe {
                                  static bool Go(Node? node) => node != null & node.Ready;

                                  public static bool Run() => Go(null);
                              }
                              """;
        var before = RuleFixtures.Compile(source, PlantedPath);
        var (id, text) = Assert.Single(FixedTexts(source, before, static rule => rule == "SK2064", cancellation));

        Assert.Equal("SK2064", id);
        Assert.Contains("node != null && node.Ready", text, StringComparison.Ordinal);
        Assert.Equal("throws NullReferenceException", Probe(before, cancellation));
        Assert.Equal("False", Probe(RuleFixtures.Compile(text, PlantedPath), cancellation));
    }

    /// <summary>
    ///     ⚠ The instrument check for the runtime sweep: a probe whose result moves must be seen, and
    ///     one that does not must not.
    /// </summary>
    [Fact]
    public void TheProbe_SeesAChangedResult_AndNotAnUnchangedOne() {
        var cancellation = TestContext.Current.CancellationToken;
        const string source = "public static class Probe { public static int Run() => 1 + 1; }";
        var two = Probe(RuleFixtures.Compile(source, PlantedPath), cancellation);

        Assert.Equal("2", two);
        Assert.Equal(
            two,
            Probe(
                RuleFixtures.Compile(source.Replace("1 + 1", "2", StringComparison.Ordinal), PlantedPath),
                cancellation
            )
        );
        Assert.NotEqual(
            two,
            Probe(
                RuleFixtures.Compile(source.Replace("1 + 1", "1 - 1", StringComparison.Ordinal), PlantedPath),
                cancellation
            )
        );
        Assert.Null(Probe(RuleFixtures.Compile("public static class Probe { }", PlantedPath), cancellation));
    }

    /// <summary>
    ///     ⚠ #412's audit: <c>SK1030</c>'s old rewrite on a property with a setter body compiles and
    ///     changes <c>Probe.Run()</c>, which is why the negative fixture is a negative.
    /// </summary>
    [Fact]
    public void TheNullCoalescingRewrite_OnASetterWithABody_ChangesTheResult() {
        var cancellation = TestContext.Current.CancellationToken;
        var path = Path.Combine(RuleFixtures.Root, "SK1030", "negative", "a-property-with-a-setter-body.cs");
        var source = File.ReadAllText(path);
        var rewritten = source.Replace(
            """box.Name = box.Name ?? "fallback";""",
            """box.Name ??= "fallback";""",
            StringComparison.Ordinal
        );

        Assert.NotEqual(source, rewritten);
        Assert.Equal("1", Probe(RuleFixtures.Compile(source, path), cancellation));
        Assert.Equal("0", Probe(RuleFixtures.Compile(rewritten, path), cancellation));
    }

    /// <summary>
    ///     <c>Probe.Run()</c>'s result as invariant text, or <see langword="null" /> when the compilation
    ///     declares no such member; an exception is a result too, by type name.
    /// </summary>
    internal static string? Probe(Compilation compilation, CancellationToken cancellation) {
        using var image = new MemoryStream();
        if (!compilation.Emit(image, cancellationToken: cancellation).Success) {
            return null;
        }

        image.Position = 0;
        var context = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);
        try {
            var run = context.LoadFromStream(image)
                .GetTypes()
                .FirstOrDefault(static type => type.Name == "Probe")
                ?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
            if (run is null) {
                return null;
            }

            try {
                return Convert.ToString(run.Invoke(null, null), CultureInfo.InvariantCulture) ?? "<null>";
            } catch (TargetInvocationException thrown) {
                return "throws " + thrown.InnerException?.GetType().Name;
            }
        } finally {
            context.Unload();
        }
    }

    /// <summary>Each safe-fix rule's edits on one source, applied rule by rule, and what they broke.</summary>
    static List<string> Regressions(string source, string path, ref int applied, CancellationToken cancellation) {
        var before = RuleFixtures.Compile(source, path);
        var result = new List<string>();
        foreach (var (id, text) in FixedTexts(source, before, cancellation)) {
            Interlocked.Increment(ref applied);
            var broken = NewErrors(before, text, path, cancellation);
            if (broken.Length > 0) {
                result.Add(id + " introduces " + string.Join(", ", broken));
            }
        }

        return result;
    }

    /// <summary>
    ///     The source after each safe-fix rule's edits, one rule at a time — grouped as <c>skala fix</c>
    ///     applies a pass.
    /// </summary>
    static IEnumerable<(string Id, string Text)> FixedTexts(
        string source,
        CSharpCompilation before,
        CancellationToken cancellation
    ) =>
        FixedTexts(source, before, null, cancellation);

    /// <summary>
    ///     The same, for the rules <paramref name="applies" /> names whatever the catalogue says of them —
    ///     still minus an edit to <c>[CallerArgumentExpression]</c> text, which the host never marks safe
    ///     (#422) — or for every finding the host would apply as safe when it is <see langword="null" />.
    /// </summary>
    static IEnumerable<(string Id, string Text)> FixedTexts(
        string source,
        CSharpCompilation before,
        Func<string, bool>? applies,
        CancellationToken cancellation
    ) {
        var findings = RuleFixtures.Analyze(before, SkalaAnalyzers.All, cancellation);

        // ⚠ #422: safe as the host decides it, per finding, not as the catalogue says it per rule —
        // the sweep measures what `skala fix --safe` would apply, and that is the narrower set.
        var models = new Dictionary<SyntaxTree, SemanticModel>();
        foreach (var group in findings
                     .Where(diagnostic => diagnostic.Location.SourceTree is { } tree
                         && (applies is null
                                 ? FixEdits.IsSafe(diagnostic, Model(models, before, tree), cancellation)
                                 : applies(diagnostic.Id)
                                 && !FixEdits.Read(diagnostic)
                                     .Any(edit => CallerArgumentSafety.ChangesCapturedText(
                                             Model(models, before, tree),
                                             edit.Span,
                                             cancellation
                                         )
                                     ))
                     )
                     .GroupBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal)) {
            var edits = group.SelectMany(FixRoundTripTests.ReadEdits)
                .OrderByDescending(static edit => edit.Start)
                .ToList();
            if (edits.Count == 0) {
                continue;
            }

            var text = source;
            var consumed = int.MaxValue;
            foreach (var (start, length, replacement) in edits) {
                // As in FixRoundTripTests: an overlapping edit is the next pass's business.
                if (start + length > consumed) {
                    continue;
                }

                text = text[..start] + replacement + text[(start + length)..];
                consumed = start;
            }

            yield return (group.Key, text);
        }
    }

    static SemanticModel Model(Dictionary<SyntaxTree, SemanticModel> models, Compilation compilation, SyntaxTree tree) {
        if (!models.TryGetValue(tree, out var model)) {
            model = compilation.GetSemanticModel(tree);
            models[tree] = model;
        }

        return model;
    }

    /// <summary>The error ids the edited text has more of than the original, counted per id.</summary>
    /// <remarks>
    ///     ⚠ Per id rather than per (line, id), as in <see cref="FixRoundTripTests" />: an edit that
    ///     removes a line moves every error below it, and a line-keyed comparison calls the move a
    ///     regression.
    /// </remarks>
    static ImmutableArray<string> NewErrors(
        Compilation before,
        string text,
        string path,
        CancellationToken cancellation
    ) {
        var was = FixRoundTripTests.ErrorsById(before, cancellation);
        var now = FixRoundTripTests.ErrorsById(RuleFixtures.Compile(text, path), cancellation);
        return [
            ..now
                .Where(entry => entry.Value > was.GetValueOrDefault(entry.Key))
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => entry.Key
                    + " ×"
                    + (entry.Value - was.GetValueOrDefault(entry.Key)).ToString(CultureInfo.InvariantCulture)
                )
        ];
    }

    /// <summary>
    ///     ⚠ #425: the rules whose fixes #412's audit measured changing a program for a reason of their
    ///     own — a value's representation, a delegate's identity, a scan order, an exception pass, a
    ///     disposal's timing — each against its own executable fixtures, whether the catalogue calls its
    ///     fix safe or not.
    /// </summary>
    /// <remarks>
    ///     Every recorded shape is a negative fixture with a <c>Probe</c>, so the guard declines it and
    ///     nothing is applied there; a sabotaged guard turns the negative red. What this theory adds is
    ///     the positive side: each rule has an executable positive whose fix is applied and run, so a
    ///     guard that admits a shape it should not shows up here as a changed result. <c>SK4010</c>,
    ///     <c>SK7110</c> and <c>SK2113</c> stay unsafe by construction (an exception message, a log
    ///     category, a throw where null was returned), and the first is pinned here all the same.
    /// </remarks>
    public static TheoryData<string> SemanticChangeRules =>
        new(
            "SK0231",
            "SK0232",
            "SK0240",
            "SK0244",
            "SK0280",
            "SK2015",
            "SK2121",
            "SK3501",
            "SK3510",
            "SK4010",
            "SK4021",
            "SK4031",
            "SK4034",
            "SK8022"
        );

    [Theory]
    [MemberData(nameof(SemanticChangeRules))]
    public void TheSemanticFix_OnItsOwnExecutableFixtures_PreservesTheResult(string id) =>
        TheEvaluationCountFix_OnItsOwnExecutableFixtures_PreservesTheResult(id);

    /// <summary>
    ///     ⚠ #425: why <c>SK4010</c> stays unsafe. Folding the predicate into <c>First</c> keeps the
    ///     exception's type and changes its message, and the message is what the fix rewrites.
    /// </summary>
    [Fact]
    public void TheFoldedPredicate_OnNoMatch_ChangesTheExceptionMessage() {
        var cancellation = TestContext.Current.CancellationToken;
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              using System.Linq;

                              public static class Probe {
                                  public static string Run() {
                                      var values = new List<int> { 1, 2 };
                                      try {
                                          return values.Where(value => value > 5).First().ToString();
                                      } catch (InvalidOperationException thrown) {
                                          return thrown.Message;
                                      }
                                  }
                              }
                              """;
        var before = RuleFixtures.Compile(source, PlantedPath);
        var (id, text) = Assert.Single(FixedTexts(source, before, static rule => rule == "SK4010", cancellation));

        Assert.Equal("SK4010", id);
        Assert.Contains("values.First(value => value > 5)", text, StringComparison.Ordinal);
        Assert.NotEqual(Probe(before, cancellation), Probe(RuleFixtures.Compile(text, PlantedPath), cancellation));
    }
}
