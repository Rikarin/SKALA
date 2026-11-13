using Rikarin.Skala.Formatting.CSharp.Arrangement;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     ⚠ #639: a rewrite that makes a using necessary, and the removal of that using, in one pass.
/// </summary>
/// <remarks>
///     ⚠ The issue named <c>SK0219</c> as the shortening; it was <c>SK0215</c>. On the issue's file,
///     <c>arrange --exclude SK0219</c> still produced <c>SK9098</c> and <c>--exclude SK0215</c> did not:
///     <see cref="StaticQualifierRule" /> drops a static receiver's namespace before
///     <see cref="QualifiedReferenceRule" /> ever sees it, and only the latter told <see cref="UsingsRule" />
///     which directives it had come to rely on (#460). The fix is not a second report from a second rule:
///     the usings rule re-asks the rewritten tree (<see cref="ArrangementRule.Rebase" />), so any rule that
///     makes a using necessary — these two, the adding direction of <c>SK0215</c>, <c>SK0211</c>'s
///     <c>int</c> ⇒ <c>Int32</c>, the next one — is
///     covered without having to remember to say so.
///     <para>
///         Every probe runs the whole catalogue with the removable set its input really has, which is what
///         <c>NamespaceQualifierArrangementTests</c> never did: it filters to <c>SK0215</c> and passes no
///         removable set, so the pair could not meet there.
///     </para>
/// </remarks>
public sealed class UsingsCompositionArrangementTests {
    /// <summary>The issue's two files as one: the enum's namespace and the user's.</summary>
    static string Repro(string usings, string expression = "(int)Repro.Severity.ImportSeverity.Error") =>
        usings
        + """


          namespace Repro.Severity {
              public enum ImportSeverity { Info, Warning, Error }
          }

          namespace Repro {
              public static class Qualified {
                  public static bool IsError(int value) => value ==
          """
        + " "
        + expression
        + """
          ;
              }
          }
          """;

    static string Arranged(string source, IReadOnlyList<KeyValuePair<string, string>>? overrides = null) =>
        ArrangementRuleTests.Declined(ArrangementRuleTests.Attempt(source, removeUnused: true, overrides: overrides));

    /// <summary>
    ///     The issue's repro: the using the qualified name left unused is what the shortened name binds
    ///     through, so both rewrites cannot happen — and the one a user wants is the shortening.
    /// </summary>
    [Fact]
    public void TheUsingAShortenedReceiverBindsThrough_IsKept() {
        var arranged = Arranged(Repro("using Repro.Severity;"));
        Assert.Contains("using Repro.Severity;", arranged, StringComparison.Ordinal);
        Assert.Contains("value == (int)ImportSeverity.Error;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The same through a trivial alias, which is removable when unused, and which the shortened name
    ///     binds through just the same.
    /// </summary>
    [Fact]
    public void ATrivialAliasTheShortenedReceiverBindsThrough_IsKept() {
        var arranged = Arranged(Repro("using ImportSeverity = Repro.Severity.ImportSeverity;"));
        Assert.Contains("using ImportSeverity = Repro.Severity.ImportSeverity;", arranged, StringComparison.Ordinal);
        Assert.Contains("value == (int)ImportSeverity.Error;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>using static</c> is never removed, so the hazard cannot reach it; the member is reached through
    ///     it and the directive stays.
    /// </summary>
    [Fact]
    public void AUsingStaticTheBareMemberBindsThrough_IsKept() {
        var arranged = Arranged(Repro("using static Repro.Severity.ImportSeverity;"));
        Assert.Contains("using static Repro.Severity.ImportSeverity;", arranged, StringComparison.Ordinal);
        Assert.Contains("value == (int)Error;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The other direction of <c>SK0215</c> makes a using necessary too: qualifying <c>Max</c> as
    ///     <c>Math.Max</c> binds through <c>using System;</c>, which nothing needed while the call went
    ///     through <c>using static System.Math;</c>.
    /// </summary>
    [Fact]
    public void TheUsingAnAddedQualifierBindsThrough_IsKept() {
        const string source = """
                              using System;
                              using static System.Math;

                              namespace P;

                              public static class Calculator {
                                  public static double Larger(double value) => Max(value, 1);
                              }
                              """;

        var arranged = Arranged(source, [new("skala_static_members_qualify_members", "method")]);
        Assert.Contains("using System;", arranged, StringComparison.Ordinal);
        Assert.Contains("=> Math.Max(value, 1);", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ And so does <c>SK0211</c>'s expanding direction: <c>int</c> ⇒ <c>Int32</c> binds through a
    ///     <c>using System;</c> that a file spelling only keywords never needed.
    /// </summary>
    [Fact]
    public void TheUsingAnExpandedKeywordBindsThrough_IsKept() {
        const string source = """
                              using System;

                              namespace P;

                              public static class Identity {
                                  public static int Of(int value) => value;
                              }
                              """;

        var arranged = Arranged(source, [new("dotnet_style_predefined_type_for_locals_parameters_members", "false")]);
        Assert.Contains("using System;", arranged, StringComparison.Ordinal);
        Assert.Contains("public static Int32 Of(Int32 value) => value;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Should not fire: narrowing the removable set does not keep what nothing needs, before or after
    ///     the shortening.
    /// </summary>
    [Fact]
    public void AUsingNothingNeeds_IsStillRemovedBesideTheShortening() {
        var arranged = Arranged(Repro("using System.IO;\nusing Repro.Severity;"));
        Assert.DoesNotContain("using System.IO;", arranged, StringComparison.Ordinal);
        Assert.Contains("using Repro.Severity;", arranged, StringComparison.Ordinal);
        Assert.Contains("value == (int)ImportSeverity.Error;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Should not fire: a file's own using that a <c>global using</c> duplicates is redundant whether or
    ///     not the name is shortened, and still goes — the case #460's recorder carved out by hand.
    /// </summary>
    [Fact]
    public void AUsingAGlobalUsingDuplicates_IsStillRemovedBesideTheShortening() {
        const string source = """
                              global using System.Text;
                              using System.Text;

                              namespace P;

                              public class OnlyQualified {
                                  System.Text.StringBuilder _builder = new System.Text.StringBuilder();

                                  public string M() => "" + _builder;
                              }
                              """;

        var arranged = Arranged(source);
        Assert.Contains("global using System.Text;", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("\nusing System.Text;", arranged, StringComparison.Ordinal);
        Assert.Contains("StringBuilder _builder = new", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Text.StringBuilder", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Should not fire: with no shortening in the pass, an unused using goes exactly as it did.
    /// </summary>
    [Fact]
    public void AnUnusedUsing_IsStillRemovedWhenNothingIsShortened() {
        var arranged = Arranged(Repro("using Repro.Severity;\nusing System.Text;", "(int)ImportSeverity.Error"));
        Assert.DoesNotContain("using System.Text;", arranged, StringComparison.Ordinal);
        Assert.Contains("using Repro.Severity;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Should not fire: a using some other line already needs still lets the qualified one shorten.
    /// </summary>
    [Fact]
    public void AUsingUsedElsewhere_StillShortensTheQualifiedName() {
        var arranged = Arranged(
            Repro(
                "using Repro.Severity;",
                "(int)Repro.Severity.ImportSeverity.Error && default(ImportSeverity) == ImportSeverity.Info"
            )
        );
        Assert.Contains("using Repro.Severity;", arranged, StringComparison.Ordinal);
        Assert.Contains("value == (int)ImportSeverity.Error && default", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("Repro.Severity.ImportSeverity", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Should not fire: with no using at all there is nothing for the short name to bind through, and
    ///     the qualified name stays whole.
    /// </summary>
    [Fact]
    public void WithNoUsing_TheQualifiedNameStays() {
        var arranged = Arranged(Repro("using System;"));
        Assert.Contains("(int)Repro.Severity.ImportSeverity.Error", arranged, StringComparison.Ordinal);
    }
}
