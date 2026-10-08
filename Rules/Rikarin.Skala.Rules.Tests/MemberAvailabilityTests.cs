using Microsoft.CodeAnalysis;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     #511: a rule whose fix writes a <em>member</em> one target framework lacks withholds the files that
///     framework compiles, exactly as one whose fix writes a missing <em>type</em> does (#343, #351).
/// </summary>
/// <remarks>
///     ⚠ <b>The class #351's guard could not see.</b> <c>System.Enum</c> and
///     <c>Dictionary&lt;K, V&gt;</c> exist on every moniker; <c>Enum.GetValues&lt;T&gt;()</c> (.NET 5) and
///     <c>Dictionary&lt;K, V&gt;.TryAdd</c> (netstandard2.1) do not. On a <c>netstandard2.1;net10.0</c>
///     project <c>SK1035</c> reported from the <c>net10.0</c> leg and <c>skala fix</c> reverted the rewrite
///     with <c>CS0308</c> — the finding was still reported on a line no fix could ever land on.
///     <para>
///         The siblings are real reference packs, not stand-ins: the rows asserting the sibling lacks the
///         member are what keep a negative from passing because the sibling had no references at all.
///     </para>
/// </remarks>
public sealed class MemberAvailabilityTests {
    const string EnumSource = """
                              using System;

                              public enum Colour { Red, Green }

                              public static class Probe {
                                  public static int Count() {
                                      var count = 0;
                                      foreach (Colour colour in Enum.GetValues(typeof(Colour))) {
                                          count++;
                                      }

                                      return count;
                                  }
                              }
                              """;

    const string TryAddSource = """
                                using System.Collections.Generic;

                                public static class Probe {
                                    public static void Put(Dictionary<string, int> map, string key, int value) {
                                        if (!map.ContainsKey(key)) {
                                            map[key] = value;
                                        }
                                    }
                                }
                                """;

    const string TryGetValueSource = """
                                     using System.Collections.Generic;

                                     public static class Probe {
                                         public static int Get(Dictionary<string, int> map, string key) {
                                             if (map.ContainsKey(key)) {
                                                 var value = map[key];
                                                 return value;
                                             }

                                             return 0;
                                         }
                                     }
                                     """;

    const string CopySource = """
                              using System.IO;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public static class Probe {
                                  public static async Task Copy(Stream from, Stream to, CancellationToken token) {
                                      await from.CopyToAsync(to);
                                  }
                              }
                              """;

    const string UncancellableSource = """
                                       using System.IO;
                                       using System.Threading.Tasks;

                                       public static class Probe {
                                           public static async Task Copy(Stream from, Stream to) {
                                               await from.CopyToAsync(to);
                                           }
                                       }
                                       """;

    const string Dictionary = "System.Collections.Generic.Dictionary`2";

    [Theory]
    [InlineData(null, true)]
    [InlineData("net10.0", true)]
    [InlineData("net9.0", true)]
    [InlineData("netstandard2.1", false)]
    [InlineData("netstandard2.0", false)]
    public async Task EnumGetValues_IsWithheldWhereASiblingLacksTheGenericOverload(string? sibling, bool fires) {
        var found = await Run(EnumSource, sibling, "System.Enum", "GetValues", static m => m.Arity == 1, fires);
        Assert.Equal(fires, found.Any(static d => d.Id == RuleIds.GenericEnumGetvalues));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("net10.0", true)]
    [InlineData("netstandard2.1", true)]
    [InlineData("netstandard2.0", false)]
    public async Task TryAdd_IsWithheldWhereASiblingLacksIt(string? sibling, bool fires) {
        var found = await Run(TryAddSource, sibling, Dictionary, "TryAdd", static _ => true, fires);
        Assert.Equal(fires, found.Any(static d => d.Id == RuleIds.DictionaryDoubleLookup));
    }

    /// <summary>
    ///     ⚠ Only the <c>TryAdd</c> shape is withheld: <c>TryGetValue</c> is on every moniker, so the same
    ///     netstandard2.0 sibling must not silence the rule's other half.
    /// </summary>
    [Fact]
    public async Task TryGetValue_StillFiresBesideANetstandard20Sibling() {
        var found = await Run(TryGetValueSource, "netstandard2.0", Dictionary, "TryGetValue", static _ => true, true);
        Assert.Contains(found, static d => d.Id == RuleIds.DictionaryDoubleLookup);
    }

    /// <summary>
    ///     <c>SK3004</c>: the token goes to <c>CopyToAsync(Stream, CancellationToken)</c>, an overload
    ///     netstandard2.0 does not have — a per-call question, asked of the sibling's own binding.
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("net10.0", true)]
    [InlineData("netstandard2.1", true)]
    [InlineData("netstandard2.0", false)]
    public async Task TokenForwarding_IsWithheldWhereASiblingLacksTheOverload(string? sibling, bool fires) {
        var found = await Run(CopySource, sibling, "System.IO.Stream", "CopyToAsync", TakesStreamAndToken, fires);
        Assert.Equal(fires, found.Any(static d => d.Id == RuleIds.CancellationTokenNotForwarded));
    }

    /// <summary>
    ///     <c>SK3051</c> forwards its new parameter to the same calls, so a call only one leg can forward
    ///     to is not evidence; with nothing else to forward to, the finding goes.
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("netstandard2.1", true)]
    [InlineData("netstandard2.0", false)]
    public async Task AddingAToken_IsWithheldWhereASiblingCouldNotForwardIt(string? sibling, bool fires) {
        var found = await Run(
            UncancellableSource,
            sibling,
            "System.IO.Stream",
            "CopyToAsync",
            TakesStreamAndToken,
            fires
        );
        Assert.Equal(fires, found.Any(static d => d.Id == RuleIds.AsyncMethodWithoutCancellation));
    }

    static bool TakesStreamAndToken(IMethodSymbol method) =>
        method.Parameters.Length == 2 && method.Parameters[1].Type.Name == "CancellationToken";

    static async Task<IReadOnlyList<Diagnostic>> Run(
        string source,
        string? sibling,
        string type,
        string member,
        Func<IMethodSymbol, bool> shape,
        bool siblingHasIt
    ) {
        var current = RuleFixtures.Compile(Directive("net10.0") + source, "shared.cs");
        AssertCompiles(current);
        if (sibling is null) {
            var alone = await SiblingProvider.Analyze(current);
            Assert.DoesNotContain(alone, static d => d.Id == "AD0001");
            return alone;
        }

        var other = RuleFixtures.Compile(Directive(sibling) + source, "shared.cs");
        AssertCompiles(other);

        // ⚠ The instrument, before the claim: the sibling's answer must be the framework's, not an
        // empty reference set's. A sibling with nothing referenced lacks every member for the wrong
        // reason and would make each negative row pass over an unguarded rule.
        var declared = other.GetTypeByMetadataName(type);
        Assert.NotNull(declared);
        Assert.Equal(siblingHasIt, declared.GetMembers(member).OfType<IMethodSymbol>().Any(shape));

        var found = await SiblingProvider.Analyze(current, other);
        Assert.DoesNotContain(found, static d => d.Id == "AD0001");
        return found;
    }

    static void AssertCompiles(Compilation compilation) =>
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d => d.Severity == DiagnosticSeverity.Error)
        );

    /// <summary>
    ///     ⚠ Padded to one width, because a per-site guard finds the sibling's node by span: a real
    ///     multi-targeted project compiles one file, byte for byte, and a directive one character longer
    ///     on one leg would shift every span after it and make the sibling look as if it lacked the node.
    /// </summary>
    static string Directive(string framework) =>
        "// fixture-option: TargetFramework = " + framework.PadRight(16) + "\n";
}
