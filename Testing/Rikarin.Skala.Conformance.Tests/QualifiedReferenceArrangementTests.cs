using Rikarin.Skala.Formatting.CSharp.Arrangement;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     <c>SK0219</c> (#460): a namespace qualifier the reference does not need is dropped, under
///     <c>skala_prefer_qualified_reference = false</c>.
/// </summary>
/// <remarks>
///     ⚠ Every row is the oracle's answer: <c>jb cleanupcode</c> 2025.2.6 under <c>SkalaCleanup</c>, asked
///     on the probes SK-DIV-0073 records. The should-not-fire set is larger than the firing one on
///     purpose — the rule's whole risk is a short name that binds to something else.
/// </remarks>
public sealed class QualifiedReferenceArrangementTests {
    const string Firing = """
                          using System;
                          using System.Collections.Generic;
                          using System.Diagnostics;
                          using System.Text;

                          namespace P.Sub {
                              public class Thing { }
                          }

                          namespace P.Sub.Inner {
                              [System.Obsolete("x")]
                              public class Shortened : System.IDisposable {
                                  System.Text.StringBuilder _builder = new System.Text.StringBuilder();
                                  System.Collections.Generic.List<int>.Enumerator _enumerator;
                                  P.Sub.Thing? _thing;
                                  global::P.Sub.Thing? _rooted;

                                  public void Dispose() { }

                                  [global::System.ComponentModel.Description("a")]
                                  public string M<T>(object value) where T : System.IComparable<T> {
                                      try {
                                          if (value is System.Text.StringBuilder builder) {
                                              return builder.ToString();
                                          }

                                          global::System.Console.WriteLine(System.Diagnostics.Stopwatch.StartNew());
                                          return typeof(System.Text.StringBuilder).Name
                                              + nameof(System.Text.StringBuilder)
                                              + _builder + _enumerator.Current + _thing + _rooted;
                                      } catch (System.InvalidOperationException) {
                                          return "";
                                      }
                                  }
                              }
                          }
                          """;

    /// <remarks>
    ///     ⚠ The <c>global::</c>-only row is on <c>System.ComponentModel</c>, not on the oracle's own
    ///     <c>System.Diagnostics.CodeAnalysis</c>: this harness's reference set carries
    ///     Microsoft.Diagnostics.Tracing.TraceEvent, which declares a top-level <c>Diagnostics</c>
    ///     namespace, and from here that segment then names something else — so the rule keeps the
    ///     <c>global::</c>, correctly, for a reason the oracle's project does not have.
    /// </remarks>
    [Theory]
    [InlineData("[Obsolete(\"x\")]")]
    [InlineData("public class Shortened : IDisposable {")]
    [InlineData("StringBuilder _builder = new StringBuilder();")]
    [InlineData("List<int>.Enumerator _enumerator;")]
    [InlineData("Thing? _thing;")]
    [InlineData("Thing? _rooted;")]
    [InlineData("[System.ComponentModel.Description(\"a\")]")]
    [InlineData("where T : IComparable<T>")]
    [InlineData("if (value is StringBuilder builder)")]
    [InlineData("Console.WriteLine(Stopwatch.StartNew());")]
    [InlineData("return typeof(StringBuilder).Name")]
    [InlineData("+ nameof(StringBuilder)")]
    [InlineData("catch (InvalidOperationException)")]
    public void AQualifierTheNameDoesNotNeed_IsDropped(string expected) {
        var arranged = ArrangementRuleTests.Declined(
            ArrangementRuleTests.Attempt(Firing, ArrangeIds.QualifiedReference)
        );
        Assert.Contains(expected, arranged, StringComparison.Ordinal);
    }

    const string Holding = """
                           using System;
                           using System.Text;
                           using System.Threading;
                           using System.Timers;
                           using Abe = System.Collections.Generic.List<int>;

                           namespace P.Serialization {
                               public class Local { }
                           }

                           namespace Q {
                               public class Outer { public class Inner { } }
                           }

                           namespace P.Shadow {
                               public class StringBuilder { }

                               /// <summary>See <see cref="System.Text.StringBuilder" />.</summary>
                               public class Holding : System.IDisposable {
                                   System.Text.StringBuilder _framework = new System.Text.StringBuilder();
                                   System.Text.RegularExpressions.Regex? _pattern;
                                   System.Threading.Timer? _threading;
                                   System.Timers.Timer? _timers;
                                   System.Collections.Generic.List<int>? _list;
                                   global::System.Runtime.Serialization.SerializationInfo? _info;
                                   Q.Outer.Inner? _nested;
                                   System./* why */Text.Encoding? _commented;

                                   void System.IDisposable.Dispose() { }

                                   public string M(object Console) {
                                       global::System.Console.WriteLine(Console);
                                       return "" + _framework + _pattern + _threading + _timers + _list + _info + _nested + _commented + new Abe();
                                   }
                               }
                           }
                           """;

    /// <summary>
    ///     Each of these keeps its qualifier: a shadowed name (<c>StringBuilder</c>), a namespace nothing
    ///     imports (never <c>RegularExpressions.Regex</c> either), an ambiguous one (<c>Timer</c>, both
    ///     namespaces imported), one the oracle would not reach through an alias (<c>Abe</c>), a
    ///     <c>cref</c>, an explicit interface specifier, a comment inside the name, a receiver that a
    ///     parameter named <c>Console</c> would capture (only its <c>global::</c> goes), a nested type in a
    ///     namespace nothing imports, and — ⚠ the
    ///     oracle's own refinement — a <c>global::</c> in a type position whose later segment
    ///     (<c>Serialization</c>) names something else from here.
    /// </summary>
    [Theory]
    [InlineData("System.Text.StringBuilder _framework = new System.Text.StringBuilder();")]
    [InlineData("System.Text.RegularExpressions.Regex? _pattern;")]
    [InlineData("System.Threading.Timer? _threading;")]
    [InlineData("System.Timers.Timer? _timers;")]
    [InlineData("System.Collections.Generic.List<int>? _list;")]
    [InlineData("global::System.Runtime.Serialization.SerializationInfo? _info;")]
    [InlineData("Q.Outer.Inner? _nested;")]
    [InlineData("System./* why */Text.Encoding? _commented;")]
    [InlineData("void System.IDisposable.Dispose()")]
    [InlineData("<see cref=\"System.Text.StringBuilder\" />")]
    [InlineData(" System.Console.WriteLine(Console);")]
    [InlineData("using Abe = System.Collections.Generic.List<int>;")]
    [InlineData("namespace P.Shadow {")]
    [InlineData("public class Holding : IDisposable {")]
    public void AQualifierTheNameNeeds_IsKept(string kept) {
        var arranged = ArrangementRuleTests.Declined(
            ArrangementRuleTests.Attempt(Holding, ArrangeIds.QualifiedReference)
        );
        Assert.Contains(kept, arranged, StringComparison.Ordinal);
    }

    /// <summary>At <c>true</c> the rule does nothing: the qualifying direction is not performed.</summary>
    [Fact]
    public void AtTrue_NothingIsShortened() {
        var arranged = ArrangementRuleTests.Declined(
            ArrangementRuleTests.Attempt(
                Firing,
                ArrangeIds.QualifiedReference,
                overrides: [new("skala_prefer_qualified_reference", "true")]
            )
        );
        Assert.Contains("System.Text.StringBuilder _builder = new System.Text.StringBuilder();", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The ordering hazard: a using that only the qualified spelling left unused is what the shortened
    ///     name binds through, and the oracle keeps it. Removing it in the same pass is <c>CS0246</c> and a
    ///     reverted document — which is what this asserts never happens.
    /// </summary>
    [Fact]
    public void AUsingTheShorteningNeeds_SurvivesTheSamePass() {
        const string source = """
                              using System.Text;
                              using System.Text.RegularExpressions;

                              namespace P;

                              public class OnlyQualified {
                                  System.Text.StringBuilder _builder = new System.Text.StringBuilder();
                                  System.Text.RegularExpressions.Regex? _pattern;

                                  public string M() => "" + _builder + _pattern;
                              }
                              """;

        var arranged = ArrangementRuleTests.Declined(ArrangementRuleTests.Attempt(source, removeUnused: true));
        Assert.Contains("using System.Text;", arranged, StringComparison.Ordinal);
        Assert.Contains("using System.Text.RegularExpressions;", arranged, StringComparison.Ordinal);
        Assert.Contains("StringBuilder _builder = new", arranged, StringComparison.Ordinal);
        Assert.Contains("Regex? _pattern;", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Text.StringBuilder", arranged, StringComparison.Ordinal);
    }

    /// <summary>And a using nothing needs, before or after, still goes.</summary>
    [Fact]
    public void AUsingNothingNeeds_IsStillRemoved() {
        const string source = """
                              using System.IO;
                              using System.Text;

                              namespace P;

                              public class OnlyQualified {
                                  System.Text.StringBuilder _builder = new System.Text.StringBuilder();

                                  public string M() => "" + _builder;
                              }
                              """;

        var arranged = ArrangementRuleTests.Declined(ArrangementRuleTests.Attempt(source, removeUnused: true));
        Assert.DoesNotContain("using System.IO;", arranged, StringComparison.Ordinal);
        Assert.Contains("using System.Text;", arranged, StringComparison.Ordinal);
    }
}
