using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An <c>=</c> before a call whose <c>(</c> falls past the margin breaks, on both passes (Nightly
///     <c>fuzz --seed=37583856628</c>, case 4304693669410283359).
/// </summary>
/// <remarks>
///     ⚠ Pass one kept <c>T v = Select(</c> by EqualsFloor's table, extrapolated past the 112 columns it was
///     measured to, and chopped the arguments; pass two read the chop as the author's, lost the floor and
///     broke the <c>=</c> — which is the oracle's answer for both inputs. The grid rows are the oracle's own
///     answers, measured 2026-10-09 with <c>Testing ask</c>.
/// </remarks>
public sealed class CallParenPastTheMarginNightlyTests {
    const string Long40 = "public static async ValueTask<bool> M83(ref string p84, IEnumerable<byte?> p85) "
        + "{";

    const string Long41 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });";

    const string Long42 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });";

    const string Long43 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });";

    const string Long44 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });";

    const string Long45 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });";

    const string Long46 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(x switch { 1 => 2, _ => 3 });";

    const string Long47 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });";

    const string Long48 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });";

    const string Long49 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });";

    const string Long50 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });";

    const string Long51 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });";

    const string Long52 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, () => { return 1; });";

    const string Long1 = """if (value switch { not (0 or 1 or 2) when TryGet() => @"verbatim\path", { Length"""
        + ": > 0 } => ((x, y) => {";

    const string Long2 = "{TAB}{TAB}{TAB}{TAB}{TAB}{TAB}{TAB}{TAB}IReadOnlyList<Dictionary<Dictionary<stri"
        + "ng, TimeSpan>, List<   Dictionary<string, TimeSpan>>>> v148 = Select(new char() "
        + """{ P149 = "ss", P150 = 1.5d, P151 = 41022, P152 = "sss" }, name153: 36713, name15"""
        + "4: (state is [_, .. var rest156]), name159: x160 => this);";

    const string Long3 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, "
        + "argument5, x);";

    const string Long4 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4, "
        + "argument5, argument6, argument7, argument8, argument9, argument10, argument11, a"
        + "rgument12, x);";

    const string Long5 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, "
        + "argument5, x);";

    const string Long6 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4, "
        + "argument5, argument6, argument7, argument8, argument9, argument10, argument11, a"
        + "rgument12, x);";

    const string Long7 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4,"
        + " argument5, x);";

    const string Long8 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4,"
        + " argument5, argument6, argument7, argument8, argument9, argument10, argument11, "
        + "argument12, x);";

    const string Long9 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4,"
        + " argument5, x);";

    const string Long10 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4,"
        + " argument5, argument6, argument7, argument8, argument9, argument10, argument11, "
        + "argument12, x);";

    const string Long11 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4"
        + ", argument5, x);";

    const string Long12 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument4"
        + ", argument5, argument6, argument7, argument8, argument9, argument10, argument11,"
        + " argument12, x);";

    const string Long13 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4"
        + ", argument5, x);";

    const string Long14 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument4"
        + ", argument5, argument6, argument7, argument8, argument9, argument10, argument11,"
        + " argument12, x);";

    const string Long15 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument"
        + "4, argument5, x);";

    const string Long16 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argument"
        + "4, argument5, argument6, argument7, argument8, argument9, argument10, argument11"
        + ", argument12, x);";

    const string Long17 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument"
        + "4, argument5, x);";

    const string Long18 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argument"
        + "4, argument5, argument6, argument7, argument8, argument9, argument10, argument11"
        + ", argument12, x);";

    const string Long19 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argumen"
        + "t4, argument5, x);";

    const string Long20 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argumen"
        + "t4, argument5, argument6, argument7, argument8, argument9, argument10, argument1"
        + "1, argument12, x);";

    const string Long21 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argumen"
        + "t4, argument5, x);";

    const string Long22 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argumen"
        + "t4, argument5, argument6, argument7, argument8, argument9, argument10, argument1"
        + "1, argument12, x);";

    const string Long23 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argume"
        + "nt4, argument5, x);";

    const string Long24 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argume"
        + "nt4, argument5, argument6, argument7, argument8, argument9, argument10, argument"
        + "11, argument12, x);";

    const string Long25 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argume"
        + "nt4, argument5, x);";

    const string Long26 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argume"
        + "nt4, argument5, argument6, argument7, argument8, argument9, argument10, argument"
        + "11, argument12, x);";

    const string Long27 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argum"
        + "ent4, argument5, x);";

    const string Long28 = "Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"
        + "yyyyyyyyyyyyyyyy v148 = Select(argument0, argument1, argument2, argument3, argum"
        + "ent4, argument5, argument6, argument7, argument8, argument9, argument10, argumen"
        + "t11, argument12, x);";

    const string Long29 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argum"
        + "ent4, argument5, x);";

    const string Long30 = "var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
        + "nnnnnnnnnnnnnnnnnnnnn = Select(argument0, argument1, argument2, argument3, argum"
        + "ent4, argument5, argument6, argument7, argument8, argument9, argument10, argumen"
        + "t11, argument12, x);";

    const string Long31 = "Select(argument0, argument1, argument2, argument3, argument4, argument5, x);";

    const string Long32 = "public sealed struct T2<T3, T4, T5> : IDisposable, IEnumerable<bool?>, IComparab"
        + "le<T2<T3, T4, T5>> where T3 : class, new() where T4 : class, new() where T5 : st"
        + "ruct {";

    const string Long33 = "Func<(CancellationToken First, Task<Guid> Second), (CancellationToken First, Tas"
        + """k<Guid> Second)> v116 = TryGet((state with  { P117 = "ss" }), x118 => Select((va"""
        + """lue is not null), (value switch { not null =>  "sss", _ => 1.0m }), [34507, 1.0m"""
        + """, true, 75570] , new[] { 20066, "sss", 65780, 1.0m, "sss", "s" }), Select(contex"""
        + "t.OrderBy.First(37062).OrderBy(true).Where(x119 => 94206)), Materialise<object, "
        + """ValueTask<Guid>>(Compute(), (async x => await @"verbatim\path")));<SP>""";

    const string Long34 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);";

    const string Long35 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);";

    const string Long36 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);";

    const string Long37 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);";

    const string Long38 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);";

    const string Long39 = "Dictionary<string, List<int>> vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv"
        + "vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Fn(a, x switch { 1 => 2, _ => 3 }, b);";

    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Minimised = $$$"""
                                public record struct T1(byte p2 = default, decimal p3 = default, long p4 = default) {
                                  protected virtual CancellationToken M9<T8>() {
                                  if (state is Nullable<CancellationToken> { P36: [_, .. var rest38] }) {
                                   {{{Long1}}}
                                 {TAB}{TAB}{TAB}{TAB}{TAB}{TAB}}), _ => (x => null) }) {
                                 {{{Long2}}}
                                   }
                                  }
                                  }
                                 }
                                """;

    static readonly string Grid = $$"""
                                    class T {
                                        void M() {
                                            if (a) {
                                                if (b) {
                                                    T{{R('y', 89)}} v148 = Select(argument0, argument1, x);
                                                    {{Long3}}
                                                    {{Long4}}
                                                    var {{R('n', 91)}} = Select(argument0, argument1, x);
                                                    {{Long5}}
                                                    {{Long6}}
                                                    T{{R('y', 90)}} v148 = Select(argument0, argument1, x);
                                                    {{Long7}}
                                                    {{Long8}}
                                                    var {{R('n', 92)}} = Select(argument0, argument1, x);
                                                    {{Long9}}
                                                    {{Long10}}
                                                    T{{R('y', 91)}} v148 = Select(argument0, argument1, x);
                                                    {{Long11}}
                                                    {{Long12}}
                                                    var {{R('n', 93)}} = Select(argument0, argument1, x);
                                                    {{Long13}}
                                                    {{Long14}}
                                                    T{{R('y', 92)}} v148 = Select(argument0, argument1, x);
                                                    {{Long15}}
                                                    {{Long16}}
                                                    var {{R('n', 94)}} = Select(argument0, argument1, x);
                                                    {{Long17}}
                                                    {{Long18}}
                                                    T{{R('y', 93)}} v148 = Select(argument0, argument1, x);
                                                    {{Long19}}
                                                    {{Long20}}
                                                    var {{R('n', 95)}} = Select(argument0, argument1, x);
                                                    {{Long21}}
                                                    {{Long22}}
                                                    T{{R('y', 94)}} v148 = Select(argument0, argument1, x);
                                                    {{Long23}}
                                                    {{Long24}}
                                                    var {{R('n', 96)}} = Select(argument0, argument1, x);
                                                    {{Long25}}
                                                    {{Long26}}
                                                    T{{R('y', 95)}} v148 = Select(argument0, argument1, x);
                                                    {{Long27}}
                                                    {{Long28}}
                                                    var {{R('n', 97)}} = Select(argument0, argument1, x);
                                                    {{Long29}}
                                                    {{Long30}}
                                                }
                                            }
                                        }
                                    }
                                    """;

    static readonly string GridOracle = $$"""
                                          class T {
                                              void M() {
                                                  if (a) {
                                                      if (b) {
                                                          T{{R('y', 89)}} v148 =
                                                              Select(argument0, argument1, x);
                                                          T{{R('y', 89)}} v148 =
                                                              {{Long31}}
                                                          T{{R('y', 89)}} v148 =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          var {{R('n', 91)}} =
                                                              Select(argument0, argument1, x);
                                                          var {{R('n', 91)}} =
                                                              {{Long31}}
                                                          var {{R('n', 91)}} =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          T{{R('y', 90)}} v148 =
                                                              Select(argument0, argument1, x);
                                                          T{{R('y', 90)}} v148 =
                                                              {{Long31}}
                                                          T{{R('y', 90)}} v148 =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          var {{R('n', 92)}} =
                                                              Select(argument0, argument1, x);
                                                          var {{R('n', 92)}} =
                                                              {{Long31}}
                                                          var {{R('n', 92)}} =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          T{{R('y', 91)}} v148 =
                                                              Select(argument0, argument1, x);
                                                          T{{R('y', 91)}} v148 =
                                                              {{Long31}}
                                                          T{{R('y', 91)}} v148 =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          var {{R('n', 93)}} =
                                                              Select(argument0, argument1, x);
                                                          var {{R('n', 93)}} =
                                                              {{Long31}}
                                                          var {{R('n', 93)}} =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          T{{R('y', 92)}} v148 =
                                                              Select(argument0, argument1, x);
                                                          T{{R('y', 92)}} v148 =
                                                              {{Long31}}
                                                          T{{R('y', 92)}} v148 =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          var {{R('n', 94)}} =
                                                              Select(argument0, argument1, x);
                                                          var {{R('n', 94)}} =
                                                              {{Long31}}
                                                          var {{R('n', 94)}} =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          T{{R('y', 93)}} v148 =
                                                              Select(argument0, argument1, x);
                                                          T{{R('y', 93)}} v148 =
                                                              {{Long31}}
                                                          T{{R('y', 93)}} v148 =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          var {{R('n', 95)}} =
                                                              Select(argument0, argument1, x);
                                                          var {{R('n', 95)}} =
                                                              {{Long31}}
                                                          var {{R('n', 95)}} =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          T{{R('y', 94)}} v148 =
                                                              Select(argument0, argument1, x);
                                                          T{{R('y', 94)}} v148 =
                                                              {{Long31}}
                                                          T{{R('y', 94)}} v148 =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          var {{R('n', 96)}} =
                                                              Select(argument0, argument1, x);
                                                          var {{R('n', 96)}} =
                                                              {{Long31}}
                                                          var {{R('n', 96)}} =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          T{{R('y', 95)}} v148 =
                                                              Select(argument0, argument1, x);
                                                          T{{R('y', 95)}} v148 =
                                                              {{Long31}}
                                                          T{{R('y', 95)}} v148 =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                          var {{R('n', 97)}} =
                                                              Select(argument0, argument1, x);
                                                          var {{R('n', 97)}} =
                                                              {{Long31}}
                                                          var {{R('n', 97)}} =
                                                              Select(
                                                                  argument0,
                                                                  argument1,
                                                                  argument2,
                                                                  argument3,
                                                                  argument4,
                                                                  argument5,
                                                                  argument6,
                                                                  argument7,
                                                                  argument8,
                                                                  argument9,
                                                                  argument10,
                                                                  argument11,
                                                                  argument12,
                                                                  x
                                                              );
                                                      }
                                                  }
                                              }
                                          }
                                          """;

    [Fact]
    public void TheMinimisedCase_IsIdempotent() {
        var first = FormatWith(Minimised.Replace("{TAB}", "\t", StringComparison.Ordinal));
        Assert.Equal(first, FormatWith(first));
        Assert.Contains("v148 =\n", first, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A typed and a <c>var</c> local, arguments of 20, 60 and 140 columns, the <c>(</c> at 121 to 127:
    ///     every one breaks the <c>=</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The rows with the <c>(</c> at 108 to 120 are left out. With 20 columns of arguments there the
    ///     oracle keeps a typed local's <c>=</c> and chops, and Skala breaks it — a stable divergence in
    ///     EqualsFloor's typed rows, not this fix's.
    /// </remarks>
    [Fact]
    public void AParenPastTheMargin_BreaksTheEquals() {
        var formatted = FormatWith(Grid);
        Assert.Equal(GridOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    // ⚠ Arguments holding a certain break: a switch expression or a block-bodied lambda has no flat width,
    // and the rule returned Flat before it read the `(` (Nightly `fuzz --seed=4242`, case
    // 7862808234978504853). `<SP>` is a trailing space, as the fuzzer left it.
    const string CertainMinimised = $$"""
                                      {{Long32}}
                                         internal class T78 : IDisposable, IEnumerable<StringBuilder> {
                                        {{Long40}}
                                         {{Long33}}
                                        }
                                        }
                                      }
                                      """;

    static readonly string CertainGrid = $$"""
                                           class C {
                                               void M() {
                                                   {{Long34}}
                                                   {{Long35}}
                                                   {{Long36}}
                                                   {{Long37}}
                                                   {{Long38}}
                                                   {{Long39}}
                                                   {{Long41}}
                                                   {{Long42}}
                                                   {{Long43}}
                                                   {{Long44}}
                                                   {{Long45}}
                                                   {{Long46}}
                                                   {{Long47}}
                                                   {{Long48}}
                                                   {{Long49}}
                                                   {{Long50}}
                                                   {{Long51}}
                                                   {{Long52}}
                                                   var {{R('v', 101)}} = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                                   var {{R('v', 102)}} = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                                   var {{R('v', 103)}} = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                                   var {{R('v', 104)}} = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                                   var {{R('v', 105)}} = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                                   var {{R('v', 106)}} = Fn(a, x switch { 1 => 2, _ => 3 }, b);
                                                   var {{R('v', 101)}} = Fn(x switch { 1 => 2, _ => 3 });
                                                   var {{R('v', 102)}} = Fn(x switch { 1 => 2, _ => 3 });
                                                   var {{R('v', 103)}} = Fn(x switch { 1 => 2, _ => 3 });
                                                   var {{R('v', 104)}} = Fn(x switch { 1 => 2, _ => 3 });
                                                   var {{R('v', 105)}} = Fn(x switch { 1 => 2, _ => 3 });
                                                   var {{R('v', 106)}} = Fn(x switch { 1 => 2, _ => 3 });
                                                   var {{R('v', 101)}} = Fn(a, () => { return 1; });
                                                   var {{R('v', 102)}} = Fn(a, () => { return 1; });
                                                   var {{R('v', 103)}} = Fn(a, () => { return 1; });
                                                   var {{R('v', 104)}} = Fn(a, () => { return 1; });
                                                   var {{R('v', 105)}} = Fn(a, () => { return 1; });
                                                   var {{R('v', 106)}} = Fn(a, () => { return 1; });
                                               }
                                           }
                                           """;

    static readonly string CertainGridOracle = $$"""
                                                 class C {
                                                     void M() {
                                                         Dictionary<string, List<int>> {{R('v', 75)}} = Fn(
                                                             a,
                                                             x switch {
                                                                 1 => 2,
                                                                 _ => 3
                                                             },
                                                             b
                                                         );
                                                         Dictionary<string, List<int>> {{R('v', 76)}} = Fn(
                                                             a,
                                                             x switch {
                                                                 1 => 2,
                                                                 _ => 3
                                                             },
                                                             b
                                                         );
                                                         Dictionary<string, List<int>> {{R('v', 77)}} =
                                                             Fn(
                                                                 a,
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 },
                                                                 b
                                                             );
                                                         Dictionary<string, List<int>> {{R('v', 78)}} =
                                                             Fn(
                                                                 a,
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 },
                                                                 b
                                                             );
                                                         Dictionary<string, List<int>> {{R('v', 79)}} =
                                                             Fn(
                                                                 a,
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 },
                                                                 b
                                                             );
                                                         Dictionary<string, List<int>> {{R('v', 80)}} =
                                                             Fn(
                                                                 a,
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 },
                                                                 b
                                                             );
                                                         Dictionary<string, List<int>> {{R('v', 75)}} = Fn(
                                                             x switch {
                                                                 1 => 2,
                                                                 _ => 3
                                                             }
                                                         );
                                                         Dictionary<string, List<int>> {{R('v', 76)}} = Fn(
                                                             x switch {
                                                                 1 => 2,
                                                                 _ => 3
                                                             }
                                                         );
                                                         Dictionary<string, List<int>> {{R('v', 77)}} =
                                                             Fn(
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 }
                                                             );
                                                         Dictionary<string, List<int>> {{R('v', 78)}} =
                                                             Fn(
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 }
                                                             );
                                                         Dictionary<string, List<int>> {{R('v', 79)}} =
                                                             Fn(
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 }
                                                             );
                                                         Dictionary<string, List<int>> {{R('v', 80)}} =
                                                             Fn(
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 }
                                                             );
                                                         Dictionary<string, List<int>> {{R('v', 75)}} =
                                                             Fn(a, () => { return 1; });
                                                         Dictionary<string, List<int>> {{R('v', 76)}} =
                                                             Fn(a, () => { return 1; });
                                                         Dictionary<string, List<int>> {{R('v', 77)}} =
                                                             Fn(a, () => { return 1; });
                                                         Dictionary<string, List<int>> {{R('v', 78)}} =
                                                             Fn(a, () => { return 1; });
                                                         Dictionary<string, List<int>> {{R('v', 79)}} =
                                                             Fn(a, () => { return 1; });
                                                         Dictionary<string, List<int>> {{R('v', 80)}} =
                                                             Fn(a, () => { return 1; });
                                                         var {{R('v', 101)}} = Fn(
                                                             a,
                                                             x switch {
                                                                 1 => 2,
                                                                 _ => 3
                                                             },
                                                             b
                                                         );
                                                         var {{R('v', 102)}} = Fn(
                                                             a,
                                                             x switch {
                                                                 1 => 2,
                                                                 _ => 3
                                                             },
                                                             b
                                                         );
                                                         var {{R('v', 103)}} =
                                                             Fn(
                                                                 a,
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 },
                                                                 b
                                                             );
                                                         var {{R('v', 104)}} =
                                                             Fn(
                                                                 a,
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 },
                                                                 b
                                                             );
                                                         var {{R('v', 105)}} =
                                                             Fn(
                                                                 a,
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 },
                                                                 b
                                                             );
                                                         var {{R('v', 106)}} =
                                                             Fn(
                                                                 a,
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 },
                                                                 b
                                                             );
                                                         var {{R('v', 101)}} = Fn(
                                                             x switch {
                                                                 1 => 2,
                                                                 _ => 3
                                                             }
                                                         );
                                                         var {{R('v', 102)}} = Fn(
                                                             x switch {
                                                                 1 => 2,
                                                                 _ => 3
                                                             }
                                                         );
                                                         var {{R('v', 103)}} =
                                                             Fn(
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 }
                                                             );
                                                         var {{R('v', 104)}} =
                                                             Fn(
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 }
                                                             );
                                                         var {{R('v', 105)}} =
                                                             Fn(
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 }
                                                             );
                                                         var {{R('v', 106)}} =
                                                             Fn(
                                                                 x switch {
                                                                     1 => 2,
                                                                     _ => 3
                                                                 }
                                                             );
                                                         var {{R('v', 101)}} =
                                                             Fn(a, () => { return 1; });
                                                         var {{R('v', 102)}} =
                                                             Fn(a, () => { return 1; });
                                                         var {{R('v', 103)}} =
                                                             Fn(a, () => { return 1; });
                                                         var {{R('v', 104)}} =
                                                             Fn(a, () => { return 1; });
                                                         var {{R('v', 105)}} =
                                                             Fn(a, () => { return 1; });
                                                         var {{R('v', 106)}} =
                                                             Fn(a, () => { return 1; });
                                                     }
                                                 }
                                                 """;

    [Fact]
    public void TheCertainBreakCase_BreaksTheEquals_AndIsIdempotent() {
        var first = FormatWith(CertainMinimised.Replace("<SP>", " ", StringComparison.Ordinal));
        Assert.Contains("v116 =\n", first, StringComparison.Ordinal);
        Assert.Equal(first, FormatWith(first));
    }

    /// <summary>
    ///     A typed and a <c>var</c> local before <c>Fn(</c> with a switch expression or a block-bodied lambda
    ///     among its arguments, the <c>(</c> at 119 to 124: the oracle keeps the <c>=</c> through 120 and breaks it
    ///     from 121, measured 2026-10-09 with <c>Testing ask</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Rows with the <c>(</c> at 125 or further are left out: there the <c>=</c> itself is past the margin
    ///     and the oracle also breaks a <c>var</c> local before its name — a stable divergence outside this rule.
    /// </remarks>
    [Fact]
    public void AParenPastTheMargin_BreaksTheEquals_OverACertainBreak() {
        var formatted = FormatWith(CertainGrid);
        Assert.Equal(CertainGridOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
