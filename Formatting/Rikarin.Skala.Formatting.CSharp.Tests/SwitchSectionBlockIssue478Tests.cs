using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A block that is one statement among several in a switch section (issue #478, SK-DIV-0115): it sits
///     on the label's column, its contents one level in, and the statements after it back on the
///     section's level.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration and with <c>csharp_new_line_before_open_brace = all</c>,
///     <c>skala_indent_switch_labels = false</c> and <c>skala_indent_break_from_case = false</c> flipped
///     one at a time.
/// </remarks>
public sealed class SwitchSectionBlockIssue478Tests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Source = """
                          class S {
                              void M(int o) {
                                  switch (o)
                                  {
                                  case 1: { M(o); } break;
                                  case 2: M(o); { M(o); } break;
                                  case 3: { M(o); }
                                  case 4:
                                  {
                                      M(o);
                                  }
                                  break;
                                  case 5:
                                      {
                                          M(o);
                                          M(o);
                                      }
                                  case 6: { M(o); M(o); } break;
                                  case 7: { M(o); } { M(o); } break;
                                  }
                              }
                          }
                          """;

    const string Export = """
                          class S {
                              void M(int o) {
                                  switch (o) {
                                      case 1: {
                                          M(o);
                                      }
                                          break;
                                      case 2:
                                          M(o);
                                      {
                                          M(o);
                                      }
                                          break;
                                      case 3: {
                                          M(o);
                                      }
                                      case 4: {
                                          M(o);
                                      }
                                          break;
                                      case 5: {
                                          M(o);
                                          M(o);
                                      }
                                      case 6: {
                                          M(o);
                                          M(o);
                                      }
                                          break;
                                      case 7: {
                                          M(o);
                                      }
                                      {
                                          M(o);
                                      }
                                          break;
                                  }
                              }
                          }
                          """;

    const string BraceOnItsOwnLine = """
                                     class S
                                     {
                                         void M(int o)
                                         {
                                             switch (o)
                                             {
                                                 case 1:
                                                 {
                                                     M(o);
                                                 }
                                                     break;
                                                 case 2:
                                                     M(o);
                                                 {
                                                     M(o);
                                                 }
                                                     break;
                                                 case 3:
                                                 {
                                                     M(o);
                                                 }
                                                 case 4:
                                                 {
                                                     M(o);
                                                 }
                                                     break;
                                                 case 5:
                                                 {
                                                     M(o);
                                                     M(o);
                                                 }
                                                 case 6:
                                                 {
                                                     M(o);
                                                     M(o);
                                                 }
                                                     break;
                                                 case 7:
                                                 {
                                                     M(o);
                                                 }
                                                 {
                                                     M(o);
                                                 }
                                                     break;
                                             }
                                         }
                                     }
                                     """;

    const string LabelsNotIndented = """
                                     class S {
                                         void M(int o) {
                                             switch (o) {
                                             case 1: {
                                                 M(o);
                                             }
                                                 break;
                                             case 2:
                                                 M(o);
                                             {
                                                 M(o);
                                             }
                                                 break;
                                             case 3: {
                                                 M(o);
                                             }
                                             case 4: {
                                                 M(o);
                                             }
                                                 break;
                                             case 5: {
                                                 M(o);
                                                 M(o);
                                             }
                                             case 6: {
                                                 M(o);
                                                 M(o);
                                             }
                                                 break;
                                             case 7: {
                                                 M(o);
                                             }
                                             {
                                                 M(o);
                                             }
                                                 break;
                                             }
                                         }
                                     }
                                     """;

    const string BreakNotIndented = """
                                    class S {
                                        void M(int o) {
                                            switch (o) {
                                                case 1: {
                                                    M(o);
                                                }
                                                break;
                                                case 2:
                                                    M(o);
                                                {
                                                    M(o);
                                                }
                                                break;
                                                case 3: {
                                                    M(o);
                                                }
                                                case 4: {
                                                    M(o);
                                                }
                                                break;
                                                case 5: {
                                                    M(o);
                                                    M(o);
                                                }
                                                case 6: {
                                                    M(o);
                                                    M(o);
                                                }
                                                break;
                                                case 7: {
                                                    M(o);
                                                }
                                                {
                                                    M(o);
                                                }
                                                break;
                                            }
                                        }
                                    }
                                    """;

    public static TheoryData<string, string, string> Cases =>
        new() {
            { "skala_indent_switch_labels", "true", Export },
            { "csharp_new_line_before_open_brace", "all", BraceOnItsOwnLine },
            { "skala_indent_switch_labels", "false", LabelsNotIndented },
            { "skala_indent_break_from_case", "false", BreakNotIndented }
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ABlockAmongASectionsStatements_SitsOnTheLabelsColumn(string key, string value, string expected) {
        var formatted = FormatWith(Source, (key, value));
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted, (key, value)));
    }
}
