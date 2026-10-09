using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An array initializer's collection element the author broke after its <c>[</c> keeps the break and keeps
///     its <c>[</c> beside the element before it (Nightly fuzz, case 15104748770501078810, origin
///     <c>pathological/nested-collection-in-generated-while.cs</c>).
/// </summary>
/// <remarks>
///     ⚠ The fill measured such an element by its flat draft (#444), which read the kept break after the
///     <c>[</c> as a space: 97 columns, which fitted the continuation line, so pass one moved the <c>[</c> down
///     and kept the collection broken anyway. Pass two read its own <c>]</c> line as certain and kept
///     <c>), [</c>, the oracle's answer for both inputs. A call broken after its <c>(</c> still goes back on one
///     line (the last row). Expected output is the oracle's, measured 2026-10-09 with <c>Testing ask</c>.
/// </remarks>
public sealed class CollectionBrokenAfterItsBracketNightlyTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Source = """
                          class C {
                              void M() {
                                  var a = new[] { Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), [
                          57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", SourceXYZ], (null ? "ss" : 1.5d), [] };
                                  var b = new[] { alpha, [
                          1, 2, 3], beta };
                                  var c = new[] { Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), [
                          57926, "sss", SourceXYZ], gamma };
                                  var d = new[] { Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), [57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", SourceXYZ], (null ? "ss" : 1.5d), [] };
                                  var e = new[] { Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), Compute(
                          57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", Source), gamma };
                              }
                          }
                          """;

    const string Oracle = """
                          class C {
                              void M() {
                                  var a = new[] {
                                      Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), [
                                          57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", SourceXYZ
                                      ],
                                      (null ? "ss" : 1.5d), []
                                  };
                                  var b = new[] {
                                      alpha, [
                                          1, 2, 3
                                      ],
                                      beta
                                  };
                                  var c = new[] {
                                      Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), [
                                          57926, "sss", SourceXYZ
                                      ],
                                      gamma
                                  };
                                  var d = new[] {
                                      Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"),
                                      [57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", SourceXYZ],
                                      (null ? "ss" : 1.5d), []
                                  };
                                  var e = new[] {
                                      Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"),
                                      Compute(57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", Source), gamma
                                  };
                              }
                          }
                          """;

    [Fact]
    public void ACollectionBrokenAfterItsBracket_StaysBesideThePreviousElement() {
        var formatted = FormatWith(Source);
        Assert.Equal(Oracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
