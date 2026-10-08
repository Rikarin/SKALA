using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A conditional chain the author broke before any <c>?</c> is chopped at both signs of every member
///     (issue #548).
/// </summary>
/// <remarks>
///     ⚠ The expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c> under
///     the repository's configuration. ⚠ The issue's "in a switch arm" is where it was noticed, not what
///     decides it: the shape in Skala's own <c>SpaceRules.cs</c> is a <c>return</c>, and the arm and the
///     statement come back alike. <c>B</c> and <c>I</c>, broken only at a <c>:</c>, do not step; <c>G</c>
///     nests on the true side and is a single conditional rather than a chain member.
/// </remarks>
public sealed class TernaryStaircaseIssue548Tests {
    const string Source = """
                          class C548 {
                              int A(bool a, bool b) {
                                  return a
                                      ? 1
                                      : b ? 2 : 3;
                              }

                              int B(bool a, bool b) {
                                  return a ? 1
                                      : b ? 2 : 3;
                              }

                              int D(bool a, bool b, bool c) {
                                  return a
                                      ? 1
                                      : b ? 2 : c ? 3 : 4;
                              }

                              int E(bool a, bool b) {
                                  return a ? 1 : b
                                      ? 2 : 3;
                              }

                              int F(bool a, bool b, int k) => k switch {
                                  1 => a
                                      ? 1
                                      : b ? 2 : 3,
                                  _ => a ? 1 : b ? 2 : 3
                              };

                              int G(bool a, bool b) {
                                  return a
                                      ? b ? 2 : 3
                                      : 1;
                              }

                              int H(bool a, bool b) {
                                  return a
                                      ? 1 : b ? 2 : 3;
                              }

                              int I(bool a, bool b) {
                                  return a ? 1 : b ? 2
                                      : 3;
                              }
                          }
                          """;

    const string Oracle = """
                          class C548 {
                              int A(bool a, bool b) {
                                  return a
                                      ? 1
                                      : b
                                          ? 2
                                          : 3;
                              }

                              int B(bool a, bool b) {
                                  return a ? 1
                                      : b ? 2 : 3;
                              }

                              int D(bool a, bool b, bool c) {
                                  return a
                                      ? 1
                                      : b
                                          ? 2
                                          : c
                                              ? 3
                                              : 4;
                              }

                              int E(bool a, bool b) {
                                  return a
                                      ? 1
                                      : b
                                          ? 2
                                          : 3;
                              }

                              int F(bool a, bool b, int k) =>
                                  k switch {
                                      1 => a
                                          ? 1
                                          : b
                                              ? 2
                                              : 3,
                                      _ => a ? 1 : b ? 2 : 3
                                  };

                              int G(bool a, bool b) {
                                  return a
                                      ? b ? 2 : 3
                                      : 1;
                              }

                              int H(bool a, bool b) {
                                  return a
                                      ? 1
                                      : b
                                          ? 2
                                          : 3;
                              }

                              int I(bool a, bool b) {
                                  return a ? 1
                                      : b ? 2
                                      : 3;
                              }
                          }
                          """;

    [Fact]
    public void AChainBrokenBeforeAQuestion_StepsEveryMember() {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );
        var formatted = CSharpFormatter.Format("Test.cs", SourceText.From(Source), options).Formatted;
        Assert.Equal(Oracle + "\n", formatted);
        Assert.Equal(formatted, CSharpFormatter.Format("Test.cs", SourceText.From(formatted), options).Formatted);
    }
}
