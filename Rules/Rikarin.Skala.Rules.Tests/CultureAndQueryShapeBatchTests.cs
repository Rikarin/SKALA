using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2150</c>–<c>SK2154</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c>, and the analyzer then produces nothing at all — so the positives fail,
///     which reads as "the rule is wrong", and every "should not fire" fixture passes, which reads as
///     a clean false-positive record. The fixture harness does not check for it (issue #279), so these
///     tests do.
///     <para>
///         ⚠ <b>The second thing the harness cannot ask is "did this arm fire for the reason I think".</b>
///         A positive fixture passes on one finding, so a rule with two arms can ship with one of them
///         inverted and look green — which is exactly what happened here. <c>SK2154</c>'s LINQ arm
///         counted parameters against the <em>reduced</em> extension method, so it matched
///         <c>OrderBy(key, comparer)</c> — the overload that supplies the ordering — and missed
///         <c>OrderBy(key)</c> entirely. The positives stayed green throughout because
///         <c>List&lt;T&gt;.Sort()</c> covered them. The counts below are what make an arm's silence
///         visible.
///     </para>
/// </remarks>
public sealed class CultureAndQueryShapeBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new ImplicitStringSearchCultureAnalyzer(), new InvariantCultureComparisonAnalyzer(),
        new PlatformDependentPathComparisonAnalyzer(), new QueryableDegradedToEnumerableAnalyzer(),
        new SortWithoutOrderingAnalyzer()
    ];

    static ImmutableArray<Diagnostic> Run(string source, string name) =>
        RuleFixtures.Analyze(RuleFixtures.Compile(source, name), Analyzers, TestContext.Current.CancellationToken);

    static int Count(ImmutableArray<Diagnostic> diagnostics, string ruleId) =>
        diagnostics.Count(diagnostic => diagnostic.Id == ruleId);

    static void NoCrash(ImmutableArray<Diagnostic> diagnostics) =>
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");

    /// <summary>
    ///     ⚠ <c>SK2150</c>'s method table, one call per row, asserted as a count rather than as
    ///     "something fired".
    /// </summary>
    /// <remarks>
    ///     The four culture-sensitive names are reported and the five already-ordinal shapes beside
    ///     them are not. Both halves are in one compilation on purpose: a rule that has stopped running
    ///     scores zero on the first half and full marks on the second, and only comparing them in one
    ///     place tells those apart.
    /// </remarks>
    [Fact]
    public void TheSearchMethodTable_IsTheFrameworksAndNotAGuess() {
        const string source = """
                              using System;

                              class Table {
                                  // Culture-sensitive: four findings.
                                  int A(string s) => s.IndexOf("x");
                                  int B(string s) => s.LastIndexOf("x");
                                  bool C(string s) => s.StartsWith("x");
                                  bool D(string s) => s.EndsWith("x");

                                  // Already ordinal on .NET: no finding.
                                  bool E(string s) => s.Contains("x");
                                  int F(string s) => s.IndexOf('x');
                                  int G(string s) => s.LastIndexOf('x');
                                  bool H(string s) => s.StartsWith('x');
                                  bool I(string s) => s.EndsWith('x');
                              }
                              """;

        var diagnostics = Run(source, "Table.cs");
        NoCrash(diagnostics);
        Assert.Equal(4, Count(diagnostics, RuleIds.ImplicitStringSearchCulture));
    }

    /// <summary>
    ///     ⚠ The line between <c>SK2150</c> and <c>SK2010</c>, asserted rather than described.
    /// </summary>
    /// <remarks>
    ///     The two rules are adjacent enough that a reader may reasonably expect them to overlap, and
    ///     an overlap would double every finding in this area. <c>SK2010</c> owns comparison —
    ///     <c>string.Compare</c> and casing inside an equality — and this rule owns search. Neither
    ///     source below produces a finding from the other rule.
    /// </remarks>
    [Fact]
    public void SearchAndComparison_AreOwnedByExactlyOneRuleEach() {
        const string comparison = """
                                  class Comparison {
                                      bool M(string a, string b) => string.Compare(a, b) == 0;
                                      bool N(string a, string b) => a.ToLower() == b;
                                  }
                                  """;

        const string search = """
                              class Search {
                                  int M(string s) => s.LastIndexOf("-");
                                  bool N(string s) => s.StartsWith("sk.");
                              }
                              """;

        var onComparison = RuleFixtures.Analyze(
            RuleFixtures.Compile(comparison, "Comparison.cs"),
            [.. Analyzers, new ImplicitStringCultureAnalyzer()],
            TestContext.Current.CancellationToken
        );
        var onSearch = RuleFixtures.Analyze(
            RuleFixtures.Compile(search, "Search.cs"),
            [.. Analyzers, new ImplicitStringCultureAnalyzer()],
            TestContext.Current.CancellationToken
        );

        NoCrash(onComparison);
        NoCrash(onSearch);

        Assert.Equal(0, Count(onComparison, RuleIds.ImplicitStringSearchCulture));
        Assert.Equal(2, Count(onComparison, RuleIds.ImplicitStringCulture));
        Assert.Equal(2, Count(onSearch, RuleIds.ImplicitStringSearchCulture));
        Assert.Equal(0, Count(onSearch, RuleIds.ImplicitStringCulture));
    }

    /// <summary>
    ///     ⚠ <c>SK2151</c>'s whole safety argument, as a compilation: the enum is reported and
    ///     <c>CultureInfo.InvariantCulture</c> is not, in the same file.
    /// </summary>
    /// <remarks>
    ///     Invariant culture is <em>correct</em> for round-tripping formatted data. A version of this
    ///     rule that matched on the member name rather than on the type would advise every author here
    ///     to corrupt their own serialisation, and it would look identical in the fixture harness
    ///     because the formatting fixture would simply be a bigger negative set.
    /// </remarks>
    [Fact]
    public void InvariantCulture_IsReportedAsAPolicyAndNeverAsAFormat() {
        const string source = """
                              using System;
                              using System.Globalization;

                              class Both {
                                  // Comparison policy: three findings.
                                  bool A(string a, string b) => string.Equals(a, b, StringComparison.InvariantCulture);
                                  bool B(string s) => s.StartsWith("x", StringComparison.InvariantCulture);
                                  bool C(string s) => s.EndsWith("x", StringComparison.InvariantCultureIgnoreCase);

                                  // Formatting and parsing: no finding, ever.
                                  string D(decimal v) => v.ToString(CultureInfo.InvariantCulture);
                                  decimal E(string t) => decimal.Parse(t, CultureInfo.InvariantCulture);
                                  string F(int a) => string.Format(CultureInfo.InvariantCulture, "{0}", a);

                                  // Ordering: excluded on purpose, because collation is wanted there.
                                  int G(string a, string b) => string.Compare(a, b, StringComparison.InvariantCulture);
                              }
                              """;

        var diagnostics = Run(source, "Both.cs");
        NoCrash(diagnostics);
        Assert.Equal(3, Count(diagnostics, RuleIds.InvariantCultureComparison));
    }

    /// <summary>
    ///     ⚠ <c>SK2152</c>'s proof obligation: a path is a symbol fact, and a name is not a proof.
    /// </summary>
    /// <remarks>
    ///     The two halves are written to be indistinguishable to a reader scanning for the word "path",
    ///     which is what a name-based version of this rule would be doing.
    /// </remarks>
    [Fact]
    public void APathIsASymbolFact_AndAParameterNameIsNotOne() {
        const string source = """
                              using System;
                              using System.IO;

                              class Paths {
                                  // Provably a path: two findings.
                                  bool A(string a, string b) => Path.GetFullPath(a).Equals(b, StringComparison.OrdinalIgnoreCase);
                                  bool B(FileInfo f, string b) => f.FullName.Equals(b, StringComparison.OrdinalIgnoreCase);

                                  // Spelled like a path and never proven to be one: no finding.
                                  bool C(string filePath, string b) => filePath.Equals(b, StringComparison.OrdinalIgnoreCase);
                                  bool D(string fullPath, string b) => fullPath.StartsWith(b, StringComparison.OrdinalIgnoreCase);
                                  bool E(string directoryName, string b) => directoryName.Equals(b, StringComparison.OrdinalIgnoreCase);

                                  // A path compared with a value chosen at run time: the shape the rule asks for.
                                  static StringComparison Selected { get; } =
                                      OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                                  bool F(string a, string b) => Path.GetFullPath(a).Equals(b, Selected);
                              }
                              """;

        var diagnostics = Run(source, "Paths.cs");
        NoCrash(diagnostics);
        Assert.Equal(2, Count(diagnostics, RuleIds.PlatformDependentPathComparison));
    }

    /// <summary>
    ///     ⚠ <c>SK2153</c> must never report <c>.AsEnumerable()</c>, and the first version did.
    /// </summary>
    /// <remarks>
    ///     <c>AsEnumerable</c> is itself an <c>Enumerable</c> extension returning
    ///     <c>IEnumerable&lt;T&gt;</c> on an <c>IQueryable</c> receiver, which is precisely the shape
    ///     this rule matches. Reporting it means reporting the sanctioned way to say "client-side, on
    ///     purpose" — a rule that reports its own escape hatch. The receiver's static type excludes
    ///     everything chained <em>after</em> the call and cannot exclude the call.
    /// </remarks>
    [Fact]
    public void TheDeliberateEscapeHatch_IsNeverReported() {
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              using System.Linq;

                              class Row { public int Status { get; set; } }

                              class Queries {
                                  // Degraded: two findings.
                                  IEnumerable<Row> A(IQueryable<Row> q, Func<Row, bool> p) => q.Where(p);
                                  IEnumerable<Row> B(IQueryable<Row> q, Func<Row, int> k) => q.OrderBy(k);

                                  // Deliberate, and the escape hatch itself: no finding.
                                  IEnumerable<Row> C(IQueryable<Row> q) => q.AsEnumerable();
                                  IEnumerable<Row> D(IQueryable<Row> q, Func<Row, bool> p) => q.AsEnumerable().Where(p);

                                  // Materialisation and scalars: no finding.
                                  List<Row> E(IQueryable<Row> q) => q.ToList();
                                  int F(IQueryable<Row> q) => q.Count();

                                  // Still a query: no finding.
                                  IQueryable<Row> G(IQueryable<Row> q) => q.Where(r => r.Status == 0);
                              }
                              """;

        var diagnostics = Run(source, "Queries.cs");
        NoCrash(diagnostics);
        Assert.Equal(2, Count(diagnostics, RuleIds.QueryableDegradedToEnumerable));
    }

    /// <summary>
    ///     ⚠ <c>SK2154</c>'s LINQ arm, which shipped inverted and green.
    /// </summary>
    /// <remarks>
    ///     The count is what matters: <c>OrderBy(key)</c> must be reported and
    ///     <c>OrderBy(key, comparer)</c> must not. Reading the parameter count off the reduced
    ///     extension method reverses exactly that pair while leaving <c>List&lt;T&gt;.Sort()</c> — and
    ///     therefore every positive fixture — working.
    /// </remarks>
    [Fact]
    public void SuppliedOrderingsAreSilent_AndMissingOnesAreNot() {
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              using System.Linq;

                              sealed class Point { public int X { get; set; } }

                              sealed class ByX : IComparer<Point> {
                                  public int Compare(Point a, Point b) => 0;
                              }

                              class Sorts {
                                  // No ordering available: four findings.
                                  void A(List<Point> p) => p.Sort();
                                  void B(Point[] p) => Array.Sort(p);
                                  IEnumerable<Point> C(IEnumerable<Point> p) => p.OrderBy(x => x);
                                  IEnumerable<Point> D(IEnumerable<Point> p) => p.OrderByDescending(x => x);

                                  // Ordering supplied: no finding.
                                  void E(List<Point> p) => p.Sort(new ByX());
                                  void F(List<Point> p) => p.Sort((a, b) => a.X - b.X);
                                  void G(Point[] p) => Array.Sort(p, new ByX());
                                  IEnumerable<Point> H(IEnumerable<Point> p) => p.OrderBy(x => x, new ByX());
                                  IEnumerable<Point> I(IEnumerable<Point> p) => p.OrderBy(x => x.X);
                              }
                              """;

        var diagnostics = Run(source, "Sorts.cs");
        NoCrash(diagnostics);
        Assert.Equal(4, Count(diagnostics, RuleIds.SortWithoutOrdering));
    }

    /// <summary>
    ///     ⚠ The decidability boundary <c>SK2154</c> is built on, asserted as silence with a witness.
    /// </summary>
    /// <remarks>
    ///     Every shape here would throw under a naive reading of the declaration and every one of them
    ///     sorts correctly at run time. The witness is the sealed type in the same compilation: without
    ///     it, an analyzer that had stopped running would pass this test perfectly.
    /// </remarks>
    [Fact]
    public void TheUndecidableShapes_AreSilentAndTheWitnessIsNot() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              class Animal { }

                              sealed class Point { public int X { get; set; } }

                              class Boundary {
                                  // Undecidable, and correct at run time: no finding.
                                  void A<T>(List<T> items) => items.Sort();
                                  IEnumerable<T> B<T>(IEnumerable<T> items) => items.OrderBy(i => i);
                                  void C(List<Animal> a) => a.Sort();
                                  void D(List<object> o) => o.Sort();
                                  void E(List<int?> n) => n.Sort();
                                  IEnumerable<int?> F(IEnumerable<int?> n) => n.OrderBy(v => v);

                                  // The witness: one finding, so the silence above is a verdict.
                                  void G(List<Point> p) => p.Sort();
                              }
                              """;

        var diagnostics = Run(source, "Boundary.cs");
        NoCrash(diagnostics);
        Assert.Equal(1, Count(diagnostics, RuleIds.SortWithoutOrdering));
    }

    /// <summary>
    ///     ⚠ Anti-vacuity for every <c>NoCrash</c> above: an analyzer set that never runs never crashes.
    /// </summary>
    /// <remarks>
    ///     Four of these five rules return from <c>RegisterCompilationStartAction</c> without
    ///     registering anything when a framework type does not resolve, so "no <c>AD0001</c>" would
    ///     otherwise be a fact about the reference set rather than about the rules. This asserts that
    ///     one source really does reach all five.
    /// </remarks>
    [Fact]
    public void AllFiveRules_ReallyReachTheirShapes() {
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              using System.IO;
                              using System.Linq;

                              sealed class Point { public int X { get; set; } }

                              class Reached {
                                  int A(string s) => s.LastIndexOf("-");
                                  bool B(string a, string b) => string.Equals(a, b, StringComparison.InvariantCulture);
                                  bool C(string a, string b) => Path.GetFullPath(a).Equals(b, StringComparison.OrdinalIgnoreCase);
                                  IEnumerable<Point> D(IQueryable<Point> q, Func<Point, bool> p) => q.Where(p);
                                  void E(List<Point> p) => p.Sort();
                              }
                              """;

        var diagnostics = Run(source, "Reached.cs");
        NoCrash(diagnostics);

        Assert.Contains(diagnostics, static d => d.Id == RuleIds.ImplicitStringSearchCulture);
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.InvariantCultureComparison);
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.PlatformDependentPathComparison);
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.QueryableDegradedToEnumerable);
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.SortWithoutOrdering);
    }

    /// <summary>
    ///     ⚠ Degenerate and hostile shapes, asserting only that nothing threw.
    /// </summary>
    /// <remarks>
    ///     These rules read type arguments positionally, walk base types and index parameter lists, so
    ///     the crash to look for is an assumption about arity: a generic method reached with the wrong
    ///     number of type arguments, a member on an unresolved type, a comparison whose receiver is an
    ///     error type. The compilation deliberately contains unbound names, so this is the one test
    ///     here that does not require its source to compile clean.
    /// </remarks>
    [Fact]
    public void DegenerateShapes_DoNotCrashAnAnalyzer() {
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              using System.Linq;

                              class Degenerate {
                                  void A(Unknown u) => u.Sort();
                                  void B(dynamic d) => d.IndexOf("x");
                                  int C(string s) => s.IndexOf();
                                  void D(List<Missing> m) => m.Sort();
                                  bool E(string a) => a.Equals(a, (StringComparison)99);
                                  IEnumerable<T> F<T>(IQueryable<T> q) => q.Where(default);
                                  void G() => Array.Sort<int>();
                                  bool H(string a, string b) => a.StartsWith(b, StringComparison.InvariantCulture, 0);
                              }
                              """;

        var diagnostics = Run(source, "Degenerate.cs");
        NoCrash(diagnostics);
    }
}
