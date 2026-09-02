using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Modernization;
using Rikarin.Skala.Rules.Security;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2230</c>–<c>SK2233</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception,
///     reports it as <c>AD0001</c>, and the analyzer then produces nothing at all — so the positives
///     fail, which reads as "the rule needs another condition", and every "should not fire" fixture
///     passes, which reads as a spotless false-positive record.
///     <para>
///         ⚠ This batch has its own reason to worry. <c>SK2231</c> walks every identifier in a member
///         body and dereferences the symbol it binds to, <c>SK2230</c> indexes into literal text on
///         both sides of a join, and <c>SK2233</c> resolves an argument through a parameter list that
///         a named argument can reorder. Every one of those is an index or a null away from throwing.
///     </para>
///     <para>
///         ⚠ The rest of this file pins the claims the rules are <em>built on</em>, which no fixture
///         can: that the neighbouring rules are disjoint rather than merely differently-worded, that
///         the fix is withheld where a directive sits in the span, and that <c>SK1073</c> already owns
///         <c>new Guid()</c> — the evidence #187 was refuted on.
///     </para>
/// </remarks>
public sealed class SqlAndReflectionBatchTests {
    static readonly ImmutableArray<string> Ids = ["SK2230", "SK2231", "SK2232", "SK2233"];

    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new SqlFragmentsRunTogetherAnalyzer(), new CommandParameterNotSuppliedAnalyzer(),
        new AssemblyLoadedOutsideItsContextAnalyzer(), new MistakenTypeArgumentAnalyzer()
    ];

    /// <summary>The batch plus every rule it is claimed not to collide with.</summary>
    static readonly ImmutableArray<DiagnosticAnalyzer> WithNeighbours = [
        new SqlFragmentsRunTogetherAnalyzer(), new CommandParameterNotSuppliedAnalyzer(),
        new AssemblyLoadedOutsideItsContextAnalyzer(), new MistakenTypeArgumentAnalyzer(),
        new SqlInjectionAnalyzer(), new EnumGetValuesAnalyzer(), new GetTypeOnATypeAnalyzer(),
        new CachedEmptyInstanceAnalyzer()
    ];

    public static TheoryData<string> Fixtures {
        get {
            var data = new TheoryData<string>();
            foreach (var fixture in RuleFixtures.All().Where(static f => Ids.Contains(f.RuleId))) {
                data.Add(fixture.Path);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoFixture_CrashesAnAnalyzer(string path) {
        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(File.ReadAllText(path), path),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    /// <summary>
    ///     ⚠ The fixture verdict with <em>only</em> this batch's four analyzers loaded.
    /// </summary>
    /// <remarks>
    ///     <see cref="RuleFixtureTests.Rule_FiresExactlyWhereTheFixtureSaysItShould" /> asks the same
    ///     question of every rule at once, which is the right default and the wrong instrument for
    ///     two jobs. It runs two hundred analyzers over three and a half thousand files, so a
    ///     sabotage — break a guard, see what turns red — costs a minute and a half per attempt, and
    ///     a pass that takes that long is a pass that gets skipped. And it cannot separate "this rule
    ///     fired" from "some other analyzer in the set threw and took this one's compilation with
    ///     it". Scoped to four analyzers and this batch's fixtures it answers in seconds, which is
    ///     what makes the sabotage pass something anyone will actually run.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixtureInTheBatch_FiresExactlyWhereItSays(string path) {
        var fixture = RuleFixtures.All().Single(candidate => candidate.Path == path);
        var produced = RuleFixtures
            .Analyze(
                RuleFixtures.Compile(File.ReadAllText(path), path),
                Analyzers,
                TestContext.Current.CancellationToken
            )
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        Assert.True(
            fixture.ShouldFire == produced.Length > 0,
            $"{fixture}: {fixture.RuleId} fired {produced.Length} time(s):\n  "
            + string.Join("\n  ", produced.Select(static d => d.Location.GetLineSpan() + ": " + d.GetMessage()))
        );
    }

    /// <summary>
    ///     ⚠ Anti-vacuity for the test above: an analyzer set that never runs also never crashes.
    /// </summary>
    [Fact]
    public void TheFixtureSet_ReallyReachesEveryRuleInTheBatch() {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fixture in RuleFixtures.All()) {
            if (!fixture.ShouldFire || !Ids.Contains(fixture.RuleId)) {
                continue;
            }

            foreach (var diagnostic in RuleFixtures.Analyze(
                         RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path),
                         Analyzers,
                         TestContext.Current.CancellationToken
                     )) {
                seen.Add(diagnostic.Id);
            }
        }

        Assert.Equal(Ids, seen.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     ⚠ <c>SK5001</c> and <c>SK2230</c> on one file that satisfies both shapes.
    /// </summary>
    /// <remarks>
    ///     A file where the two rules merely differ proves nothing. This one has a query built from a
    ///     request value <em>and</em> a fused literal join in the same method, and the claim is that
    ///     each rule takes exactly its own one: <c>SK5001</c> needs a tainted value in the SQL and
    ///     <c>SK2230</c> refuses to read a concatenation with a non-literal operand, so no single
    ///     expression can produce both.
    /// </remarks>
    [Fact]
    public void TaintedSqlAndFusedSql_AreTakenByDifferentRules() {
        const string source = """
                              using System.Data;
                              using System.Net;

                              public sealed class Reports {
                                  public void Tainted(IDbCommand command, HttpListenerRequest request) {
                                      var who = request.QueryString["who"];
                                      command.CommandText = "select * from audit where who = '" + who + "'order by at";
                                  }

                                  public void Fused(IDbCommand command) {
                                      command.CommandText = "select * from audit where kind = 3"
                                          + "order by at";
                                  }
                              }
                              """;

        var byLine = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var diagnostic in RuleFixtures.Analyze(
                     RuleFixtures.Compile(source, "disjoint.cs"),
                     WithNeighbours,
                     TestContext.Current.CancellationToken
                 )) {
            if (!byLine.TryGetValue(diagnostic.Id, out var lines)) {
                byLine[diagnostic.Id] = lines = [];
            }

            lines.Add(diagnostic.Location.GetLineSpan().StartLinePosition.Line);
        }

        Assert.True(
            byLine.ContainsKey("SK5001"),
            "SK5001 did not fire on the tainted half, so this file proves nothing about disjointness. "
            + "Found: "
            + string.Join(", ", byLine.Keys.Order(StringComparer.Ordinal))
        );

        Assert.True(byLine.ContainsKey("SK2230"), "SK2230 did not fire on the fused half.");
        Assert.Empty(byLine["SK5001"].Intersect(byLine["SK2230"]));
    }

    /// <summary>
    ///     ⚠ <c>SK1035</c> and <c>SK2233</c> are disjoint by their own conditions, on one file.
    /// </summary>
    /// <remarks>
    ///     <c>SK1035</c> offers <c>Enum.GetValues&lt;T&gt;()</c> and so requires the operand to
    ///     <em>be</em> an enum, because the generic overload is constrained <c>struct, Enum</c>. This
    ///     rule requires it not to be. The two calls below differ only in that, and the assertion is
    ///     that neither diagnostic appears on the other's line.
    /// </remarks>
    [Fact]
    public void EnumGetValues_GoesToOneRuleOrTheOther() {
        const string source = """
                              using System;

                              public enum Kind { First, Second }

                              public sealed class Widget { }

                              public sealed class Registry {
                                  public void Good() {
                                      foreach (var value in Enum.GetValues(typeof(Kind))) {
                                          _ = value;
                                      }
                                  }

                                  public void Bad() {
                                      foreach (var value in Enum.GetValues(typeof(Widget))) {
                                          _ = value;
                                      }
                                  }
                              }
                              """;

        var lines = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "enum.cs"), WithNeighbours, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id is "SK1035" or "SK2233")
            .ToLookup(
                static diagnostic => diagnostic.Id,
                static diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line,
                StringComparer.Ordinal
            );

        Assert.Single(lines["SK1035"]);
        Assert.Single(lines["SK2233"]);
        Assert.Empty(lines["SK1035"].Intersect(lines["SK2233"]));
    }

    /// <summary>
    ///     ⚠ The refutation of #187, as a test rather than as a sentence in doc 08.
    /// </summary>
    /// <remarks>
    ///     #187 asked for a rule reporting <c>new Guid()</c>. <c>SK1073</c> already reports exactly
    ///     that span, with a fix, on by default — from the other direction, offering
    ///     <c>Guid.Empty</c>. A second rule offering <c>Guid.NewGuid()</c> would be two findings and
    ///     two contradictory fixes on one expression. The day <c>SK1073</c> stops covering it, this
    ///     goes red and the refutation is re-examined instead of quietly outliving its evidence.
    /// </remarks>
    [Fact]
    public void Sk1073_AlreadyOwnsNewGuid() {
        const string source = """
                              using System;

                              public sealed class Registry {
                                  public bool IsUnset(Guid id) => id == new Guid();
                              }
                              """;

        var found = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "guid.cs"), WithNeighbours, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id == "SK1073")
            .ToArray();

        Assert.Single(found);
        Assert.Contains("Guid.Empty", found[0].GetMessage(), StringComparison.Ordinal);
        Assert.Equal("1", found[0].Properties[FixEdits.CountKey]);
    }

    /// <summary>
    ///     ⚠ The withheld-fix path, which no fixture can reach.
    /// </summary>
    /// <remarks>
    ///     <see cref="RuleFixtureTests.EveryFix_ProducesTextThatStillParses" /> asserts that every
    ///     positive fixture of a rule the catalogue says has a fix <em>carries</em> one, so a fixture
    ///     for the case where the fix is deliberately absent would fail. The finding is still
    ///     reported — the code is still wrong — and only the edit is withheld.
    /// </remarks>
    [Theory]
    [InlineData(
        "SK2230",
        """
        public sealed class Queries {
            public string ByActive() =>
                "select id from users"
        #if LEGACY
                + "where active = 1";
        #else
                + "where active = 1";
        #endif
        }
        """
    )]
    [InlineData(
        "SK2232",
        """
        using System.Reflection;
        using System.Runtime.Loader;

        public sealed class PluginContext : AssemblyLoadContext {
            protected override Assembly Load(AssemblyName name) =>
                Assembly. /* the default context, deliberately */ LoadFrom(name.Name + ".dll");
        }
        """
    )]
    public void ADirectiveOrCommentInTheSpan_WithholdsTheFixAndKeepsTheFinding(string id, string source) {
        var found = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "withheld.cs"), Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == id)
            .ToArray();

        Assert.NotEmpty(found);
        foreach (var diagnostic in found) {
            Assert.False(
                diagnostic.Properties.ContainsKey(FixEdits.CountKey),
                $"{id} offered a fix over a span holding a comment or a directive."
            );
        }
    }

    /// <summary>
    ///     ⚠ <c>SK2231</c>'s marker parser, driven through the analyzer rather than called directly.
    /// </summary>
    /// <remarks>
    ///     Each row is a text where a scan that only looked for the <c>@</c> sigil would report a
    ///     parameter that is not one. <c>@@identity</c> is the row that was already wrong: skipping
    ///     only the first sigil left the loop standing on the second, whose predecessor is <c>@</c>
    ///     and therefore not a name character, so it read <c>@identity</c> as a missing binding.
    /// </remarks>
    [Theory]
    [InlineData("insert into t (id) values (@id); select @@identity", false)]
    [InlineData("select * from t where id = @id and mail = 'root@localhost'", false)]
    [InlineData("select * from t /* @status */ where id = @id -- @tenant", false)]
    [InlineData("select * from t where id = @id and note = 'ask @support'", false)]
    [InlineData("select * from t where id = @id and status = @status", true)]
    [InlineData("select * from t where id = @id and t.x = @x", true)]
    public void OnlyARealMarker_IsCountedAsUnsupplied(string sql, bool reports) {
        var source = "using System.Collections;\nusing System.Data;\n\n"
            + "public sealed class Orders {\n"
            + "    public void Load(int id) {\n"
            + "        var command = new Command();\n"
            + "        command.CommandText = \""
            + sql
            + "\";\n"
            + "        command.Parameters.AddWithValue(\"@id\", id);\n"
            + "        command.ExecuteNonQuery();\n"
            + "    }\n"
            + "}\n\n"
            + Scaffolding;

        var compilation = RuleFixtures.Compile(source, "markers.cs");
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        );

        var found = RuleFixtures
            .Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id == "SK2231")
            .ToArray();

        Assert.Equal(reports, found.Length > 0);
    }

    /// <summary>
    ///     The <c>IDbCommand</c> the marker rows are driven through, taken from a fixture rather than
    ///     copied, so the two cannot drift apart.
    /// </summary>
    static string Scaffolding { get; } = Read();

    static string Read() {
        var text = File.ReadAllText(Path.Combine(RuleFixtures.Root, "SK2231", "negative", "every_marker_is_bound.cs"));

        var start = text.IndexOf("sealed class Bag", StringComparison.Ordinal);
        Assert.True(start > 0, "The SK2231 fixture no longer carries the command scaffolding.");
        return text[start..];
    }
}
