using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A value continued inside a <c>for</c> or <c>fixed</c> header's item, or inside a declarator of a
///     multi-declarator list (issue #468, SK-DIV-0109, SK-DIV-0111): one level past the item's column.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration. A <c>using</c> header's declarator and a single declarator
///     outside a header are the controls, and both are unchanged.
/// </remarks>
public sealed class ContinuedListItemIssue468Tests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Headers = """
                           using System;
                           class R {
                               unsafe void M(int n, int a, int[] arr) {
                                   for (int i = 0; i < n; i +=
                                   1) { }
                                   int x = a
                                   + 1, y = 2;
                                   for (int i =
                                   0; i < n; i++) { }
                                   using (var d =
                                   default(IDisposable)) { }
                                   fixed (int* p =
                                   &arr[0]) { }
                                   int z = a
                                   + 1;
                                   int w =
                                   a + 1, v = 2;
                                   for (int i = 0, j =
                                   1; i < n; i++) { }
                                   int k;
                                   for (k = 0; k < n; k = k
                                   + 1) { }
                                   for (k = 0; k < n; k =
                                   k + 1) { }
                                   int u = 1, t = a
                                   + 1;
                                   using var e = (IDisposable)
                                   null;
                               }
                           }
                           """;

    const string HeadersOracle = """
                                 using System;

                                 class R {
                                     unsafe void M(int n, int a, int[] arr) {
                                         for (int i = 0;
                                              i < n;
                                              i +=
                                                  1) { }

                                         int x = a
                                                 + 1,
                                             y = 2;
                                         for (int i =
                                                  0;
                                              i < n;
                                              i++) { }

                                         using (var d =
                                                default(IDisposable)) { }

                                         fixed (int* p =
                                                    &arr[0]) { }

                                         int z = a
                                             + 1;
                                         int w =
                                                 a + 1,
                                             v = 2;
                                         for (int i = 0,
                                              j =
                                                  1;
                                              i < n;
                                              i++) { }

                                         int k;
                                         for (k = 0;
                                              k < n;
                                              k = k
                                                  + 1) { }

                                         for (k = 0;
                                              k < n;
                                              k =
                                                  k + 1) { }

                                         int u = 1,
                                             t = a
                                                 + 1;
                                         using var e = (IDisposable)
                                             null;
                                     }
                                 }
                                 """;

    const string Declarators = """
                               using System;
                               class R {
                                   int _x = 1
                                   + 1, _y = 2;
                                   void M(int n, int a, bool b, int c) {
                                       int x1 = F(
                                       1), y1 = 2;
                                       int x2 = F(1,
                                       2), y2 = 2;
                                       Action x3 = () => {
                                           M(n, a, b, c);
                                       }, y3 = null;
                                       int[] x4 = new[] {
                                       1 }, y4 = null;
                                       int x6 = a.CompareTo(1)
                                       .CompareTo(2), y6 = 2;
                                       int x7 = 1, y7 = a
                                       + 1, z7 = 2;
                                       for (int i = F(
                                       1); i < n; i++) { }
                                       for (int i = 0; i < n; i += F(
                                       1)) { }
                                       for (int i = 0; i < n
                                       + 1; i++) { }
                                       int x8 = a +
                                       1, y8 = 2;
                                   }
                                   int F(int a, int b = 0) => a;
                               }
                               """;

    const string DeclaratorsOracle = """
                                     using System;

                                     class R {
                                         int _x = 1
                                                 + 1,
                                             _y = 2;

                                         void M(int n, int a, bool b, int c) {
                                             int x1 = F(1), y1 = 2;
                                             int x2 = F(
                                                     1,
                                                     2
                                                 ),
                                                 y2 = 2;
                                             Action x3 = () => { M(n, a, b, c); }, y3 = null;
                                             int[] x4 = new[] { 1 }, y4 = null;
                                             int x6 = a.CompareTo(1)
                                                     .CompareTo(2),
                                                 y6 = 2;
                                             int x7 = 1,
                                                 y7 = a
                                                     + 1,
                                                 z7 = 2;
                                             for (int i = F(1); i < n; i++) { }

                                             for (int i = 0; i < n; i += F(1)) { }

                                             for (int i = 0;
                                                  i
                                                  < n
                                                  + 1;
                                                  i++) { }

                                             int x8 = a + 1, y8 = 2;
                                         }

                                         int F(int a, int b = 0) => a;
                                     }
                                     """;

    public static TheoryData<string, string> Cases =>
        new() {
            { Headers, HeadersOracle },
            { Declarators, DeclaratorsOracle }
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheContinuation_ComesBackAsTheOracleWritesIt(string source, string expected) {
        var formatted = FormatWith(source);
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
