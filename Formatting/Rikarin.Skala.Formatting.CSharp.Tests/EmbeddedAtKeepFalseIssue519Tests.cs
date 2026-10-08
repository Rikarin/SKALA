using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Embedded statements at <c>skala_keep_existing_embedded_arrangement = false</c> (issue #519).
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration with the keep key off at each placement value, and with
///     <c>csharp_prefer_braces</c> off on Skala's side because the oracle's <c>ask</c> inserts no braces.
/// </remarks>
public sealed class EmbeddedAtKeepFalseIssue519Tests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [
                    new KeyValuePair<string, string>("csharp_prefer_braces", "false"),
                    .. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))
                ]
            )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string OneLine = """
                           using System.Collections.Generic;
                           class D {
                               void M1(bool b, bool c) {
                                   if (b) M(); else M();
                                   if (b) while (c) M(); else M();
                                   if (b) M();
                                   while (b) M();
                                   if (b) if (c) M();
                                   while (b) if (c) M();
                                   if (b) M(); else if (c) M(); else M();
                                   if (b) if (c) M(); else M();
                                   if (b)
                                       M();
                                   else M();
                                   if (b) M(); else { M(); }
                                   if (b) { M(); } else M();
                               }
                               void M2(bool b, bool c, List<int> xs) {
                                   foreach (var x in xs) foreach (var y in xs) M();
                                   do M(); while (b);
                                   lock (this) M();
                                   if (b) using (D2()) M();
                               }
                               void M() {
                               }
                               System.IDisposable D2() => null;
                           }
                           """;

    const string OneLineAtKeep = """
                                 using System.Collections.Generic;

                                 class D {
                                     void M1(bool b, bool c) {
                                         if (b) M();
                                         else M();
                                         if (b)
                                             while (c)
                                                 M();
                                         else M();
                                         if (b) M();
                                         while (b) M();
                                         if (b)
                                             if (c)
                                                 M();
                                         while (b)
                                             if (c)
                                                 M();
                                         if (b) M();
                                         else if (c) M();
                                         else M();
                                         if (b)
                                             if (c) M();
                                             else M();
                                         if (b)
                                             M();
                                         else M();
                                         if (b) M();
                                         else {
                                             M();
                                         }

                                         if (b) {
                                             M();
                                         } else M();
                                     }

                                     void M2(bool b, bool c, List<int> xs) {
                                         foreach (var x in xs)
                                         foreach (var y in xs)
                                             M();
                                         do M();
                                         while (b);
                                         lock (this) M();
                                         if (b)
                                             using (D2())
                                                 M();
                                     }

                                     void M() { }
                                     System.IDisposable D2() => null;
                                 }
                                 """;

    const string OneLineIfOwnerIsSingleLine = """
                                              using System.Collections.Generic;

                                              class D {
                                                  void M1(bool b, bool c) {
                                                      if (b)
                                                          M();
                                                      else
                                                          M();
                                                      if (b)
                                                          while (c)
                                                              M();
                                                      else
                                                          M();
                                                      if (b) M();
                                                      while (b) M();
                                                      if (b)
                                                          if (c)
                                                              M();
                                                      while (b)
                                                          if (c)
                                                              M();
                                                      if (b)
                                                          M();
                                                      else if (c)
                                                          M();
                                                      else
                                                          M();
                                                      if (b)
                                                          if (c)
                                                              M();
                                                          else
                                                              M();
                                                      if (b)
                                                          M();
                                                      else
                                                          M();
                                                      if (b)
                                                          M();
                                                      else {
                                                          M();
                                                      }

                                                      if (b) {
                                                          M();
                                                      } else
                                                          M();
                                                  }

                                                  void M2(bool b, bool c, List<int> xs) {
                                                      foreach (var x in xs)
                                                      foreach (var y in xs)
                                                          M();
                                                      do
                                                          M();
                                                      while (b);
                                                      lock (this) M();
                                                      if (b)
                                                          using (D2())
                                                              M();
                                                  }

                                                  void M() { }
                                                  System.IDisposable D2() => null;
                                              }
                                              """;

    const string OneLineAlways = """
                                 using System.Collections.Generic;

                                 class D {
                                     void M1(bool b, bool c) {
                                         if (b) M();
                                         else M();
                                         if (b)
                                             while (c)
                                                 M();
                                         else M();
                                         if (b) M();
                                         while (b) M();
                                         if (b)
                                             if (c)
                                                 M();
                                         while (b)
                                             if (c)
                                                 M();
                                         if (b) M();
                                         else if (c) M();
                                         else M();
                                         if (b)
                                             if (c) M();
                                             else M();
                                         if (b) M();
                                         else M();
                                         if (b) M();
                                         else {
                                             M();
                                         }

                                         if (b) {
                                             M();
                                         } else M();
                                     }

                                     void M2(bool b, bool c, List<int> xs) {
                                         foreach (var x in xs)
                                         foreach (var y in xs)
                                             M();
                                         do M();
                                         while (b);
                                         lock (this) M();
                                         if (b)
                                             using (D2())
                                                 M();
                                     }

                                     void M() { }
                                     System.IDisposable D2() => null;
                                 }
                                 """;

    const string OneLineNever = """
                                using System.Collections.Generic;

                                class D {
                                    void M1(bool b, bool c) {
                                        if (b)
                                            M();
                                        else
                                            M();
                                        if (b)
                                            while (c)
                                                M();
                                        else
                                            M();
                                        if (b)
                                            M();
                                        while (b)
                                            M();
                                        if (b)
                                            if (c)
                                                M();
                                        while (b)
                                            if (c)
                                                M();
                                        if (b)
                                            M();
                                        else if (c)
                                            M();
                                        else
                                            M();
                                        if (b)
                                            if (c)
                                                M();
                                            else
                                                M();
                                        if (b)
                                            M();
                                        else
                                            M();
                                        if (b)
                                            M();
                                        else {
                                            M();
                                        }

                                        if (b) {
                                            M();
                                        } else
                                            M();
                                    }

                                    void M2(bool b, bool c, List<int> xs) {
                                        foreach (var x in xs)
                                        foreach (var y in xs)
                                            M();
                                        do
                                            M();
                                        while (b);
                                        lock (this)
                                            M();
                                        if (b)
                                            using (D2())
                                                M();
                                    }

                                    void M() { }
                                    System.IDisposable D2() => null;
                                }
                                """;

    const string Broken = """
                          class D {
                              void M1(bool b, bool c) {
                                  if (b)
                                      if (c)
                                          M();
                                  if (b)
                                      M();
                                  else
                                      M();
                                  while (b)
                                      M();
                              }
                              void M() {
                              }
                          }
                          """;

    const string BrokenAlways = """
                                class D {
                                    void M1(bool b, bool c) {
                                        if (b)
                                            if (c)
                                                M();
                                        if (b) M();
                                        else M();
                                        while (b) M();
                                    }

                                    void M() { }
                                }
                                """;

    const string BrokenIfOwnerIsSingleLine = """
                                             class D {
                                                 void M1(bool b, bool c) {
                                                     if (b)
                                                         if (c)
                                                             M();
                                                     if (b)
                                                         M();
                                                     else
                                                         M();
                                                     while (b) M();
                                                 }

                                                 void M() { }
                                             }
                                             """;

    public static TheoryData<string, string, string> Cases =>
        new() {
            { OneLine, OneLineAtKeep, "" },
            { OneLine, OneLineIfOwnerIsSingleLine, "skala_keep_existing_embedded_arrangement=false" },
            {
                OneLine,
                OneLineAlways,
                "skala_keep_existing_embedded_arrangement=false;skala_place_simple_embedded_statement_on_same_line=always"
            },
            {
                OneLine,
                OneLineNever,
                "skala_keep_existing_embedded_arrangement=false;skala_place_simple_embedded_statement_on_same_line=never"
            },
            {
                Broken,
                BrokenAlways,
                "skala_keep_existing_embedded_arrangement=false;skala_place_simple_embedded_statement_on_same_line=always"
            },
            { Broken, BrokenIfOwnerIsSingleLine, "skala_keep_existing_embedded_arrangement=false" }
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheEmbeddedStatements_ComeBackAsTheOracleWritesThem(string source, string expected, string settings) {
        var overrides = settings.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(static s => (s.Split('=')[0], s.Split('=')[1]))
            .ToArray();
        var formatted = FormatWith(source, overrides);
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted, overrides));
    }
}
