using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #380: a two-segment chain whose receiver is itself a call — <c>SomeMethod(…).Other(…)</c> —
///     chopped <c>.Other</c>'s argument list where the oracle breaks the chain before <c>.Other</c>.
///     Every expected string is <c>jb cleanupcode</c>'s own output for the input, measured 2026-09-22
///     with <c>Testing ask</c> from flat input, and the oracle's second pass over each answer was
///     byte-identical; <c>constructs/breaks/two-segment-chain-with-a-call-root.cs</c> holds the wider
///     set as a fixture.
/// </summary>
/// <remarks>
///     ⚠ The oracle counts a chain in <em>calls</em>, not in dots, and the first call need not have a
///     dot. The proof is the key flipped rather than the width: at <c>chop_always</c> it chops
///     <c>SomeMethod(a, b)</c> / <c>.Other(c, d)</c> and leaves <c>x.Other(c, d)</c> whole, and it does
///     the same for an element access — <c>arr[0]</c> / <c>.Other(c, d)</c> — so an indexer is a call
///     too. <c>PlanChainedCalls</c> required two dots, so <c>F(…).Other(…)</c> planned no group and the
///     last argument list took the break. ⚠ What is a call at the head is the innermost call on the
///     spine and nothing else: <c>alpha.SomeMethod(a, b)[0].Other(c, d)</c> keeps <c>.SomeMethod</c>
///     with <c>alpha</c> at <c>chop_always</c>, a <c>[0]</c> behind it notwithstanding. SK-DIV-0128,
///     whose controls are the heads that are not calls — <c>new T(…)</c>, <c>(F(…))</c>, <c>x?.</c>.
///     The <c>wrap_if_long</c> fill, which diverges on every chain and not only these, is SK-DIV-0129.
/// </remarks>
public sealed class ChainWithACallRootIssue380Tests {
    const string A = "aaaaaaaaaaaaaaaaaa";
    const string B = "bbbbbbbbbbbbbbbbbbbbb";
    const string C = "ccccccccccccccc";
    const string D = "ddddddddddddddddddddddddddddddddddd";
    const string E = "eeeeeeeeeeeeeeeeeeeeee";
    const string F = "ffffffff";
    const string Root = $"SomeMethod({A}, {B}, {C})";
    const string LastCall = $".Other({D}, {E}, {F})";
    const string LongIdentifier = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

    /// <summary>The three arguments of the last call, chopped one per line at <paramref name="indent" />.</summary>
    static string Chopped(int indent) {
        var pad = new string(' ', indent);
        return $"{pad}{D},\n{pad}{E},\n{pad}{F}\n{pad[4..]}";
    }

    static string Statement(string statement) =>
        $$"""
          class T {
              void M() {
                  {{statement}}
              }
          }
          """;

    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    /// <summary>
    ///     The issue's 149-column line: the chain breaks before <c>.Other</c> and nothing is chopped.
    ///     Before the fix: <c>…).Other(</c> and three argument lines.
    /// </summary>
    [Fact]
    public void ACallHead_BreaksTheChain_NotTheLastArgumentList() =>
        Oracle.Agrees(
            Statement($"var a3 = {Root}{LastCall};"),
            Statement(
                $"""
                 var a3 = {Root}
                             {LastCall};
                 """
            )
        );

    /// <summary>
    ///     When the second segment does not fit on the continuation line either, the oracle breaks the
    ///     chain <em>and</em> chops, one level past the chain's; and a three-call chain whose last call
    ///     needs a chop of its own does the same.
    /// </summary>
    [Fact]
    public void ACallHead_WhoseLastCallStillDoesNotFit_BreaksTheChainAndChops() {
        const string WideE = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        const string WideF = "fffffffffffffffffffffffffffffffffffffffffffffff";
        const string G = "gggggggggggggggggggg";
        Oracle.Agrees(
            Statement($"var a4 = {Root}.Other({D}, {WideE}, {WideF}, {G});"),
            Statement(
                $"""
                 var a4 = {Root}
                             .Other(
                                 {D},
                                 {WideE},
                                 {WideF},
                                 {G}
                             );
                 """
            )
        );

        const string ThirdE = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        const string ThirdF = "ffffffffffffffffffffffffffffffffffff";
        Oracle.Agrees(
            Statement($"var d5 = SomeMethod({A}, {B}).Other(ccccccccccc).Third({D}, {ThirdE}, {ThirdF});"),
            Statement(
                $"""
                 var d5 = SomeMethod({A}, {B})
                             .Other(ccccccccccc)
                             .Third(
                                 {D},
                                 {ThirdE},
                                 {ThirdF}
                             );
                 """
            )
        );
    }

    /// <summary>
    ///     A generic call, a delegate invocation, and — after the call head — an element access, a
    ///     <c>?.</c> and a property run all break before the second call, as they already did after an
    ///     identifier head.
    /// </summary>
    [Fact]
    public void AGenericADelegateInvocation_AndTheLinksAfterACallHead_BreakTheSame() {
        const string Short = ".Other(ddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff)";
        Oracle.Agrees(
            Statement($"var a8 = SomeMethod<TTTTTTTTTTT>({A}, {B}, {C}).Other({D[..29]}, {E}, {F});"),
            Statement(
                $"""
                 var a8 = SomeMethod<TTTTTTTTTTT>({A}, {B}, {C})
                             .Other({D[..29]}, {E}, {F});
                 """
            )
        );

        Oracle.Agrees(
            Statement($"var c5 = handler({A})({B}, {C}){LastCall};"),
            Statement(
                $"""
                 var c5 = handler({A})({B}, {C})
                             {LastCall};
                 """
            )
        );

        Oracle.Agrees(
            Statement($"var b1 = {Root}[0]{Short};"),
            Statement(
                $"""
                 var b1 = {Root}[0]
                             {Short};
                 """
            )
        );

        Oracle.Agrees(
            Statement($"var b2 = {Root}?{Short};"),
            Statement(
                $"""
                 var b2 = {Root}
                             ?{Short};
                 """
            )
        );

        Oracle.Agrees(
            Statement($"var b3 = {Root}.Prop.Other(ddddddddddddddddddddddddddd, {E}, {F});"),
            Statement(
                $"""
                 var b3 = {Root}
                             .Prop.Other(ddddddddddddddddddddddddddd, {E}, {F});
                 """
            )
        );
    }

    /// <summary>
    ///     ⚠ An indexer is a call: <c>arr[0]</c>, <c>x.Items[0]</c> and <c>arr[0][1]</c> at the head each
    ///     make the dot after them a point, with <c>.Items</c> staying on the first line.
    /// </summary>
    [Fact]
    public void AnElementAccessHead_IsTheChainsFirstCall() {
        var array = LongIdentifier[..77];
        Oracle.Agrees(
            Statement($"var c3 = {array}[0]{LastCall};"),
            Statement(
                $"""
                 var c3 = {array}[0]
                             {LastCall};
                 """
            )
        );

        var shorter = LongIdentifier[..70];
        Oracle.Agrees(
            Statement($"var d1 = {shorter}.Items[0]{LastCall};"),
            Statement(
                $"""
                 var d1 = {shorter}.Items[0]
                             {LastCall};
                 """
            )
        );

        Oracle.Agrees(
            Statement($"var d2 = xxxxxxxxxxxxxxxxxxxx.Items[0]{LastCall}.Third(gggggggggggggggggggggggggg);"),
            Statement(
                $"""
                 var d2 = xxxxxxxxxxxxxxxxxxxx.Items[0]
                             {LastCall}
                             .Third(gggggggggggggggggggggggggg);
                 """
            )
        );
    }

    /// <summary>
    ///     ⚠ Not every head is a call. An identifier, an object creation, a parenthesised call and a
    ///     bare <c>x?.</c> leave <c>.Other</c> the chain's first — and only — call, and the oracle chops
    ///     its argument list exactly as Skala did before.
    /// </summary>
    [Fact]
    public void AHeadThatIsNotACall_StillChopsTheLastArgumentList() {
        Oracle.Agrees(
            Statement($"var i1 = {LongIdentifier}{LastCall};"),
            Statement($"var i1 = {LongIdentifier}.Other(\n{Chopped(12)});")
        );

        Oracle.Agrees(
            Statement($"var a9 = new SomeType({A}, {B}, {C}).Other(ddddddddddddddddddddddddddddddddd, {E}, {F});"),
            Statement(
                $"""
                 var a9 = new SomeType({A}, {B}, {C}).Other(
                             ddddddddddddddddddddddddddddddddd,
                             {E},
                             {F}
                         );
                 """
            )
        );

        Oracle.Agrees(
            Statement($"var c6 = ({Root}){LastCall};"),
            Statement($"var c6 = ({Root}).Other(\n{Chopped(12)});")
        );

        Oracle.Agrees(
            Statement($"var d4 = {LongIdentifier[..80]}?{LastCall};"),
            Statement($"var d4 = {LongIdentifier[..80]}?.Other(\n{Chopped(12)});")
        );
    }

    /// <summary>
    ///     The negative control that makes the proof real: the answer moves with
    ///     <c>skala_wrap_chained_method_calls</c>. At <c>chop_always</c> a two-call chain that fits is
    ///     chopped when its head is a call or an indexer and left whole when its head is an identifier;
    ///     ⚠ and the first call is the innermost one — <c>alpha.SomeMethod(a, b)[0].Other(c, d)</c>
    ///     breaks before <c>.Other</c> only, never before <c>.SomeMethod</c>, so a <c>[0]</c> behind a
    ///     dotted call does not make that call's dot a point. ⚠ <c>wrap_if_long</c> is not asserted:
    ///     the oracle's fill keeps a last link's <c>.Other(</c> on the line and chops its arguments,
    ///     and Skala's breaks before the link — on a property root as much as on a call root, so it is
    ///     not this fix's — SK-DIV-0129.
    /// </summary>
    [Fact]
    public void TheChainKey_MovesTheAnswer_AndTheFirstCallIsTheInnermostOne() {
        const string Short = """
                             var s1 = SomeMethod(a, b).Other(c, d);
                                     var s2 = x.Other(c, d);
                                     var d7 = arr[0].Other(c, d);
                                     var d8 = x.Items[0].Other(c, d);
                                     var e1 = alpha.SomeMethod(a, b)[0].Other(c, d);
                                     var e2 = alpha.SomeMethod(a, b).Other(c, d)[0].Third(e);
                                     var e4 = SomeMethod(a, b).Other(c, d)[0].Third(e);
                             """;

        var chopped = FormatWith(Statement(Short), ("skala_wrap_chained_method_calls", "chop_always"));
        Assert.Equal(
            Statement(
                """
                var s1 = SomeMethod(a, b)
                            .Other(c, d);
                        var s2 = x.Other(c, d);
                        var d7 = arr[0]
                            .Other(c, d);
                        var d8 = x.Items[0]
                            .Other(c, d);
                        var e1 = alpha.SomeMethod(a, b)[0]
                            .Other(c, d);
                        var e2 = alpha.SomeMethod(a, b)
                            .Other(c, d)[0]
                            .Third(e);
                        var e4 = SomeMethod(a, b)
                            .Other(c, d)[0]
                            .Third(e);
                """
            ),
            chopped.TrimEnd('\n')
        );

        // The export's value leaves every one of them whole.
        Assert.Equal(Statement(Short), Format.Text(Statement(Short)).TrimEnd('\n'));
    }

    /// <summary>
    ///     The chain's level under an owner that has already spent one: an expression body's arrow
    ///     breaks first and the dot lands one level past the body, as for any invocation head
    ///     (SK-DIV-0112); a chain inside an argument list takes the list's level and then its own.
    /// </summary>
    [Fact]
    public void UnderAnArrow_AndInsideAnArgumentList_TheChainSpendsItsOwnLevel() {
        Oracle.Agrees(
            $$"""
              class T {
                  object E() => SomeMethod({{A}}, {{B}}, {{C}}, gggggggggg){{LastCall}};
              }
              """,
            $$"""
              class T {
                  object E() =>
                      SomeMethod({{A}}, {{B}}, {{C}}, gggggggggg)
                          {{LastCall}};
              }
              """
        );

        Oracle.Agrees(
            Statement($"Consume({Root}{LastCall});"),
            Statement(
                $"""
                 Consume(
                             {Root}
                                 {LastCall}
                         );
                 """
            )
        );
    }

    /// <summary>
    ///     ⚠ A chain pays its level once. An author's break before a dot that is not a point — the
    ///     <c>.Qux</c> of a <c>.Baz.Qux()</c> run, the <c>.Select</c> after <c>.Members</c>, the
    ///     <c>.Lines</c> feeding an indexer — is the chain <em>frame</em>'s to pay, and the chain's
    ///     group has already opened the same level when it has a point break before it. The oracle
    ///     keeps every dot of these at one level; Skala put the link after the kept break one deeper,
    ///     on an identifier head (<c>alpha.Foo(a)</c>) as much as on a call head, and the writer's
    ///     same-line collapse hid it only while the kept break came before any point break
    ///     (<c>alpha.Members</c> / <c>.Select</c>, which was already right). Found applying SK-DIV-0128
    ///     to Skala's own <c>OptionRegistryModel.cs</c> and <c>LinqChainAndLoopShapeBatchTests.cs</c>.
    /// </summary>
    [Fact]
    public void AKeptBreakBeforeANonPointDot_AfterAPointBreak_TakesNoSecondLevel() {
        const string Kept = """
                            var s2 = Get()["k"]
                                        .Members
                                        .Select(static a => new KeyValuePair<string, string>(a.Key, a.Value))
                                        .OrderBy(static a => a.Key, StringComparer.Ordinal)
                                        .ToList();
                                    var s3 = diagnostics[0].Location.SourceTree!
                                        .GetText(TestContext.Current.CancellationToken)
                                        .Lines[diagnostics[0].Location.GetLineSpan().StartLinePosition.Line]
                                        .ToString();
                                    var s5 = Foo(a)
                                        .Bar()
                                        .Baz
                                        .Qux();
                                    var s6 = alpha.Foo(a)
                                        .Bar()
                                        .Baz
                                        .Qux();
                                    var s8 = Foo(a)
                                        .Baz
                                        .Qux();
                                    var s1 = alpha.Members
                                        .Select(static a => new KeyValuePair<string, string>(a.Key, a.Value))
                                        .OrderBy(static a => a.Key, StringComparer.Ordinal)
                                        .ToList();
                            """;

        Oracle.Agrees(Statement(Kept), Statement(Kept));
    }
}
