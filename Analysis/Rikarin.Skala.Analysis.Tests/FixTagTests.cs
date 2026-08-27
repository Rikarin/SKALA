using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     <c>skala fix</c> reports inside a <c>@formatter:off</c> region and rewrites nothing there.
/// </summary>
/// <remarks>
///     ⚠ The decision, and it is a decision rather than an omission. <c>@formatter:off</c> says "do not
///     rewrite this", not "do not look at this": a finding inside a region is still a true finding, and
///     swallowing it would make the tag a fifth suppression mechanism beside doc 09's four — one with no
///     audit, no <c>--no-new-suppressions</c> and no way to count. So <c>check</c> keeps naming the line
///     and <c>fix</c> declines to touch it. docs/plan/09 § "The four suppression mechanisms".
///     <para>
///         ⚠ <c>FixCommand.ApplyToFile</c> splices a rule's edits into raw text with no tree involved, which
///         is why the document builder's own check could never have covered it.
///     </para>
/// </remarks>
public sealed class FixTagTests {
    const string Source = """
                          using System;

                          namespace Scratch;

                          public sealed class Thrower {
                              // @formatter:off
                              public void Inside() {
                                  try { Console.WriteLine(); } catch (Exception ex) { throw ex; }
                              }
                              // @formatter:on

                              public void Outside() {
                                  try { Console.WriteLine(); } catch (Exception ex) { throw ex; }
                              }
                          }
                          """;

    [Fact]
    public void AFixInsideTheRegion_IsNotApplied_AndOneOutsideIs() {
        using var scratch = new Scratch();
        var path = scratch.Write("Thrower.cs", Source);
        ConfigurationCache.Clear();

        FixCommand.Run(
            new FixRequest { RepositoryRoot = scratch.Root, Paths = [scratch.Root] },
            TestContext.Current.CancellationToken
        );

        var after = File.ReadAllText(path);
        Assert.Contains(
            """
                // @formatter:off
                public void Inside() {
                    try { Console.WriteLine(); } catch (Exception ex) { throw ex; }
                }
                // @formatter:on
            """,
            after,
            StringComparison.Ordinal
        );

        // The identical statement outside the region is fixed, so this is about the tag rather than
        // about the rule declining to fire.
        Assert.Contains("throw;", after, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Report, never rewrite: the finding inside the region is still reported, by name and line.
    /// </summary>
    [Fact]
    public void TheFindingInsideTheRegion_IsStillReported() {
        using var scratch = new Scratch();
        scratch.Write("Thrower.cs", Source);
        ConfigurationCache.Clear();

        var (_, report) = CheckCommand.Run(
            new CheckRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Loose,
                Output = string.Empty,
                NoCache = true
            },
            TestContext.Current.CancellationToken
        );

        // Two of them: one inside the tags and one outside. Neither is suppressed.
        Assert.Equal(
            2,
            report.Findings.Count(finding => string.Equals(finding.RuleId, "SK2015", StringComparison.Ordinal))
        );
    }
}
