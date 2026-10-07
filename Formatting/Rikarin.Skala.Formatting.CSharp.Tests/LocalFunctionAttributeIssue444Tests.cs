using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #444, SK-DIV-0207: a local function's attribute sections are each on a line of their own
///     whatever the placement keys say; a method beside it moves with them. Skala read the method key for
///     both. Every expected string is <c>jb cleanupcode</c>'s own output under the keys named, and a
///     second pass is asserted.
/// </summary>
public sealed class LocalFunctionAttributeIssue444Tests {
    const string Source =
        """
        public class In {
            void Outer() {
                [Obsolete] void LocalOne() { }
                [Obsolete]
                void LocalTwo() { }
                [Obsolete] [Serializable] static int LocalThree() => 1;
                LocalOne();
            }

            [Obsolete] void Method() { }
            [Obsolete]
            void MethodTwo() { }
        }
        """;

    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            )
                .Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    [Fact]
    public void UnderTheExport() =>
        Agrees(
            Source,
            """
            public class In {
                void Outer() {
                    [Obsolete]
                    void LocalOne() { }

                    [Obsolete]
                    void LocalTwo() { }

                    [Obsolete]
                    [Serializable]
                    static int LocalThree() => 1;

                    LocalOne();
                }

                [Obsolete]
                void Method() { }

                [Obsolete]
                void MethodTwo() { }
            }
            """
        );

    /// <summary>The method key at <c>always</c> joins the methods and not the local functions.</summary>
    [Fact]
    public void UnderAlways_OnlyTheMethodsJoin() =>
        Agrees(
            Source,
            """
            public class In {
                void Outer() {
                    [Obsolete]
                    void LocalOne() { }

                    [Obsolete]
                    void LocalTwo() { }

                    [Obsolete]
                    [Serializable]
                    static int LocalThree() => 1;

                    LocalOne();
                }

                [Obsolete] void Method() { }
                [Obsolete] void MethodTwo() { }
            }
            """,
            ("skala_place_method_attribute_on_same_line", "always")
        );

    [Fact]
    public void UnderIfOwnerIsSingleLine_OnlyTheMethodsJoin() =>
        Agrees(
            Source,
            """
            public class In {
                void Outer() {
                    [Obsolete]
                    void LocalOne() { }

                    [Obsolete]
                    void LocalTwo() { }

                    [Obsolete]
                    [Serializable]
                    static int LocalThree() => 1;

                    LocalOne();
                }

                [Obsolete] void Method() { }
                [Obsolete] void MethodTwo() { }
            }
            """,
            ("skala_place_method_attribute_on_same_line", "if_owner_is_single_line")
        );

    /// <summary>Kept arrangement keeps the joined method and still breaks the local function.</summary>
    [Fact]
    public void WithTheArrangementKept_TheLocalFunctionStillBreaks() =>
        Agrees(
            Source,
            """
            public class In {
                void Outer() {
                    [Obsolete]
                    void LocalOne() { }

                    [Obsolete]
                    void LocalTwo() { }

                    [Obsolete]
                    [Serializable]
                    static int LocalThree() => 1;

                    LocalOne();
                }

                [Obsolete] void Method() { }

                [Obsolete]
                void MethodTwo() { }
            }
            """,
            ("skala_keep_existing_attribute_arrangement", "true")
        );
}
